namespace BusDriver.Core;

/// <summary>
/// A passenger waiting for, or riding on, the bus.  The destination index is
/// relative to the current direction of travel and is not stored: the stop id
/// is enough, and it keeps a passenger valid when the bus turns round.
/// </summary>
public sealed record Passenger(
    int Id,
    string Name,
    string OriginStopId,
    string DestinationStopId,
    int Hops,
    int FarePence);

public static class PassengerFactory
{
    private static readonly string[] Names =
    {
        "Mrs Bramble", "Tom", "Aisha", "Mr Pemberton", "Chloe", "Sanjay",
        "The Vicar", "Nana Rose", "Danny", "Priya", "Old Mr Grimes", "Kez",
        "Miss Okafor", "Ben", "Ruby", "Dr Shah", "Trev", "Ellie",
        "Postman Pat", "Wendy", "Callum", "Maya", "Grandad Joe", "Sophie"
    };

    public static int FareFor(int hops) => 150 + Math.Max(0, hops - 1) * 40;

    public static Passenger Create(
        Random rng,
        Route route,
        int originRouteIndex,
        int direction,
        int id)
    {
        if (direction is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        var first = direction > 0 ? originRouteIndex + 1 : originRouteIndex - 1;
        var last = direction > 0 ? route.Stops.Count - 1 : 0;
        if (direction > 0 ? first > last : first < last)
        {
            throw new ArgumentException("Stop has no destinations ahead in this direction.");
        }

        var destination = rng.Next(Math.Min(first, last), Math.Max(first, last) + 1);
        var hops = Math.Abs(destination - originRouteIndex);
        return new Passenger(
            id,
            Names[rng.Next(Names.Length)],
            route.Stops[originRouteIndex].Id,
            route.Stops[destination].Id,
            hops,
            FareFor(hops));
    }

    public static List<Passenger> QueueForStop(
        Random rng,
        Route route,
        int originRouteIndex,
        int direction,
        int count,
        ref int nextId)
    {
        var queue = new List<Passenger>(count);
        for (var i = 0; i < count; i++)
        {
            queue.Add(Create(rng, route, originRouteIndex, direction, nextId++));
        }

        return queue;
    }
}
