using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record SubmitScoreCommand(Guid SessionId, int RoundNumber, int Value) : IRequest<int>;

public sealed class SubmitScoreCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<SubmitScoreCommand, int>
{
    private const int MaxScoreValue = 999;

    public async Task<int> Handle(SubmitScoreCommand request, CancellationToken cancellationToken)
    {
        if (request.Value is < -MaxScoreValue or > MaxScoreValue)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Value), $"Score must be between {-MaxScoreValue} and {MaxScoreValue}.");
        }

        var session = await repository.GetSessionByIdAsync(request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session not found.");

        if (session.Status == EShooting.Domain.Enums.SessionStatus.Completed)
        {
            throw new InvalidOperationException("Session is already finished.");
        }

        var startUtc = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var endUtc = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        var hasValidTimer = endUtc > startUtc;
        if (hasValidTimer && DateTime.UtcNow >= endUtc)
        {
            throw new InvalidOperationException("Session is already finished.");
        }

        // Server-side guard: total score must never go below 0.
        // If a negative score is submitted that would drop the total below 0,
        // clamp it to -currentTotal to bring total to exactly 0.
        var currentTotal = Math.Max(0, session.TotalScore);
        var adjustedValue = request.Value;
        if (adjustedValue < 0)
        {
            var maxSubtract = currentTotal;
            var requestedSubtract = -adjustedValue;
            var effectiveSubtract = Math.Min(requestedSubtract, maxSubtract);
            adjustedValue = -effectiveSubtract;
        }

        // No-op (e.g., trying to subtract while total is already 0).
        if (adjustedValue == 0)
        {
            return currentTotal;
        }

        session.Scores.Add(new ScoreEntry
        {
            // Empty Guid lets EF treat this as a new row (INSERT), not an existing row (UPDATE).
            Id = Guid.Empty,
            SessionId = session.Id,
            RoundNumber = request.RoundNumber,
            Value = adjustedValue
        });

        await repository.UpdateSessionAsync(session, cancellationToken);

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var lane = lanes.FirstOrDefault(x => x.Id == session.LaneId);
        if (lane is not null)
        {
            await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        }

        var total = Math.Max(0, session.TotalScore);
        await notifier.PublishScoreUpdateAsync(session.Id, total, cancellationToken);
        return total;
    }
}
