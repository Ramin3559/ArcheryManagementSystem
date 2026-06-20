using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using MediatR;

namespace EShooting.Application.StaffMembers.Queries;

public sealed record GetStaffMemberByIdQuery(Guid Id) : IRequest<StaffMemberItem?>;

public sealed class GetStaffMemberByIdQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetStaffMemberByIdQuery, StaffMemberItem?>
{
    public async Task<StaffMemberItem?> Handle(
        GetStaffMemberByIdQuery request,
        CancellationToken cancellationToken)
    {
        var member = await repository.GetStaffMemberByIdAsync(request.Id, cancellationToken);
        return member is null ? null : GetStaffMembersQueryHandler.Map(member);
    }
}
