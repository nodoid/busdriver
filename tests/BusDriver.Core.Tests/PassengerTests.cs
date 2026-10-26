using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class PassengerTests
{
    [Fact]
    public void Outbound_passengers_only_travel_towards_later_stops()
    {
        var route = RouteCatalog.Get("3");
        var rng = new Random(7);
        var nextId = 0;
        var queue = PassengerFactory.QueueForStop(rng, route, 2, 1, 40, ref nextId);

        Assert.Equal(40, queue.Count);
        Assert.All(queue, p =>
        {
            var origin = route.Stops.ToList().FindIndex(s => s.Id == p.OriginStopId);
            var destination = route.Stops.ToList().FindIndex(s => s.Id == p.DestinationStopId);
            Assert.True(destination > origin);
        });
    }

    [Fact]
    public void Inbound_passengers_only_travel_towards_earlier_stops()
    {
        var route = RouteCatalog.Get("3");
        var rng = new Random(8);
        var nextId = 0;
        var queue = PassengerFactory.QueueForStop(rng, route, 3, -1, 40, ref nextId);

        Assert.All(queue, p =>
        {
            var origin = route.Stops.ToList().FindIndex(s => s.Id == p.OriginStopId);
            var destination = route.Stops.ToList().FindIndex(s => s.Id == p.DestinationStopId);
            Assert.True(destination < origin);
        });
    }

    [Fact]
    public void Fare_grows_with_the_number_of_stops()
    {
        Assert.Equal(150, PassengerFactory.FareFor(1));
        Assert.Equal(190, PassengerFactory.FareFor(2));
        Assert.Equal(230, PassengerFactory.FareFor(3));
        Assert.True(PassengerFactory.FareFor(6) > PassengerFactory.FareFor(2));
    }

    [Fact]
    public void Queues_are_deterministic_for_a_seed()
    {
        var route = RouteCatalog.Get("5");
        var aId = 0;
        var bId = 0;
        var a = PassengerFactory.QueueForStop(new Random(99), route, 1, 1, 10, ref aId);
        var b = PassengerFactory.QueueForStop(new Random(99), route, 1, 1, 10, ref bId);

        Assert.Equal(a.Select(p => p.Name), b.Select(p => p.Name));
        Assert.Equal(a.Select(p => p.DestinationStopId), b.Select(p => p.DestinationStopId));
    }

    [Fact]
    public void A_stop_with_nowhere_to_go_is_rejected()
    {
        var route = RouteCatalog.Get("2");
        Assert.Throws<ArgumentException>(() =>
            PassengerFactory.Create(new Random(1), route, route.Stops.Count - 1, 1, 0));
    }
}
