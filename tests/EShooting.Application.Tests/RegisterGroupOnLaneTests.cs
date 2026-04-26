using EShooting.Application.Sessions.Commands;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Tests;

public sealed class RegisterGroupOnLaneTests
{
    [Fact]
    public async Task RegisterGroupOnLane_CreatesSingleSessionWithAllNames()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 1, LaneType = LaneType.Amateur };
        var repository = new InMemoryTrainingCenterRepository(lanes: [lane]);
        var notifier = new SpyRealtimeNotifier();
        var handler = new RegisterGroupOnLaneCommandHandler(repository, notifier);
        var start = DateTime.UtcNow;

        var result = await handler.Handle(
            new RegisterGroupOnLaneCommand(
                new[] { "Ali", "Veli", "Leyla" },
                lane.Number,
                start,
                60,
                false),
            CancellationToken.None);

        var item = Assert.Single(result.Sessions);
        Assert.Equal(start, item.StartTimeUtc);
        Assert.Equal(start.AddHours(1), item.EndTimeUtc);
        Assert.Contains("Ali", item.AthleteName);
        Assert.Contains("Veli", item.AthleteName);
        Assert.Contains("Leyla", item.AthleteName);
        Assert.Contains(lane.Number, notifier.LaneUpdates);
    }

    [Fact]
    public async Task RegisterGroupOnLane_WithOpenEndedActiveSession_Throws()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 2, LaneType = LaneType.Amateur };
        var openEndedStart = DateTime.UtcNow.AddMinutes(-5);
        var blocking = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            LaneId = lane.Id,
            StartTimeUtc = openEndedStart,
            EndTimeUtc = openEndedStart,
            Status = SessionStatus.Active
        };

        var repository = new InMemoryTrainingCenterRepository(lanes: [lane], sessions: [blocking]);
        var notifier = new SpyRealtimeNotifier();
        var handler = new RegisterGroupOnLaneCommandHandler(repository, notifier);

        var action = () => handler.Handle(
            new RegisterGroupOnLaneCommand(
                new[] { "Friend 1", "Friend 2" },
                lane.Number,
                DateTime.UtcNow,
                60,
                false),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task RegisterGroupOnLane_WhenSubscriberDemandRequiresFreeLanes_Throws()
    {
        var lane1 = new Lane { Id = Guid.NewGuid(), Number = 1, LaneType = LaneType.Amateur };
        var lane2 = new Lane { Id = Guid.NewGuid(), Number = 2, LaneType = LaneType.Amateur };
        var todayLocal = DateTime.Now.Date;
        var startLocal = todayLocal.AddHours(18);
        var startUtc = startLocal.ToUniversalTime();

        var existingSession = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            LaneId = lane1.Id,
            StartTimeUtc = startUtc,
            EndTimeUtc = startUtc.AddHours(1),
            Status = SessionStatus.Scheduled
        };

        var subscriberDemand = new SubscriptionSchedule
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            DayOfWeek = (int)todayLocal.DayOfWeek,
            StartTimeLocal = new TimeSpan(18, 0, 0),
            DurationMinutes = 60,
            ActiveFromDateLocal = todayLocal,
            ActiveToDateLocal = todayLocal,
            IsEnabled = true
        };

        var repository = new InMemoryTrainingCenterRepository(
            lanes: [lane1, lane2],
            sessions: [existingSession],
            subscriptionSchedules: [subscriberDemand]);

        var notifier = new SpyRealtimeNotifier();
        var handler = new RegisterGroupOnLaneCommandHandler(repository, notifier);

        var action = () => handler.Handle(
            new RegisterGroupOnLaneCommand(
                new[] { "Ali", "Veli" },
                lane2.Number,
                startUtc,
                60,
                false),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }
}
