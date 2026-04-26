using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record DeleteLastScoreCommand(Guid SessionId) : IRequest<int?>;

public sealed class DeleteLastScoreCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<DeleteLastScoreCommand, int?>
{
    public async Task<int?> Handle(DeleteLastScoreCommand request, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        // Delete last score row and get updated total.
        var totalScore = await repository.DeleteLastScoreAsync(request.SessionId, cancellationToken);
        if (totalScore is null)
        {
            return null;
        }

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var lane = lanes.FirstOrDefault(x => x.Id == session.LaneId);
        if (lane is not null)
        {
            await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        }

        await notifier.PublishScoreUpdateAsync(session.Id, totalScore.Value, cancellationToken);
        return totalScore.Value;
    }
}

