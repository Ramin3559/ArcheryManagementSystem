namespace EShooting.Application.Common.Models;

public sealed class StaffMemberItem
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public Guid StaffPositionId { get; init; }
    public string PositionName { get; init; } = string.Empty;
    public Guid AccessProfileId { get; init; }
    public string AccessProfileName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
}
