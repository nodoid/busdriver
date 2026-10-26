using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class TimetableTests
{
    [Fact]
    public void Outbound_timetable_starts_at_the_first_stop_and_runs_in_order()
    {
        var route = RouteCatalog.Get("2");
        var start = new TimeSpan(8, 0, 0);
        var trip = Timetable.Build(route, 1, start);

        Assert.Equal(start, trip.StartTime);
        Assert.Equal(start, trip[0].Due);
        Assert.Equal(0, trip[0].RouteIndex);
        Assert.Equal(route.Stops.Count - 1, trip[trip.Stops.Count - 1].RouteIndex);
        Assert.Equal(route.LastStop.Id, trip[trip.Stops.Count - 1].Stop.Id);

        for (var i = 1; i < trip.Stops.Count; i++)
        {
            Assert.True(trip[i].Due > trip[i - 1].Due, "due times must increase");
            Assert.True(trip[i].CumulativeDistance > trip[i - 1].CumulativeDistance);
        }
    }

    [Fact]
    public void Return_timetable_runs_the_other_way()
    {
        var route = RouteCatalog.Get("2");
        var trip = Timetable.Build(route, -1, TimeSpan.FromHours(9));

        Assert.Equal(-1, trip.Direction);
        Assert.Equal(route.Stops.Count - 1, trip[0].RouteIndex);
        Assert.Equal(0, trip[trip.Stops.Count - 1].RouteIndex);
        Assert.Equal(0, trip.FinalRouteIndex);
        Assert.True(trip.IsFinalStop(0));
        Assert.False(trip.IsFinalStop(route.Stops.Count - 1));
    }

    [Fact]
    public void Every_stop_has_exactly_one_scheduled_row()
    {
        var route = RouteCatalog.Get("9");
        var trip = Timetable.Build(route, 1, TimeSpan.Zero);
        Assert.Equal(route.Stops.Count, trip.Stops.Count);
        foreach (var stop in route.Stops)
        {
            Assert.NotNull(trip.AtRouteIndex(route.Stops.ToList().IndexOf(stop)));
        }
    }

    [Fact]
    public void Faster_planned_speed_makes_a_shorter_trip()
    {
        var route = RouteCatalog.Get("11");
        var slow = Timetable.Build(route, 1, TimeSpan.Zero, 100);
        var fast = Timetable.Build(route, 1, TimeSpan.Zero, 250);
        Assert.True(fast.Duration < slow.Duration);
    }

    [Fact]
    public void Direction_must_be_out_or_back()
    {
        var route = RouteCatalog.Get("2");
        Assert.Throws<ArgumentOutOfRangeException>(() => Timetable.Build(route, 0, TimeSpan.Zero));
    }
}
