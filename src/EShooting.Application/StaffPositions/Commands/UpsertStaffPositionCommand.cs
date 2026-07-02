using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.StaffPositions.Commands;

public sealed record UpsertStaffPositionCommand(
    Guid? Id,
    string Name,
    string? Description,
    Guid? DefaultAccessProfileId,
    bool IsActive) : IRequest<Guid>;

public sealed class UpsertStaffPositionCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpsertStaffPositionCommand, Guid>
{
    public async Task<Guid> Handle(UpsertStaffPositionCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Vəzifə adı mütləqdir.");
        }

        if (request.DefaultAccessProfileId is Guid profileId && profileId != Guid.Empty)
        {
            var profile = await repository.GetAccessProfileByIdAsync(profileId, cancellationToken);
            if (profile is null || profile.IsDeleted || !profile.IsActive)
            {
                throw new InvalidOperationException("Standart icazə profili tapılmadı və ya deaktivdir.");
            }
        }

        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var defaultProfileId = request.DefaultAccessProfileId is Guid id && id != Guid.Empty ? id : (Guid?)null;

        if (request.Id is null || request.Id == Guid.Empty)
        {
            var created = await repository.AddStaffPositionAsync(new StaffPosition
            {
                Name = name,
                Description = description,
                DefaultAccessProfileId = defaultProfileId,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            return created.Id;
        }

        var existing = await repository.GetStaffPositionByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException("Vəzifə tapılmadı.");

        existing.Name = name;
        existing.Description = description;
        existing.DefaultAccessProfileId = defaultProfileId;
        existing.IsActive = request.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateStaffPositionAsync(existing, cancellationToken);
        return existing.Id;
    }
}
