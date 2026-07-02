using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Application.StaffMembers;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.StaffMembers.Queries;

public sealed record GetStaffMemberByPinQuery(string Pin) : IRequest<ReceptionStaffSessionItem?>;

public sealed class GetStaffMemberByPinQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetStaffMemberByPinQuery, ReceptionStaffSessionItem?>
{
    public async Task<ReceptionStaffSessionItem?> Handle(
        GetStaffMemberByPinQuery request,
        CancellationToken cancellationToken)
    {
        var pin = (request.Pin ?? "").Trim();
        if (pin.Length is < 4 or > 6 || !pin.All(char.IsDigit))
        {
            return null;
        }

        var member = await repository.GetStaffMemberByPinAsync(pin, cancellationToken);
        if (member is null || member.IsDeleted || !member.IsActive)
        {
            return null;
        }

        var profile = member.AccessProfile;
        if (profile is null || profile.IsDeleted || !profile.IsActive)
        {
            return null;
        }

        return ReceptionStaffSessionMapper.Map(member, profile);
    }
}
