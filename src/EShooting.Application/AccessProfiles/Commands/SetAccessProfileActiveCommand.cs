using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.AccessProfiles.Commands;

public sealed record SetAccessProfileActiveCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetAccessProfileActiveCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetAccessProfileActiveCommand>
{
    public async Task Handle(SetAccessProfileActiveCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetAccessProfileByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("İcazə profili tapılmadı.");

        item.IsActive = request.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateAccessProfileAsync(item, cancellationToken);
    }
}
