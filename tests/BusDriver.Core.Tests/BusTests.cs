using System.Numerics;
using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class BusTests
{
    private static readonly TownMap Town = new();

    [Fact]
    public void Bus_accelerates_and_rolls()
    {
        var bus = new Bus(BusType.SingleDecker);
        bus.Teleport(new Vector2(TownMap.RoadCentresX[2], TownMap.RoadCentresY[2]), 0);

        for (var i = 0; i < 60; i++)
        {
            bus.Update(1 / 60f, new DriveInput(1, 0, false), Town,
                new WeatherState(WeatherKind.Clear, 1), 1);
        }

        Assert.True(bus.Speed > 20);
        Assert.True(bus.Position.X > TownMap.RoadCentresX[2]);
        Assert.Equal(0f, bus.Damage);
    }

    [Fact]
    public void A_jammed_bus_is_set_back_on_the_lane_it_was_heading_for()
    {
        var bus = new Bus(BusType.SingleDecker);
        // Nosed into the middle of a building block, pointing east.
        bus.Teleport(new Vector2(20 * TownMap.TileSize, 20 * TownMap.TileSize), 0.05f);

        bus.RecoverOnRoad();

        Assert.Equal(TileKind.Road, Town.TileAt(bus.Position));
        Assert.Equal(0f, bus.Heading, 3);
        Assert.Equal(0f, bus.Speed);
    }

    [Fact]
    public void Mounting_the_pavement_counts_as_a_crash()
    {
        var town = new TownMap();
        var bus = new Bus(BusType.SingleDecker);
        // On the horizontal road, pointing north at the kerb.
        bus.Teleport(
            new Vector2(TownMap.RoadCentresX[0] + 4 * TownMap.TileSize, TownMap.RoadCentresY[0]),
            -MathF.PI / 2f,
            60f);

        var kerbed = false;
        for (var i = 0; i < 120; i++)
        {
            bus.Update(1 / 60f, new DriveInput(1, 0, false), town,
                new WeatherState(WeatherKind.Clear, 1), 1);
            kerbed |= bus.KerbThisFrame;
        }

        Assert.True(kerbed);
        Assert.True(bus.Damage > 0);
        Assert.NotEqual(TileKind.Pavement, town.TileAt(bus.Position));
    }

    [Fact]
    public void Brakes_stop_the_bus()
    {
        var bus = new Bus(BusType.SingleDecker);
        bus.Teleport(new Vector2(TownMap.RoadCentresX[2], TownMap.RoadCentresY[2]), 0, 200);

        for (var i = 0; i < 120; i++)
        {
            bus.Update(1 / 60f, new DriveInput(0, 0, true), Town,
                new WeatherState(WeatherKind.Clear, 1), 1);
        }

        Assert.Equal(0, bus.Speed, 3);
    }

    [Fact]
    public void Snow_slows_the_bus_compared_with_clear_weather()
    {
        var clear = new Bus(BusType.SingleDecker);
        var snow = new Bus(BusType.SingleDecker);
        clear.Teleport(new Vector2(TownMap.RoadCentresX[2], TownMap.RoadCentresY[2]), 0);
        snow.Teleport(new Vector2(TownMap.RoadCentresX[2], TownMap.RoadCentresY[2]), 0);

        for (var i = 0; i < 300; i++)
        {
            clear.Update(1 / 60f, new DriveInput(1, 0, false), Town,
                new WeatherState(WeatherKind.Clear, 1), 1);
            snow.Update(1 / 60f, new DriveInput(1, 0, false), Town,
                new WeatherState(WeatherKind.Snow, 1), 1);
        }

        Assert.True(snow.Speed < clear.Speed);
    }

    [Fact]
    public void Hitting_the_edge_of_the_map_damages_the_bus()
    {
        var bus = new Bus(BusType.SingleDecker);
        bus.Teleport(new Vector2(TownMap.WorldWidth - 40, TownMap.RoadCentresY[2]), 0, 250);

        bus.Update(0.1f, new DriveInput(1, 0, false), Town,
            new WeatherState(WeatherKind.Clear, 1), 1);

        Assert.True(bus.CollisionThisFrame);
        Assert.True(bus.Damage > 0);
        Assert.True(bus.Speed <= 0);
    }

    [Fact]
    public void Damage_reduces_the_top_speed()
    {
        var fresh = new Bus(BusType.SingleDecker);
        var damaged = new Bus(BusType.SingleDecker);
        fresh.Teleport(new Vector2(TownMap.RoadCentresX[1], TownMap.RoadCentresY[3]), 0);
        damaged.Teleport(new Vector2(TownMap.RoadCentresX[1], TownMap.RoadCentresY[3]), 0);
        damaged.AddImpactDamage(80);

        for (var i = 0; i < 400; i++)
        {
            fresh.Update(1 / 60f, new DriveInput(1, 0, false), Town,
                new WeatherState(WeatherKind.Clear, 1), 1);
            damaged.Update(1 / 60f, new DriveInput(1, 0, false), Town,
                new WeatherState(WeatherKind.Clear, 1), 1);
        }

        Assert.True(damaged.Speed < fresh.Speed);
    }
}
