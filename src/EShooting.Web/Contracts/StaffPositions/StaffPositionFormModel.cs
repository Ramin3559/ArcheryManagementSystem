namespace EShooting.Web.Contracts.StaffPositions;

public sealed class StaffPositionFormModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
