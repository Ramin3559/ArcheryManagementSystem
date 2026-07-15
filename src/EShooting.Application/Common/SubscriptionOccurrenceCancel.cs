using System.Globalization;
using System.Text.Json;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

/// <summary>
/// Planlı abunə seansını ləğv edəndə həmin günü abunə təqvimindən də çıxarır
/// (yoxsa sync yenidən seans yaradar / plan yenidən görünər).
/// </summary>
public static class SubscriptionOccurrenceCancel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task CancelScheduledSessionAsync(
        ITrainingCenterRepository repository,
        TrainingSession session,
        CancellationToken cancellationToken)
    {
        if (session.Status != SessionStatus.Completed)
        {
            session.Status = SessionStatus.Completed;
            session.ActivatedAtUtc = null;
            session.EndTimeUtc = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
            await repository.UpdateSessionAsync(session, cancellationToken);
        }

        if (session.SubscriptionScheduleId is not Guid scheduleId || scheduleId == Guid.Empty)
        {
            return;
        }

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var schedule = schedules.FirstOrDefault(s => s.Id == scheduleId);
        if (schedule is null)
        {
            return;
        }

        var day = AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(session.StartTimeUtc));
        var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var excluded = SubscriptionOccurrenceJson.DeserializeExcluded(schedule.ExcludedOccurrenceDatesJson);
        if (excluded.Add(key))
        {
            schedule.ExcludedOccurrenceDatesJson = JsonSerializer.Serialize(
                excluded.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                JsonOptions);
            await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
        }
    }
}
