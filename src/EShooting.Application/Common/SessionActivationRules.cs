using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

public static class SessionActivationRules
{
    /// <summary>
    /// Sessiya yalnız «Aktiv et» / «İndi başla» ilə aktiv sayılır (ActivatedAtUtc doldurulmalıdır).
    /// </summary>
    public static bool HasActivation(TrainingSession session)
    {
        return session.ActivatedAtUtc is not null;
    }

    /// <summary>
    /// Gəliş sayılır yalnız «İndi başla» / aktiv et olanda.
    /// Plan olub aktivləşməyən seans (keçmiş gündə Completed olsa belə) gəlməmişdir.
    /// </summary>
    public static bool CountsAsAttendedVisit(TrainingSession session)
    {
        if (session.Status is not (SessionStatus.Active or SessionStatus.Completed))
        {
            return false;
        }

        return HasActivation(session);
    }

    public static void MarkActivated(TrainingSession session, DateTime activatedAtUtc)
    {
        session.ActivatedAtUtc = DateTimeAssumedUtc.AsUtc(activatedAtUtc);
        session.Status = SessionStatus.Active;
    }
}
