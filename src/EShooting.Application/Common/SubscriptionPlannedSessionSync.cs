using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

public enum SubscriptionSessionEnsureMode
{
    /// <summary>
    /// Açıq seans yoxdursa yaradır. Eyni gün tamamlanmış / aktiv seansa toxunmur.
    /// </summary>
    MissingOnly,

    /// <summary>Açıq seansı yenilə; yoxdursa yenisini yarat (completed olsa belə).</summary>
    ForceOpen
}

/// <summary>
/// Abunə təqvimində olan günlər üçün planlı TrainingSession yaradır.
/// </summary>
public static class SubscriptionPlannedSessionSync
{
    public static Task EnsureForLocalDateAsync(
        ITrainingCenterRepository repository,
        DateTime localDate,
        CancellationToken cancellationToken)
        => EnsureForLocalDateAsync(repository, localDate, SubscriptionSessionEnsureMode.MissingOnly, cancellationToken);

    public static async Task EnsureForLocalDateAsync(
        ITrainingCenterRepository repository,
        DateTime localDate,
        SubscriptionSessionEnsureMode mode,
        CancellationToken cancellationToken)
    {
        var day = localDate.Date;
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var sessions = (await repository.GetSessionsLightAsync(cancellationToken)).ToList();
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var laneByNumber = lanes.ToDictionary(x => x.Number);
        var nowUtc = DateTime.UtcNow;

        foreach (var schedule in schedules)
        {
            if (!SubscriptionOccurrenceJson.TryResolveOccurrence(
                    schedule,
                    day,
                    out var startTimeLocal,
                    out var durationMinutes,
                    out var laneNumber))
            {
                continue;
            }

            if (laneNumber <= 0)
            {
                laneNumber = ResolveProvisionalLaneNumber(
                    schedule,
                    lanes,
                    sessions,
                    schedules,
                    day,
                    startTimeLocal,
                    durationMinutes,
                    nowUtc);
            }

            if (laneNumber <= 0 || !laneByNumber.TryGetValue(laneNumber, out var lane))
            {
                continue;
            }

            var slotLocal = day.Add(startTimeLocal);
            var startUtc = AzerbaijanTime.NormalizeScheduleInputToUtc(
                DateTime.SpecifyKind(slotLocal, DateTimeKind.Unspecified));
            var endUtc = startUtc.AddMinutes(durationMinutes);

            var sameDay = sessions
                .Where(s => SessionMatchesScheduleDay(s, schedule, day, startUtc))
                .OrderByDescending(s => DateTimeAssumedUtc.AsUtc(s.StartTimeUtc))
                .ToList();

            var explicitLane = SubscriptionPoolCapacity.ResolveExplicitLaneNumber(schedule, day);
            var provisionalPool = explicitLane <= 0;

            var slotBusy = SubscriptionSlotConflict.IsLaneSlotBusy(
                sessions,
                schedules,
                lanes,
                laneNumber,
                day,
                startTimeLocal,
                durationMinutes,
                nowUtc,
                excludeScheduleId: schedule.Id);

            var open = sameDay.FirstOrDefault(s => s.Status != SessionStatus.Completed);
            if (open is not null)
            {
                // Aktiv seansın zolağı/vaxtı sync ilə dəyişməsin (fors-major / Başlat seçimi qorunsun).
                if (SessionActivationRules.HasActivation(open))
                {
                    continue;
                }

                if (mode == SubscriptionSessionEnsureMode.ForceOpen
                    || NeedsResync(open, lane.Id, startUtc, endUtc))
                {
                    if (NeedsResync(open, lane.Id, startUtc, endUtc))
                    {
                        // Pool abunədə konkret zolaq Başlat-da seçilir — dolu olsa belə siyahı üçün saxlanır.
                        if (slotBusy && !provisionalPool)
                        {
                            continue;
                        }

                        open.LaneId = lane.Id;
                        open.SubscriptionScheduleId = schedule.Id;
                        open.StartTimeUtc = startUtc;
                        open.EndTimeUtc = endUtc;
                        await repository.UpdateSessionAsync(open, cancellationToken);
                    }
                }

                continue;
            }

            // MissingOnly: bu gün artıq oynayıb bitibsə yenidən plan açma.
            var completed = sameDay.FirstOrDefault(s => s.Status == SessionStatus.Completed);
            if (completed is not null && mode == SubscriptionSessionEnsureMode.MissingOnly)
            {
                continue;
            }

            // Create / reopen onto lane only when slot is free (pool üçün istisna).
            if (slotBusy && !provisionalPool)
            {
                continue;
            }

            if (completed is not null)
            {
                completed.Status = SessionStatus.Scheduled;
                completed.ActivatedAtUtc = null;
                completed.LaneId = lane.Id;
                completed.SubscriptionScheduleId = schedule.Id;
                completed.StartTimeUtc = startUtc;
                completed.EndTimeUtc = endUtc;
                await repository.UpdateSessionAsync(completed, cancellationToken);
                continue;
            }

            var created = await repository.AddSessionAsync(
                new TrainingSession
                {
                    AthleteId = schedule.AthleteId,
                    LaneId = lane.Id,
                    SubscriptionScheduleId = schedule.Id,
                    StartTimeUtc = startUtc,
                    EndTimeUtc = endUtc,
                    Status = SessionStatus.Scheduled
                },
                cancellationToken);

            sessions.Add(created);
        }
    }

