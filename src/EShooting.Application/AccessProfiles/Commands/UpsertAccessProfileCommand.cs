using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.AccessProfiles.Commands;

public sealed record UpsertAccessProfileCommand(
    Guid? Id,
    string Name,
    string? Description,
    bool CanRegisterCustomers,
    bool CanManageSubscriptions,
    bool CanManageSessions,
    bool CanManageEquipment,
    bool CanViewHistory,
    bool IsActive) : IRequest<Guid>;

public sealed class UpsertAccessProfileCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpsertAccessProfileCommand, Guid>
{
    public async Task<Guid> Handle(UpsertAccessProfileCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Profil adı mütləqdir.");
        }

        if (!request.CanRegisterCustomers
            && !request.CanManageSubscriptions
            && !request.CanManageSessions
            && !request.CanManageEquipment
            && !request.CanViewHistory)
        {
            throw new InvalidOperationException("Ən az bir icazə seçilməlidir.");
        }

        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.Id is null || request.Id == Guid.Empty)
        {
            var created = await repository.AddAccessProfileAsync(new AccessProfile
            {
                Name = name,
                Description = description,
                CanRegisterCustomers = request.CanRegisterCustomers,
                CanManageSubscriptions = request.CanManageSubscriptions,
                CanManageSessions = request.CanManageSessions,
                CanManageEquipment = request.CanManageEquipment,
                CanViewHistory = request.CanViewHistory,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            return created.Id;
        }

        var existing = await repository.GetAccessProfileByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException("İcazə profili tapılmadı.");

        existing.Name = name;
        existing.Description = description;
        existing.CanRegisterCustomers = request.CanRegisterCustomers;
        existing.CanManageSubscriptions = request.CanManageSubscriptions;
        existing.CanManageSessions = request.CanManageSessions;
        existing.CanManageEquipment = request.CanManageEquipment;
        existing.CanViewHistory = request.CanViewHistory;
        existing.IsActive = request.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateAccessProfileAsync(existing, cancellationToken);
        return existing.Id;
    }
}
