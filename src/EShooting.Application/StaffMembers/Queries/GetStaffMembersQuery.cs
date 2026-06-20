using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.StaffMembers.Queries;

public sealed record GetStaffMembersQuery(bool ActiveOnly = false) : IRequest<IReadOnlyCollection<StaffMemberItem>>;

public sealed class GetStaffMembersQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetStaffMembersQuery, IReadOnlyCollection<StaffMemberItem>>
{
    public async Task<IReadOnlyCollection<StaffMemberItem>> Handle(
        GetStaffMembersQuery request,
        CancellationToken cancellationToken)
    {
        var members = await repository.GetStaffMembersAsync(request.ActiveOnly, cancellationToken);

        return members
            .OrderBy(x => x.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .Select(Map)
            .ToArray();
    }

    internal static StaffMemberItem Map(StaffMember x) => new()
    {
        Id = x.Id,
        FirstName = x.FirstName,
        LastName = x.LastName,
        FullName = $"{x.FirstName} {x.LastName}".Trim(),
        StaffPositionId = x.StaffPositionId,
        PositionName = x.StaffPosition?.Name ?? "—",
        AccessProfileId = x.AccessProfileId,
        AccessProfileName = x.AccessProfile?.Name ?? "—",
        PhoneNumber = x.PhoneNumber,
        IsActive = x.IsActive
    };
}
