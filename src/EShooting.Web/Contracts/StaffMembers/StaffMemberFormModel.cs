using EShooting.Application.Common.Models;

namespace EShooting.Web.Contracts.StaffMembers;

public sealed class StaffMemberFormModel
{
    public Guid? Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid StaffPositionId { get; set; }
    public Guid AccessProfileId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Pin { get; set; }
    public bool IsActive { get; set; } = true;

    public IReadOnlyCollection<StaffPositionItem> Positions { get; set; } = [];
    public IReadOnlyCollection<AccessProfileItem> Profiles { get; set; } = [];
}
