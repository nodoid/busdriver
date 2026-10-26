using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class SessionTests
{
    private static BusDriverSession NewSession(string route = "2", DifficultyLevel level = DifficultyLevel.Easy) =>
        new(DifficultyProfile.For(level), RouteCatalog.Get(route), 1985);

    [Fact]
    public void A_shift_starts_parked_at_the_first_terminus()
    {
        var session = NewSession();
        Assert.False(session.IsFinished);
        Assert.Equal(session.Route.FirstStop.Id, session.Route.Stops[0].Id);
        Assert.Equal(session.Route.Stops[1].Id, session.NextStop?.Id);
        Assert.Equal(8, session.Now.Hours);
        Assert.Equal(TimeSpan.FromMinutes(12), session.ShiftRemaining);
    }

    [Fact]
    public void Serving_a_stop_boards_and_drops_at_the_same_time()
    {
        var session = NewSession("2");
        session.TeleportToStop(1);

        session.Update(0.02f, DriveInput.None, doorsPressed: true);

        Assert.NotNull(session.LastService);
        Assert.True(session.LastService!.Boarded > 0);
        Assert.True(session.Bus.PassengerCount > 0);
        Assert.True(session.Score.FaresPence > 0);
        Assert.Equal(1, session.Score.OnTimeStops);
        Assert.Equal(0, session.Score.LateStops);
    }

    [Fact]
    public void Opening_the_doors_leaves_the_bus_facing_the_way_it_was_driven()
    {
        var session = NewSession("2");
        session.TeleportToStop(1);
        session.Bus.Teleport(session.Bus.Position, heading: 2.4f);

        session.Update(0.02f, DriveInput.None, doorsPressed: true);

        Assert.Equal(2.4f, session.Bus.Heading, 3);
    }

    [Fact]
    public void A_terminus_is_alighting_only()
    {
        var session = NewSession("1C");
        session.TeleportToStop(1);
        session.Update(0.02f, DriveInput.None, true);
        session.TeleportToStop(2);
        session.Update(0.02f, DriveInput.None, true);

        var carriedBefore = session.Score.PassengersCarried;
        var onboardBefore = session.Bus.PassengerCount;

        session.TeleportToStop(3);
        session.Update(0.02f, DriveInput.None, true);

        Assert.NotNull(session.LastService);
        Assert.Equal(0, session.LastService!.Boarded);
        Assert.True(session.LastService.Alighted > 0);
        Assert.Equal(0, session.Bus.PassengerCount);
        Assert.True(onboardBefore > 0);
        Assert.Equal(carriedBefore, session.Score.PassengersCarried);
    }

    [Fact]
    public void Reaching_the_final_terminus_completes_the_trip()
    {
        var session = NewSession("1C");
        for (var i = 1; i <= 3; i++)
        {
            session.TeleportToStop(i);
            session.Update(0.02f, DriveInput.None, true);
        }

        Assert.True(session.IsAwaitingLayover);
        Assert.Equal(1, session.Score.TripsCompleted);
        Assert.True(session.Score.TripBonus >= 150);
        Assert.True(session.Score.PassengersDelivered > 0);
    }

    [Fact]
    public void After_the_layover_the_bus_works_the_return_trip()
    {
        var session = NewSession("1C");
        for (var i = 1; i <= 3; i++)
        {
            session.TeleportToStop(i);
            session.Update(0.02f, DriveInput.None, true);
        }

        session.Update(33f, DriveInput.None, false);

        Assert.False(session.IsAwaitingLayover);
        Assert.Equal(-1, session.Direction);
        Assert.Equal(session.Route.Stops[2].Id, session.NextStop?.Id);
    }

    [Fact]
    public void Lateness_beyond_tolerance_costs_points()
    {
        var session = NewSession("2", DifficultyLevel.Easy);
        session.TeleportToStop(1);
        session.Update(100f, DriveInput.None, false);
        session.TeleportToStop(1);
        session.Update(0.02f, DriveInput.None, true);

        Assert.Equal(1, session.Score.LateStops);
        Assert.Equal(0, session.Score.OnTimeStops);
        Assert.True(session.Score.PunctualityPoints < 0);
    }

    [Fact]
    public void Doors_will_not_open_away_from_the_stop()
    {
        var session = NewSession();
        session.Update(0.02f, DriveInput.None, true);
        var events = session.DrainEvents();

        Assert.Null(session.LastService);
        Assert.Contains(events, e => e.Kind == SessionEventKind.DoorsRefused);
    }

    [Fact]
    public void The_shift_ends_on_time()
    {
        var session = NewSession("2", DifficultyLevel.Easy);
        session.Update(12 * 60 + 1, DriveInput.None, false);

        Assert.True(session.IsFinished);
        Assert.Equal("End of shift", session.FinishReason);
        Assert.Equal(TimeSpan.Zero, session.ShiftRemaining);
    }

    [Fact]
    public void A_written_off_bus_ends_the_shift()
    {
        var session = NewSession();
        session.Bus.AddImpactDamage(100);
        session.Update(0.02f, DriveInput.None, false);

        Assert.True(session.IsFinished);
        Assert.Equal("Bus written off", session.FinishReason);
        Assert.True(session.Score.DamagePenalty > 0);
    }

    [Fact]
    public void Events_can_be_drained_once()
    {
        var session = NewSession();
        session.TeleportToStop(1);
        session.Update(0.02f, DriveInput.None, true);

        var first = session.DrainEvents();
        var second = session.DrainEvents();

        Assert.NotEmpty(first);
        Assert.Empty(second);
    }

    [Fact]
    public void Difficulty_changes_the_shift_configuration()
    {
        var easy = NewSession("2", DifficultyLevel.Easy);
        var hard = NewSession("2", DifficultyLevel.Hard);

        Assert.True(hard.Difficulty.ShiftMinutes < easy.Difficulty.ShiftMinutes);
        Assert.True(hard.Traffic.Count > easy.Traffic.Count);
        Assert.Equal(BusType.SingleDecker, hard.BusType);
        Assert.Equal(BusTypeProfile.SingleDecker.Capacity, hard.Bus.Capacity);
    }
}
