using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.AccessProfiles.Commands;

public sealed record SetAccessProfileDeletedCommand(Guid Id, bool IsDeleted) : IRequest;

public sealed class SetAccessProfileDeletedCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetAccessProfileDeletedCommand>
{
    public async Task Handle(SetAccessProfileDeletedCommand request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetAccessProfileByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("İcazə profili tapılmadı.");

        profile.IsDeleted = request.IsDeleted;
        if (request.IsDeleted)
        {
            profile.IsActive = false;
        }

        profile.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateAccessProfileAsync(profile, cancellationToken);
    }
}
