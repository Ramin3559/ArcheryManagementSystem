using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;
using static EShooting.Application.Sessions.SessionEquipmentRules;

namespace EShooting.Application.Sessions.Commands;

public sealed record CompleteSessionCommand(Guid SessionId) : IRequest;

public sealed class CompleteSessionCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<CompleteSessionCommand>
{
    public async Task Handle(CompleteSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionByIdAsync(request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session not found.");

        if (session.Status == SessionStatus.Completed)
        {
            return;
        }

        var equipmentIssues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        if (HasPendingRentalEquipment(session, equipmentIssues))
        {
            throw new InvalidOperationException("İcarə avadanlığı təhvil alınmalıdır.");
        }

        session.Status = SessionStatus.Completed;
        await repository.UpdateSessionAsync(session, cancellationToken);

        var lane = (await repository.GetLanesAsync(cancellationToken)).FirstOrDefault(x => x.Id == session.LaneId);
        if (lane is not null)
        {
            await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        }
    }
}
