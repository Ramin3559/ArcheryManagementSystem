using EShooting.Application.AccessProfiles;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.AccessProfiles.Commands;

public sealed record UpsertAccessProfileCommand(
    Guid? Id,
    string Name,
    string? Description,
    bool CanRegisterCustomers,
    bool CanViewCustomerDetails,
    bool CanEditCustomerDetails,
    bool CanManageSubscriptions,
    bool CanRecordPayments,
    bool CanApplyDiscount,
    bool CanGrantComplimentarySession,
    bool CanManageSessions,
    bool CanManageEquipment,
    bool CanSellEquipment,
    bool CanReturnEquipment,
    bool CanAccessPlanset,
    bool CanIssueEquipmentRental,
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

        if (!ReceptionPermissionRules.HasAny(
                request.CanRegisterCustomers,
                request.CanViewCustomerDetails,
                request.CanEditCustomerDetails,
                request.CanManageSubscriptions,
                request.CanRecordPayments,
                request.CanApplyDiscount,
                request.CanGrantComplimentarySession,
                request.CanManageSessions,
                request.CanManageEquipment,
                request.CanSellEquipment,
                request.CanReturnEquipment,
                request.CanAccessPlanset,
                request.CanIssueEquipmentRental,
                request.CanViewHistory))
        {
            throw new InvalidOperationException("Ən az bir icazə seçilməlidir.");
        }

        var name = request.Name.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.Id is null || request.Id == Guid.Empty)
        {
            var created = new AccessProfile
            {
                Name = name,
                Description = description,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            ReceptionPermissionRules.ApplyPermissions(
                created,
                request.CanRegisterCustomers,
                request.CanViewCustomerDetails,
                request.CanEditCustomerDetails,
                request.CanManageSubscriptions,
                request.CanRecordPayments,
                request.CanApplyDiscount,
                request.CanGrantComplimentarySession,
                request.CanManageSessions,
                request.CanManageEquipment,
                request.CanSellEquipment,
                request.CanReturnEquipment,
                request.CanAccessPlanset,
                request.CanIssueEquipmentRental,
                request.CanViewHistory);

            var added = await repository.AddAccessProfileAsync(created, cancellationToken);
            return added.Id;
        }

        var existing = await repository.GetAccessProfileByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException("İcazə profili tapılmadı.");

        existing.Name = name;
        existing.Description = description;
        existing.IsActive = request.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        ReceptionPermissionRules.ApplyPermissions(
            existing,
            request.CanRegisterCustomers,
            request.CanViewCustomerDetails,
            request.CanEditCustomerDetails,
            request.CanManageSubscriptions,
            request.CanRecordPayments,
            request.CanApplyDiscount,
            request.CanGrantComplimentarySession,
            request.CanManageSessions,
            request.CanManageEquipment,
            request.CanSellEquipment,
            request.CanReturnEquipment,
            request.CanAccessPlanset,
            request.CanIssueEquipmentRental,
            request.CanViewHistory);

        await repository.UpdateAccessProfileAsync(existing, cancellationToken);
        return existing.Id;
    }
}
