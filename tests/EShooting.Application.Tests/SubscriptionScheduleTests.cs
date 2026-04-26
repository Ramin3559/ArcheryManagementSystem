using EShooting.Application.Subscriptions.Commands;
using EShooting.Application.Subscriptions.Queries;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Tests;

public sealed class SubscriptionScheduleTests
{
    [Fact]
    public async Task CreateSubscriptionPackage_OddPattern_CreatesRequestedVisitCount()
    {
        var lane1 = new Lane { Id = Guid.NewGuid(), Number = 1, LaneType = LaneType.Amateur };
        var lane2 = new Lane { Id = Guid.NewGuid(), Number = 2, LaneType = LaneType.Amateur };
        var repository = new InMemoryTrainingCenterRepository(lanes: [lane1, lane2]);
        var handler = new CreateSubscriptionPackageCommandHandler(repository);
        var nextMonday = NextDay(DateTime.Today, DayOfWeek.Monday);

        var result = await handler.Handle(
            new CreateSubscriptionPackageCommand(
                "Package Athlete",
                "1-3-5",
                12,
                new TimeSpan(19, 0, 0),
                60,
                nextMonday,
                PreferredLaneTypesByDayOfWeek: new Dictionary<int, PreferredLaneType>(),
                IsFullPackage: false),
            CancellationToken.None);

        Assert.Equal(12, result.CreatedCount);
        var schedules = await repository.GetSubscriptionSchedulesAsync(CancellationToken.None);
        Assert.Equal(12, schedules.Count);
        Assert.All(schedules, s => Assert.Contains(s.DayOfWeek, new[] { 1, 3, 5 }));
    }

    [Fact]
    public async Task CreateSubscriptionPackage_WhenSlotCapacityExceeded_Throws()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 1, LaneType = LaneType.Amateur };
        var existingAthlete = new Athlete { Id = Guid.NewGuid(), FullName = "Existing", IsSubscriber = true };
        var targetDate = NextDay(DateTime.Today, DayOfWeek.Monday);
        var existingSchedule = new SubscriptionSchedule
        {
            Id = Guid.NewGuid(),
            AthleteId = existingAthlete.Id,
            LaneNumber = 0,
            DayOfWeek = (int)DayOfWeek.Monday,
            StartTimeLocal = new TimeSpan(19, 0, 0),
            DurationMinutes = 60,
            ActiveFromDateLocal = targetDate,
            ActiveToDateLocal = targetDate,
            IsEnabled = true
        };

        var repository = new InMemoryTrainingCenterRepository(
            lanes: [lane],
            athletes: [existingAthlete],
            subscriptionSchedules: [existingSchedule]);

        var handler = new CreateSubscriptionPackageCommandHandler(repository);

        var action = () => handler.Handle(
            new CreateSubscriptionPackageCommand(
                "New Athlete",
                "1-3-5",
                1,
                new TimeSpan(19, 0, 0),
                60,
                targetDate,
                PreferredLaneTypesByDayOfWeek: new Dictionary<int, PreferredLaneType>(),
                IsFullPackage: false),
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task CreateSubscriptionSchedule_ValidRequest_CreatesSchedule()
    {
        var lane = new Lane { Id = Guid.NewGuid(), Number = 7, LaneType = LaneType.Amateur };
        var repository = new InMemoryTrainingCenterRepository(lanes: [lane]);
        var handler = new CreateSubscriptionScheduleCommandHandler(repository);

        var id = await handler.Handle(
            new CreateSubscriptionScheduleCommand(
                AthleteId: null,
                "Subscriber Athlete",
                (int)DayOfWeek.Monday,
                new TimeSpan(19, 0, 0),
                60,
                DateTime.Today,
                DateTime.Today.AddMonths(1),
                PreferredLaneType: PreferredLaneType.Any,
                LaneNumber: 0,
                IsFullPackage: false),
            CancellationToken.None);

        var schedules = await repository.GetSubscriptionSchedulesAsync(CancellationToken.None);
        var created = Assert.Single(schedules);
        Assert.Equal(id, created.Id);
        var athletes = await repository.GetAthletesAsync(CancellationToken.None);
        var createdAthlete = Assert.Single(athletes);
        Assert.Equal(createdAthlete.Id, created.AthleteId);
        Assert.Equal(0, created.LaneNumber);
        Assert.Equal(60, created.DurationMinutes);
    }

    [Fact]
    public async Task GetSubscriptionSchedules_ReturnsAthleteName()
    {
        var athlete = new Athlete { Id = Guid.NewGuid(), FullName = "Monthly User", IsSubscriber = true };
        var schedule = new SubscriptionSchedule
        {
            Id = Guid.NewGuid(),
            AthleteId = athlete.Id,
            LaneNumber = 8,
            DayOfWeek = (int)DayOfWeek.Wednesday,
            StartTimeLocal = new TimeSpan(18, 30, 0),
            DurationMinutes = 45,
            ActiveFromDateLocal = DateTime.Today,
            ActiveToDateLocal = DateTime.Today.AddMonths(1),
            IsEnabled = true
        };

        var repository = new InMemoryTrainingCenterRepository(
            athletes: [athlete],
            subscriptionSchedules: [schedule]);

        var handler = new GetSubscriptionSchedulesQueryHandler(repository);

        var items = await handler.Handle(new GetSubscriptionSchedulesQuery(), CancellationToken.None);
        var item = Assert.Single(items);

        Assert.Equal(athlete.FullName, item.AthleteName);
        Assert.Equal(schedule.DurationMinutes, item.DurationMinutes);
    }

    private static DateTime NextDay(DateTime fromDate, DayOfWeek targetDay)
    {
        var cursor = fromDate.Date;
        while (cursor.DayOfWeek != targetDay)
        {
            cursor = cursor.AddDays(1);
        }

        return cursor;
    }
}
