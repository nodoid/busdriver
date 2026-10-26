using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class FleetTests
{
    [Fact]
    public void Three_bus_types_are_offered()
    {
        Assert.Equal(
            new[] { BusType.Minibus, BusType.SingleDecker, BusType.DoubleDecker },
            BusTypeProfile.All.Select(b => b.Type));
    }

    [Fact]
    public void Capacity_grows_with_the_bus()
    {
        Assert.True(BusTypeProfile.Minibus.Capacity < BusTypeProfile.SingleDecker.Capacity);
        Assert.True(BusTypeProfile.SingleDecker.Capacity < BusTypeProfile.DoubleDecker.Capacity);
        Assert.Equal(16, BusTypeProfile.Minibus.Capacity);
        Assert.Equal(32, BusTypeProfile.SingleDecker.Capacity);
        Assert.Equal(56, BusTypeProfile.DoubleDecker.Capacity);
    }

    [Fact]
    public void Bigger_buses_are_longer_and_slower()
    {
        Assert.True(BusTypeProfile.Minibus.Length < BusTypeProfile.DoubleDecker.Length);
        Assert.True(BusTypeProfile.Minibus.MaxSpeed > BusTypeProfile.DoubleDecker.MaxSpeed);
        Assert.True(BusTypeProfile.Minibus.Steering > BusTypeProfile.DoubleDecker.Steering);
    }

    [Fact]
    public void A_session_uses_the_selected_bus_capacity()
    {
        var route = RouteCatalog.Get("2");
        var minibus = new BusDriverSession(DifficultyProfile.Medium, route, 1, BusType.Minibus);
        var doubleDecker = new BusDriverSession(DifficultyProfile.Medium, route, 1, BusType.DoubleDecker);

        Assert.Equal(16, minibus.Bus.Capacity);
        Assert.Equal(56, doubleDecker.Bus.Capacity);
        Assert.Equal(BusType.Minibus, minibus.BusType);
        Assert.Equal(BusType.DoubleDecker, doubleDecker.BusType);
    }

    [Fact]
    public void Bus_physics_follows_the_selected_type()
    {
        var minibus = new Bus(BusType.Minibus);
        var doubleDecker = new Bus(BusType.DoubleDecker);

        Assert.Equal(96f, minibus.Length);
        Assert.Equal(148f, doubleDecker.Length);
        Assert.True(minibus.MaxSpeed > doubleDecker.MaxSpeed);
    }
}
