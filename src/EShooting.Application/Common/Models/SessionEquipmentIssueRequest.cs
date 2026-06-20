using EShooting.Domain.Enums;

namespace EShooting.Application.Common.Models;

public sealed class SessionEquipmentIssueRequest
{
    public Guid EquipmentItemId { get; init; }
    public EquipmentIssueType IssueType { get; init; }
}
