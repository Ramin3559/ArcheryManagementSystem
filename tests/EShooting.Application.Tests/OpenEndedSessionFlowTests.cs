using EShooting.Application.Sessions.Commands;
using EShooting.Application.Sessions.Queries;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Tests;

public sealed class OpenEndedSessionFlowTests
{
    [Fact]
    public async Task Schedule_WithZeroDuration_CreatesOpenEndedActiveSession()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 1, LaneType = LaneType.Amateur };
        var athlete = new Athlete { Id = Guid.NewGuid(), FullName = "Test Athlete" };
        var repository = new InMemoryTrainingCenterRepository(lanes: [lane], athletes: [athlete]);
        var notifier = new SpyRealtimeNotifier();
        var handler = new ScheduleSessionCommandHandler(repository, notifier);
        var startUtc = DateTime.UtcNow.AddSeconds(-3);

        var sessionId = await handler.Handle(
            new ScheduleSessionCommand(athlete.Id, lane.Number, startUtc, 0, false, PreferredLaneType.Any),
            CancellationToken.None);

        var created = await repository.GetSessionByIdAsync(sessionId, CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal(startUtc, created!.StartTimeUtc);
        Assert.Equal(startUtc, created.EndTimeUtc);
        Assert.Equal(SessionStatus.Active, created.Status);
        Assert.Contains(lane.Number, notifier.LaneUpdates);
    }

    [Fact]
    public async Task SubmitScore_OpenEndedActiveSession_AcceptsScore()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 2, LaneType = LaneType.Amateur };
        var openEndedStart = DateTime.UtcNow.AddMinutes(-5);
        var session = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            LaneId = lane.Id,
            StartTimeUtc = openEndedStart,
            EndTimeUtc = openEndedStart,
            Status = SessionStatus.Active,
            Scores = []
        };

        var repository = new InMemoryTrainingCenterRepository(lanes: [lane], sessions: [session]);
        var notifier = new SpyRealtimeNotifier();
        var handler = new SubmitScoreCommandHandler(repository, notifier);

        var total = await handler.Handle(
            new SubmitScoreCommand(session.Id, RoundNumber: 1, Value: 9),
            CancellationToken.None);

        Assert.Equal(9, total);
        var updated = await repository.GetSessionByIdAsync(session.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Single(updated!.Scores);
        Assert.Contains(lane.Number, notifier.LaneUpdates);
        Assert.Contains(notifier.ScoreUpdates, x => x.SessionId == session.Id && x.TotalScore == 9);
    }

    [Fact]
    public async Task SubmitScore_CompletedSession_ThrowsInvalidOperationException()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 3, LaneType = LaneType.Professional };
        var session = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            LaneId = lane.Id,
            StartTimeUtc = DateTime.UtcNow.AddMinutes(-10),
            EndTimeUtc = DateTime.UtcNow.AddMinutes(-10),
            Status = SessionStatus.Completed
        };

        var repository = new InMemoryTrainingCenterRepository(lanes: [lane], sessions: [session]);
        var notifier = new SpyRealtimeNotifier();
        var handler = new SubmitScoreCommandHandler(repository, notifier);

        var action = () => handler.Handle(
            new SubmitScoreCommand(session.Id, RoundNumber: 2, Value: 10),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task Dashboard_OpenEndedStartedSession_ShowsActiveState()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 4, LaneType = LaneType.Professional };
        var athlete = new Athlete { Id = Guid.NewGuid(), FullName = "Open Ended Athlete" };
        var openEndedStart = DateTime.UtcNow.AddMinutes(-2);
        var session = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = athlete.Id,
            LaneId = lane.Id,
            StartTimeUtc = openEndedStart,
            EndTimeUtc = openEndedStart,
            Status = SessionStatus.Active
        };

        var repository = new InMemoryTrainingCenterRepository(
            lanes: [lane],
            athletes: [athlete],
            sessions: [session]);

        var handler = new GetLaneDashboardQueryHandler(repository);

        var lanes = await handler.Handle(new GetLaneDashboardQuery(), CancellationToken.None);
        var item = Assert.Single(lanes);

        Assert.Equal("Active", item.Status);
        Assert.Equal("In progress", item.Warning);
        Assert.Equal(athlete.FullName, item.AthleteName);
    }

    [Fact]
    public async Task Dashboard_OpenEndedFutureSession_ShowsScheduledAndCountdownWarning()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 5, LaneType = LaneType.Amateur };
        var athlete = new Athlete { Id = Guid.NewGuid(), FullName = "Future Athlete" };
        var openEndedFutureStart = DateTime.UtcNow.AddMinutes(2);
        var session = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = athlete.Id,
            LaneId = lane.Id,
            StartTimeUtc = openEndedFutureStart,
            EndTimeUtc = openEndedFutureStart,
            Status = SessionStatus.Active
        };

        var repository = new InMemoryTrainingCenterRepository(
            lanes: [lane],
            athletes: [athlete],
            sessions: [session]);

        var handler = new GetLaneDashboardQueryHandler(repository);

        var lanes = await handler.Handle(new GetLaneDashboardQuery(), CancellationToken.None);
        var item = Assert.Single(lanes);

        Assert.Equal("Scheduled", item.Status);
        Assert.StartsWith("Starts in ", item.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteSession_MarksSessionAsCompleted()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 6, LaneType = LaneType.Amateur };
        var openEndedStart = DateTime.UtcNow.AddMinutes(-3);
        var session = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            LaneId = lane.Id,
            StartTimeUtc = openEndedStart,
            EndTimeUtc = openEndedStart,
            Status = SessionStatus.Active
        };

        var repository = new InMemoryTrainingCenterRepository(lanes: [lane], sessions: [session]);
        var notifier = new SpyRealtimeNotifier();
        var handler = new CompleteSessionCommandHandler(repository, notifier);

        await handler.Handle(new CompleteSessionCommand(session.Id), CancellationToken.None);

        var updated = await repository.GetSessionByIdAsync(session.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(SessionStatus.Completed, updated!.Status);
        Assert.Contains(lane.Number, notifier.LaneUpdates);
    }

    [Fact]
    public async Task Schedule_WhenSubscriberReservedOnSameLaneAndTime_ThrowsInvalidOperationException()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 11, LaneType = LaneType.Professional };
        var athlete = new Athlete { Id = Guid.NewGuid(), FullName = "Manual User" };
        var todayLocal = DateTime.Now.Date;
        var reservedStartLocal = todayLocal.AddHours(19);
        var reservedStartUtc = reservedStartLocal.ToUniversalTime();

        var subscriberSchedule = new SubscriptionSchedule
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            LaneNumber = lane.Number,
            LastAssignedLaneNumber = lane.Number,
            DayOfWeek = (int)todayLocal.DayOfWeek,
            StartTimeLocal = new TimeSpan(19, 0, 0),
            DurationMinutes = 60,
            ActiveFromDateLocal = todayLocal,
            ActiveToDateLocal = todayLocal,
            IsEnabled = true
        };

        var repository = new InMemoryTrainingCenterRepository(
            lanes: [lane],
            athletes: [athlete],
            subscriptionSchedules: [subscriberSchedule]);

        var notifier = new SpyRealtimeNotifier();
        var handler = new ScheduleSessionCommandHandler(repository, notifier);

        var action = () => handler.Handle(
            new ScheduleSessionCommand(athlete.Id, lane.Number, reservedStartUtc, 60, false, PreferredLaneType.Any),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task Schedule_WhenSubscriberDemandRequiresFreeLanes_ThrowsInvalidOperationException()
    {
        var lane1 = new Lane { Id = Guid.NewGuid(), Number = 1, LaneType = LaneType.Amateur };
        var lane2 = new Lane { Id = Guid.NewGuid(), Number = 2, LaneType = LaneType.Amateur };
        var lane3 = new Lane { Id = Guid.NewGuid(), Number = 3, LaneType = LaneType.Amateur };
        var athlete = new Athlete { Id = Guid.NewGuid(), FullName = "Manual Occupancy" };
        var todayLocal = DateTime.Now.Date;
        var slotStartLocal = todayLocal.AddHours(17);
        var slotStartUtc = slotStartLocal.ToUniversalTime();

        var existingSession = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = athlete.Id,
            LaneId = lane1.Id,
            StartTimeUtc = slotStartUtc,
            EndTimeUtc = slotStartUtc.AddHours(1),
            Status = SessionStatus.Scheduled
        };

        var subscriberA = new SubscriptionSchedule
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            DayOfWeek = (int)todayLocal.DayOfWeek,
            StartTimeLocal = new TimeSpan(17, 0, 0),
            DurationMinutes = 60,
            ActiveFromDateLocal = todayLocal,
            ActiveToDateLocal = todayLocal,
            IsEnabled = true
        };

        var subscriberB = new SubscriptionSchedule
        {
            Id = Guid.NewGuid(),
            AthleteId = Guid.NewGuid(),
            DayOfWeek = (int)todayLocal.DayOfWeek,
            StartTimeLocal = new TimeSpan(17, 0, 0),
            DurationMinutes = 60,
            ActiveFromDateLocal = todayLocal,
            ActiveToDateLocal = todayLocal,
            IsEnabled = true
        };

        var repository = new InMemoryTrainingCenterRepository(
            lanes: [lane1, lane2, lane3],
            athletes: [athlete],
            sessions: [existingSession],
            subscriptionSchedules: [subscriberA, subscriberB]);

        var notifier = new SpyRealtimeNotifier();
        var handler = new ScheduleSessionCommandHandler(repository, notifier);

        var action = () => handler.Handle(
            new ScheduleSessionCommand(athlete.Id, lane2.Number, slotStartUtc, 60, false, PreferredLaneType.Any),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task Schedule_WhenSubscriberSessionAlreadyOccupiesReservedLane_AllowsManualOnRemainingCapacity()
    {
        var lane1 = new Lane { Id = Guid.NewGuid(), Number = 1, LaneType = LaneType.Amateur };
        var lane2 = new Lane { Id = Guid.NewGuid(), Number = 2, LaneType = LaneType.Amateur };
        var lane3 = new Lane { Id = Guid.NewGuid(), Number = 3, LaneType = LaneType.Amateur };
        var manualAthlete = new Athlete { Id = Guid.NewGuid(), FullName = "Manual Occupancy" };
        var subscriberAthlete = new Athlete { Id = Guid.NewGuid(), FullName = "Subscriber", IsSubscriber = true };
        var todayLocal = DateTime.Now.Date;
        var slotStartLocal = todayLocal.AddHours(17);
        var slotStartUtc = slotStartLocal.ToUniversalTime();

        var manualSession = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = manualAthlete.Id,
            LaneId = lane1.Id,
            StartTimeUtc = slotStartUtc,
            EndTimeUtc = slotStartUtc.AddHours(1),
            Status = SessionStatus.Scheduled
        };

        var subscriberSession = new TrainingSession
        {
            Id = Guid.NewGuid(),
            AthleteId = subscriberAthlete.Id,
            LaneId = lane3.Id,
            StartTimeUtc = slotStartUtc,
            EndTimeUtc = slotStartUtc.AddHours(1),
            Status = SessionStatus.Scheduled
        };

        var subscriberDemand = new SubscriptionSchedule
        {
            Id = Guid.NewGuid(),
            AthleteId = subscriberAthlete.Id,
            DayOfWeek = (int)todayLocal.DayOfWeek,
            StartTimeLocal = new TimeSpan(17, 0, 0),
            DurationMinutes = 60,
            ActiveFromDateLocal = todayLocal,
            ActiveToDateLocal = todayLocal,
            IsEnabled = true
        };

        var repository = new InMemoryTrainingCenterRepository(
            lanes: [lane1, lane2, lane3],
            athletes: [manualAthlete, subscriberAthlete],
            sessions: [manualSession, subscriberSession],
            subscriptionSchedules: [subscriberDemand]);

        var notifier = new SpyRealtimeNotifier();
        var handler = new ScheduleSessionCommandHandler(repository, notifier);

        var createdSessionId = await handler.Handle(
            new ScheduleSessionCommand(manualAthlete.Id, lane2.Number, slotStartUtc, 60, false, PreferredLaneType.Any),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, createdSessionId);
    }
}
