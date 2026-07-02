using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using static EShooting.Application.Common.DateTimeAssumedUtc;

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

    public sealed class LanePendingRentalInfo
    {
        public Guid SessionId { get; init; }
        public string AthleteName { get; init; } = string.Empty;
        public IReadOnlyList<string> EquipmentLabels { get; init; } = [];
    }

    public static LanePendingRentalInfo? ResolveLanePendingRental(
        IEnumerable<TrainingSession> laneSessions,
        IEnumerable<SessionEquipmentIssue> allIssues,
        IReadOnlyDictionary<Guid, string> equipmentNames,
        IReadOnlyDictionary<Guid, string> athleteNames,
        DateTime nowUtc)
    {
        foreach (var session in laneSessions.OrderByDescending(s => s.StartTimeUtc))
        {
            if (!HasPendingRentalEquipment(session, allIssues))
            {
                continue;
            }

            var start = AsUtc(session.StartTimeUtc);
            if (nowUtc < start)
            {
                continue;
            }

            var openIssues = allIssues
                .Where(x => x.SessionId == session.Id
                    && x.IssueType == EquipmentIssueType.Rental
                    && x.ReturnedAtUtc is null)
                .ToList();

            var labels = openIssues.Count > 0
                ? openIssues.Select(i =>
                {
                    var name = equipmentNames.GetValueOrDefault(i.EquipmentItemId) ?? "Avadanlıq";
                    var qty = Math.Max(1, i.Quantity);
                    return qty > 1 ? $"{name} ×{qty}" : name;
                }).ToList()
                : ["Avadanlıq"];

            var athleteName = athleteNames.GetValueOrDefault(session.AthleteId) ?? "—";
            return new LanePendingRentalInfo
            {
                SessionId = session.Id,
                AthleteName = athleteName,
                EquipmentLabels = labels
            };
        }

        return null;
    }
}
