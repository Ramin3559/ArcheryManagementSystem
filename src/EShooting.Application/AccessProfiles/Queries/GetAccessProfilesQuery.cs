using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.AccessProfiles.Queries;

public sealed record GetAccessProfilesQuery(bool ActiveOnly = false) : IRequest<IReadOnlyCollection<AccessProfileItem>>;

public sealed class GetAccessProfilesQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetAccessProfilesQuery, IReadOnlyCollection<AccessProfileItem>>
{
    public async Task<IReadOnlyCollection<AccessProfileItem>> Handle(
        GetAccessProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetAccessProfilesAsync(request.ActiveOnly, cancellationToken);

        return items
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(Map)
            .ToArray();
    }

    internal static AccessProfileItem Map(AccessProfile x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Description = x.Description,
        CanRegisterCustomers = x.CanRegisterCustomers,
        CanManageSubscriptions = x.CanManageSubscriptions,
        CanManageSessions = x.CanManageSessions,
        CanManageEquipment = x.CanManageEquipment,
        CanViewHistory = x.CanViewHistory,
        IsActive = x.IsActive
    };
}
