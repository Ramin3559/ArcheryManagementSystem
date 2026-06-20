using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Sessions;

public static class SessionEquipmentRules
{
    public static bool HasPendingRentalEquipment(
        TrainingSession session,
        IEnumerable<SessionEquipmentIssue> allIssues)
    {
        var sessionIssues = allIssues.Where(x => x.SessionId == session.Id).ToList();

        if (sessionIssues.Any(x => x.IssueType == EquipmentIssueType.Rental && x.ReturnedAtUtc is null))
        {
            return true;
        }

        if (session.IsEquipmentIssued
            && session.EquipmentReturnedAtUtc is null
            && sessionIssues.Count == 0)
        {
            return true;
        }

        return false;
    }
}
