using EShooting.Domain.Enums;

namespace EShooting.Web.Contracts.Sessions;

public sealed class SessionEquipmentIssueDto
{
    public Guid EquipmentItemId { get; set; }
    public EquipmentIssueType IssueType { get; set; }
}
