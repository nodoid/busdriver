using System.Numerics;

namespace BusDriver.Core;

public sealed record ScheduledStop(
    int RouteIndex,
    BusStop Stop,
    TimeSpan Due,
    double CumulativeDistance);

/// <summary>
/// One trip in one direction, with a due time at every stop.  The same class
/// is used for the outbound and return legs, so a session only has to flip the
/// direction to build the next duty.
/// </summary>
public sealed class TimetableTrip
{
    private readonly Dictionary<int, ScheduledStop> _byRouteIndex;

    public TimetableTrip(
        Route route,
        int direction,
        TimeSpan startTime,
        IReadOnlyList<ScheduledStop> stops)
    {
        Route = route;
        Direction = direction;
        StartTime = startTime;
        Stops = stops;
        _byRouteIndex = stops.ToDictionary(s => s.RouteIndex);
    }

    public Route Route { get; }

    /// <summary>+1 when travelling from the first stop to the last, -1 the other way.</summary>
    public int Direction { get; }

    public TimeSpan StartTime { get; }

    /// <summary>The stops in the order they will be served.</summary>
    public IReadOnlyList<ScheduledStop> Stops { get; }

    public TimeSpan Duration => Stops[^1].Due - StartTime;

    public int FinalRouteIndex => Direction > 0 ? Route.Stops.Count - 1 : 0;

    public ScheduledStop this[int travelIndex] => Stops[travelIndex];

    public ScheduledStop AtRouteIndex(int routeIndex) => _byRouteIndex[routeIndex];

    public bool IsFinalStop(int routeIndex) => routeIndex == FinalRouteIndex;

    public double Distance => Stops[^1].CumulativeDistance;
}

public static class Timetable
{
    /// <summary>Average speed the timetable assumes, in world pixels per second.</summary>
    public const float PlannedSpeed = 150f;

    /// <summary>Minutes allowed at each intermediate stop.</summary>
    public const double StopAllowanceSeconds = 10;

    public static TimetableTrip Build(
        Route route,
        int direction,
        TimeSpan startTime,
        float plannedSpeed = PlannedSpeed)
    {
        if (direction is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be +1 or -1");
        }

        if (plannedSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannedSpeed));
        }

        var indices = direction > 0
            ? Enumerable.Range(0, route.Stops.Count).ToArray()
            : Enumerable.Range(0, route.Stops.Count).Reverse().ToArray();

        var scheduled = new List<ScheduledStop>(indices.Length);
        var due = startTime;
        var distance = 0.0;

        for (var k = 0; k < indices.Length; k++)
        {
            var routeIndex = indices[k];
            var stop = route.Stops[routeIndex];

            if (k > 0)
            {
                var previous = scheduled[k - 1];
                var leg = Vector2.Distance(previous.Stop.Position, stop.Position);
                distance += leg;
                var seconds = Math.Max(5.0, leg / plannedSpeed);
                if (k > 1)
                {
                    seconds += StopAllowanceSeconds;
                }

                due += TimeSpan.FromSeconds(seconds);
            }

            scheduled.Add(new ScheduledStop(routeIndex, stop, due, distance));
        }

        return new TimetableTrip(route, direction, startTime, scheduled);
    }
}
