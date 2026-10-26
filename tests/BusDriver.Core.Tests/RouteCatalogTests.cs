using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class RouteCatalogTests
{
    [Fact]
    public void Network_has_four_heritage_and_twelve_new_routes()
    {
        Assert.Equal(16, RouteCatalog.Routes.Count);
        Assert.Equal(4, RouteCatalog.Routes.Count(r => r.Heritage));
        Assert.Equal(12, RouteCatalog.Routes.Count(r => !r.Heritage));
    }

    [Fact]
    public void Route_numbers_are_unique()
    {
        var numbers = RouteCatalog.Routes.Select(r => r.Number).ToArray();
        Assert.Equal(numbers.Length, numbers.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Every_route_runs_terminus_to_terminus(Route route)
    {
        Assert.True(route.FirstStop.IsTerminus, $"{route} does not start at a terminus");
        Assert.True(route.LastStop.IsTerminus, $"{route} does not end at a terminus");
        Assert.True(route.Stops.Count >= 3);

        foreach (var stop in route.Stops.Skip(1).SkipLast(1))
        {
            Assert.False(stop.IsTerminus, $"{route} calls at terminus {stop} in the middle");
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Route_does_not_visit_the_same_stop_twice(Route route)
    {
        Assert.Equal(route.Stops.Count, route.Stops.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void All_stops_are_on_a_road()
    {
        foreach (var stop in RouteCatalog.Stops)
        {
            Assert.True(
                TownMap.IsRoadTile(stop.TileX, stop.TileY),
                $"{stop} at {stop.TileX},{stop.TileY} is not on a road");
        }
    }

    [Fact]
    public void Terminuses_are_unique_and_all_used()
    {
        Assert.Equal(8, RouteCatalog.Terminuses.Count);
        Assert.All(RouteCatalog.Terminuses, t => Assert.True(t.IsTerminus));

        var used = RouteCatalog.Routes.SelectMany(r => r.Stops)
            .Where(s => s.IsTerminus)
            .Select(s => s.Id)
            .Distinct()
            .Count();
        Assert.Equal(RouteCatalog.Terminuses.Count, used);
    }

    [Fact]
    public void Stop_ids_are_unique()
    {
        var ids = RouteCatalog.Stops.Select(s => s.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    public static IEnumerable<object[]> Routes() =>
        RouteCatalog.Routes.Select(r => new object[] { r });
}
