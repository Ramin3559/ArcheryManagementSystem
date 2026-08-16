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
        List<TrainingSession> sessions;
        IReadOnlyCollection<SubscriptionSchedule> schedules;
        IReadOnlyCollection<Lane> lanes;
        try
        {
            schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
            sessions = (await repository.GetSessionsLightAsync(cancellationToken)).ToList();
            lanes = await repository.GetLanesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return;
        }
        var laneByNumber = lanes
            .GroupBy(x => x.Number)
            .ToDictionary(g => g.Key, g => g.First());
        var nowUtc = DateTime.UtcNow;

        foreach (var schedule in schedules)
        {
            try
            {
                await PersistOneScheduleAsync(
                    repository,
                    schedule,
                    day,
                    lanes,
                    laneByNumber,
                    sessions,
                    schedules,
                    nowUtc,
                    mode,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Unique index / FK bir abunədə — qalanlar və zolaq paneli işləsin.
            }
        }
    }

    private static async Task PersistOneScheduleAsync(
        ITrainingCenterRepository repository,
        SubscriptionSchedule schedule,
        DateTime day,
        IReadOnlyCollection<Lane> lanes,
        Dictionary<int, Lane> laneByNumber,
        List<TrainingSession> sessions,
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        DateTime nowUtc,
        SubscriptionSessionEnsureMode mode,
        CancellationToken cancellationToken)
    {
        if (!SubscriptionOccurrenceJson.TryResolveOccurrence(
                schedule,
                day,
                out var startTimeLocal,
                out var durationMinutes,
                out var laneNumber))
        {
            return;
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
            return;
        }

        var slotLocal = day.Add(startTimeLocal);
        var startUtc = AzerbaijanTime.NormalizeScheduleInputToUtc(
            DateTime.SpecifyKind(slotLocal, DateTimeKind.Unspecified));
        var endUtc = startUtc.AddMinutes(durationMinutes);

        var sameDay = sessions
            .Where(s => SessionMatchesScheduleDay(s, schedule, day, startUtc))
            .OrderByDescending(s => DateTimeAssumedUtc.AsUtc(s.StartTimeUtc))
            .ToList();

        var existingAtSlot = sessions.FirstOrDefault(s =>
            s.AthleteId == schedule.AthleteId
            && s.LaneId == lane.Id
            && DateTimeAssumedUtc.AsUtc(s.StartTimeUtc) == startUtc);
        if (existingAtSlot is not null && sameDay.All(s => s.Id != existingAtSlot.Id))
        {
            sameDay.Add(existingAtSlot);
        }

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
                return;
            }

            if (mode == SubscriptionSessionEnsureMode.ForceOpen
                || NeedsResync(open, lane.Id, startUtc, endUtc))
            {
                if (NeedsResync(open, lane.Id, startUtc, endUtc))
                {
                    // Pool abunədə konkret zolaq Başlat-da seçilir — dolu olsa belə siyahı üçün saxlanır.
                    if (slotBusy && !provisionalPool)
                    {
                        return;
                    }

                    if (WouldConflictUniqueSlot(sessions, open.Id, schedule.AthleteId, lane.Id, startUtc))
                    {
                        return;
                    }

                    open.LaneId = lane.Id;
                    open.SubscriptionScheduleId = schedule.Id;
                    open.StartTimeUtc = startUtc;
                    open.EndTimeUtc = endUtc;
                    await repository.UpdateSessionAsync(open, cancellationToken);
                }
            }

            return;
        }

        // MissingOnly: bu gün artıq oynayıb bitibsə yenidən plan açma.
        var completed = sameDay.FirstOrDefault(s => s.Status == SessionStatus.Completed);
        if (completed is not null && mode == SubscriptionSessionEnsureMode.MissingOnly)
        {
            return;
        }

        // Create / reopen onto lane only when slot is free (pool üçün istisna).
        if (slotBusy && !provisionalPool)
        {
            return;
        }

        if (completed is not null)
        {
            if (WouldConflictUniqueSlot(sessions, completed.Id, schedule.AthleteId, lane.Id, startUtc))
            {
                return;
            }

            completed.Status = SessionStatus.Scheduled;
            completed.ActivatedAtUtc = null;
            completed.LaneId = lane.Id;
            completed.SubscriptionScheduleId = schedule.Id;
            completed.StartTimeUtc = startUtc;
            completed.EndTimeUtc = endUtc;
            await repository.UpdateSessionAsync(completed, cancellationToken);
            return;
        }

        if (WouldConflictUniqueSlot(sessions, excludeSessionId: null, schedule.AthleteId, lane.Id, startUtc))
        {
            return;
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

    private static bool WouldConflictUniqueSlot(
        IEnumerable<TrainingSession> sessions,
        Guid? excludeSessionId,
        Guid athleteId,
        Guid laneId,
        DateTime startUtc)
    {
        return sessions.Any(s =>
            s.Id != excludeSessionId
            && s.AthleteId == athleteId
            && s.LaneId == laneId
            && DateTimeAssumedUtc.AsUtc(s.StartTimeUtc) == startUtc);
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
