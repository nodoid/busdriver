using System.Numerics;
using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class TrafficTests
{
    [Fact]
    public void Traffic_count_follows_the_difficulty()
    {
        var town = new TownMap();
        var easy = new TrafficSystem(1, town, DifficultyProfile.Easy.TrafficVehicles, Vector2.Zero);
        var hard = new TrafficSystem(1, town, DifficultyProfile.Hard.TrafficVehicles, Vector2.Zero);

        Assert.Equal(4, easy.Count);
        Assert.Equal(14, hard.Count);
        Assert.True(hard.Count > easy.Count);
    }

    [Fact]
    public void Traffic_stays_on_the_road()
    {
        var town = new TownMap();
        var traffic = new TrafficSystem(42, town, 12, Vector2.Zero);
        var bus = new Bus(BusType.SingleDecker);
        bus.Teleport(new Vector2(TownMap.RoadCentresX[0], TownMap.RoadCentresY[0]));
        var events = new List<SessionEvent>();

        for (var i = 0; i < 3000; i++)
        {
            traffic.Update(1 / 60f, new WeatherState(WeatherKind.Clear, 1), bus, 1, events);
        }

        Assert.All(traffic.Vehicles, v =>
        {
            var tile = town.TileAt(v.Position);
            Assert.True(
                tile is TileKind.Road or TileKind.Terminus,
                $"{v.Kind} wandered onto {tile} at {v.Position}");
        });
    }

    [Fact]
    public void A_traffic_collision_damages_the_bus()
    {
        var town = new TownMap();
        var bus = new Bus(BusType.SingleDecker);
        var centre = new Vector2(TownMap.RoadCentresX[1], TownMap.RoadCentresY[1]);
        bus.Teleport(centre);

        var traffic = new TrafficSystem(3, town, 1, Vector2.Zero);
        var vehicle = traffic.Vehicles[0];
        vehicle.Horizontal = true;
        vehicle.Line = centre.Y;
        vehicle.Direction = 1;
        vehicle.Along = centre.X;
        vehicle.Speed = 0;

        traffic.Update(1 / 60f, new WeatherState(WeatherKind.Clear, 1), bus, 1, new List<SessionEvent>());

        Assert.True(bus.Damage > 0);
    }

    [Fact]
    public void A_terminus_light_is_red_while_a_bus_is_in_the_yard()
    {
        var town = new TownMap();
        var terminus = RouteCatalog.Terminuses[0];
        var bus = new Bus(BusType.SingleDecker);
        bus.Teleport(terminus.Position);

        var traffic = new TrafficSystem(5, town, 4, Vector2.Zero);
        traffic.Update(1 / 60f, new WeatherState(WeatherKind.Clear, 1), bus, 1, new List<SessionEvent>());

        Assert.True(traffic.Lights.Single(l => l.StopId == terminus.Id).Red);
        Assert.All(traffic.Lights.Where(l => l.StopId != terminus.Id),
            light => Assert.False(light.Red));

        bus.Teleport(new Vector2(TownMap.RoadCentresX[4], TownMap.RoadCentresY[3]));
        traffic.Update(1 / 60f, new WeatherState(WeatherKind.Clear, 1), bus, 1, new List<SessionEvent>());

        Assert.All(traffic.Lights, light => Assert.False(light.Red));
    }

    [Fact]
    public void A_red_terminus_light_holds_traffic_at_the_stop_line()
    {
        var town = new TownMap();
        var terminus = RouteCatalog.Terminuses[0];
        var bus = new Bus(BusType.SingleDecker);
        bus.Teleport(terminus.Position);

        var traffic = new TrafficSystem(6, town, 1, Vector2.Zero);
        // Come in along the road the terminus stands on, heading for the yard.
        var vehicle = traffic.Vehicles[0];
        vehicle.Horizontal = true;
        vehicle.Line = terminus.Position.Y;
        vehicle.Direction = -1;
        vehicle.Along = terminus.Position.X + 260f;
        vehicle.Speed = 200f;
        vehicle.TurnCooldown = 5f;

        var events = new List<SessionEvent>();
        for (var i = 0; i < 120; i++)
        {
            traffic.Update(1 / 60f, new WeatherState(WeatherKind.Clear, 1), bus, 1, events);
        }

        Assert.True(vehicle.Along > terminus.Position.X,
            $"traffic drove into the yard at {vehicle.Along}");
    }

    [Fact]
    public void Traffic_keeps_to_the_left()
    {
        var town = new TownMap();
        var traffic = new TrafficSystem(4, town, 6, Vector2.Zero);

        foreach (var vehicle in traffic.Vehicles.Where(v => v.Horizontal))
        {
            var offset = vehicle.Position.Y - vehicle.Line;
            Assert.Equal(vehicle.Direction > 0 ? -30 : 30, offset, 3);
        }
    }
}