    private static bool SessionMatchesScheduleDay(
        TrainingSession session,
        SubscriptionSchedule schedule,
        DateTime day,
        DateTime plannedStartUtc)
    {
        if (AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(session.StartTimeUtc)) != day)
        {
            return false;
        }

        if (session.SubscriptionScheduleId == schedule.Id)
        {
            return true;
        }

        // Köhnə seanslarda ScheduleId boş ola bilər — eyni müştəri + yaxın saat.
        if (session.SubscriptionScheduleId is not null || session.AthleteId != schedule.AthleteId)
        {
            return false;
        }

        var delta = (DateTimeAssumedUtc.AsUtc(session.StartTimeUtc) - plannedStartUtc).Duration();
        return delta <= TimeSpan.FromHours(2);
    }

    private static bool NeedsResync(TrainingSession open, Guid laneId, DateTime startUtc, DateTime endUtc)
        => open.LaneId != laneId
           || DateTimeAssumedUtc.AsUtc(open.StartTimeUtc) != startUtc
           || DateTimeAssumedUtc.AsUtc(open.EndTimeUtc) != endUtc;

    private static int ResolveProvisionalLaneNumber(
        SubscriptionSchedule schedule,
        IReadOnlyCollection<Lane> lanes,
        IReadOnlyCollection<TrainingSession> sessions,
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        DateTime dayLocal,
        TimeSpan startTimeLocal,
        int durationMinutes,
        DateTime nowUtc)
    {
        if (schedule.LastAssignedLaneNumber is > 0)
        {
            var last = schedule.LastAssignedLaneNumber.Value;
            if (!SubscriptionSlotConflict.IsLaneSlotBusy(
                    sessions,
                    schedules,
                    lanes,
                    last,
                    dayLocal,
                    startTimeLocal,
                    durationMinutes,
                    nowUtc,
                    excludeScheduleId: schedule.Id))
            {
                return last;
            }
        }

        var candidates = LaneReservationRules.FilterLanesByPreferredType(lanes, schedule.PreferredLaneType)
            .OrderBy(x => x.Number);
        foreach (var lane in candidates)
        {
            if (!SubscriptionSlotConflict.IsLaneSlotBusy(
                    sessions,
                    schedules,
                    lanes,
                    lane.Number,
                    dayLocal,
                    startTimeLocal,
                    durationMinutes,
                    nowUtc,
                    excludeScheduleId: schedule.Id))
            {
                return lane.Number;
            }
        }

        // Hələ boş konkret zolaq yoxdur — Başlat-da seçiləcək; siyahı üçün pool-un ilk zolağı.
        return candidates.Select(x => x.Number).FirstOrDefault();
    }
}
