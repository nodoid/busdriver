using System.Numerics;

namespace BusDriver.Core;

public sealed record ServiceResult(
    int Alighted,
    int Boarded,
    int LeftWaiting,
    bool OnTime,
    double LatenessSeconds);

/// <summary>
/// One driver's shift.  The bus is driven by the player; everything else - the
/// timetable, waiting passengers, traffic and weather - happens here in the
/// platform-independent core so it can be tested without a graphics device.
/// </summary>
public sealed class BusDriverSession
{
    public const float StopRadius = 125f;
    public const float DoorSpeedLimit = 30f;
    public const double LayoverSeconds = 32;

    private readonly List<SessionEvent> _events = new();
    private readonly Dictionary<string, List<Passenger>> _waiting = new(StringComparer.Ordinal);
    private readonly Random _rng;
    private readonly double _shiftSeconds;
    private double _elapsed;
    private double _layover;
    private float _jammedSeconds;
    private int _nextPassengerId;
    private int _nextRouteIndex;

    public BusDriverSession(
        DifficultyProfile difficulty,
        Route route,
        int seed = 1985,
        BusType busType = BusType.SingleDecker,
        TimeSpan? shiftStart = null)
    {
        Difficulty = difficulty;
        Route = route;
        BusType = busType;
        _rng = new Random(seed);
        Seed = seed;

        Town = new TownMap(seed);
        Bus = new Bus(busType);
        Bus.PlaceAt(route.FirstStop, route.Stops[1]);
        Traffic = new TrafficSystem(seed + 17, Town, difficulty.TrafficVehicles, Bus.Position);
        Weather = new WeatherSystem(seed + 31, difficulty.WeatherSeverity);

        StartTime = shiftStart ?? new TimeSpan(8, 0, 0);
        ShiftEnds = StartTime + TimeSpan.FromMinutes(difficulty.ShiftMinutes);
        _shiftSeconds = difficulty.ShiftMinutes * 60;

        StartTrip(1, StartTime);
    }

    public DifficultyProfile Difficulty { get; }

    public Route Route { get; }

    public BusType BusType { get; }

    public int Seed { get; }

    public TownMap Town { get; }

    public Bus Bus { get; }

    public TrafficSystem Traffic { get; }

    public WeatherSystem Weather { get; }

    public TimeSpan StartTime { get; }

    public TimeSpan ShiftEnds { get; }

    public TimetableTrip? Trip { get; private set; }

    public int Direction { get; private set; }

    public bool IsFinished { get; private set; }

    public string FinishReason { get; private set; } = string.Empty;

    public ScoreBreakdown Score { get; } = new();

    public ServiceResult? LastService { get; private set; }

    public bool IsAwaitingLayover => _layover > 0;

    public double LayoverRemaining => Math.Max(0, _layover);

    public TimeSpan Now => StartTime + TimeSpan.FromSeconds(_elapsed);

    public TimeSpan ShiftRemaining =>
        ShiftEnds > Now ? ShiftEnds - Now : TimeSpan.Zero;

    public IReadOnlyList<SessionEvent> Events => _events;

    public int WaitingAt(string stopId) =>
        _waiting.TryGetValue(stopId, out var queue) ? queue.Count : 0;

    public BusStop? NextStop =>
        Trip is null || IsAwaitingLayover ? null : Trip.AtRouteIndex(_nextRouteIndex).Stop;

    public ScheduledStop? NextScheduled =>
        Trip is null || IsAwaitingLayover ? null : Trip.AtRouteIndex(_nextRouteIndex);

    public int NextRouteIndex => _nextRouteIndex;

    public float NextStopDistance => NextStop is null
        ? 0
        : Vector2.Distance(Bus.Position, NextStop.Position);

    public float BearingToNextStop
    {
        get
        {
            if (NextStop is null)
            {
                return 0;
            }

            var delta = NextStop.Position - Bus.Position;
            return MathF.Atan2(delta.Y, delta.X) - Bus.Heading;
        }
    }

    public bool IsInStopZone => NextStop is not null && NextStopDistance <= StopRadius;

    public bool CanOpenDoors => IsInStopZone && MathF.Abs(Bus.Speed) <= DoorSpeedLimit;

    public double DelaySeconds => NextScheduled is null
        ? 0
        : (Now - NextScheduled.Due).TotalSeconds;

    public string StatusText
    {
        get
        {
            if (IsFinished)
            {
                return FinishReason;
            }

            if (IsAwaitingLayover)
            {
                return $"Layover at {Route.LastStop.Name} - {Math.Ceiling(LayoverRemaining):0}s";
            }

            if (NextStop is null)
            {
                return string.Empty;
            }

            if (!IsInStopZone)
            {
                return $"Next: {NextStop.Name}";
            }

            if (!CanOpenDoors)
            {
                return $"Slow down at {NextStop.Name}";
            }

            return $"Stopped at {NextStop.Name} - open the doors";
        }
    }

    public void Update(float dt, DriveInput input, bool doorsPressed)
    {
        if (IsFinished || dt < 0)
        {
            return;
        }

        var worldDt = MathF.Min(dt, 0.1f);
        _elapsed += dt;

        if (_elapsed >= _shiftSeconds)
        {
            _elapsed = _shiftSeconds;
            Finish("End of shift");
            return;
        }

        Weather.Update(worldDt);
        Traffic.Update(worldDt, Weather.Current, Bus, Difficulty.DamageMultiplier, _events);
        Bus.Update(worldDt, input, Town, Weather.Current, Difficulty.DamageMultiplier);

        if (Bus.CollisionThisFrame)
        {
            _events.Add(new SessionEvent(
                SessionEventKind.Crash,
                Bus.KerbThisFrame ? "Mounted the pavement!" : "Hit a wall!"));
        }

        // A missed turn that leaves the bus nosed into a wall would strand the
        // shift, so after a moment of being stuck it is set back on the lane,
        // still pointing the way the driver was heading.
        var stuck = MathF.Abs(Bus.Speed) < 12f &&
                    (Bus.CollisionThisFrame || Town.IsSolidAt(Bus.Position));
        _jammedSeconds = stuck ? _jammedSeconds + worldDt : 0f;
        if (_jammedSeconds > 1.1f)
        {
            Bus.RecoverOnRoad();
            _jammedSeconds = 0f;
            _events.Add(new SessionEvent(SessionEventKind.Recovered, "Back on the road"));
        }

        if (Bus.Damage >= 100f)
        {
            Finish("Bus written off");
            return;
        }

        if (doorsPressed)
        {
            AttemptService();
        }

        if (IsAwaitingLayover)
        {
            _layover -= dt;
            if (_layover <= 0)
            {
                _layover = 0;
                StartTrip(-Direction, Now);
            }
        }
    }

    /// <summary>Teleport helper used by tests and by the "recover" option.</summary>
    public void TeleportToStop(int routeIndex)
    {
        var stop = Route.Stops[routeIndex];
        Bus.Teleport(stop.Position, 0f, 0f);
        if (NextStop is not null)
        {
            Bus.AimAt(NextStop.Position);
        }
    }

    public IReadOnlyList<SessionEvent> DrainEvents()
    {
        if (_events.Count == 0)
        {
            return Array.Empty<SessionEvent>();
        }

        var copy = _events.ToArray();
        _events.Clear();
        return copy;
    }

    private void AttemptService()
    {
        if (IsAwaitingLayover || Trip is null || NextStop is null)
        {
            _events.Add(new SessionEvent(SessionEventKind.DoorsRefused, "No stop due"));
            return;
        }

        if (!IsInStopZone)
        {
            _events.Add(new SessionEvent(SessionEventKind.DoorsRefused, "Not at a stop"));
            return;
        }

        if (!CanOpenDoors)
        {
            _events.Add(new SessionEvent(SessionEventKind.DoorsRefused, "Stop the bus first"));
            return;
        }

        var stop = NextStop;
        var scheduled = Trip.AtRouteIndex(_nextRouteIndex);
        _events.Add(new SessionEvent(SessionEventKind.DoorsOpened, "Doors"));

        // Alighting first: everyone whose destination is this stop gets off.
        var alighted = Bus.Passengers.RemoveAll(p => p.DestinationStopId == stop.Id);
        if (stop.IsTerminus)
        {
            // A terminus is an alighting-only stop: nobody boards, and anyone
            // still aboard has reached the end of the line.
            alighted += Bus.Passengers.Count;
            Bus.Passengers.Clear();
        }

        Score.PassengersDelivered += alighted;

        var boarded = 0;
        var leftWaiting = 0;
        if (!stop.IsTerminus && _waiting.TryGetValue(stop.Id, out var queue))
        {
            while (queue.Count > 0 && Bus.FreeSeats > 0)
            {
                var passenger = queue[0];
                queue.RemoveAt(0);
                Bus.Passengers.Add(passenger);
                Score.FaresPence += passenger.FarePence;
                Score.PassengersCarried++;
                boarded++;
            }

            leftWaiting = queue.Count;
        }

        _events.Add(new SessionEvent(
            SessionEventKind.StopServed,
            $"{stop.Name}: -{alighted} +{boarded}"));

        var late = (Now - scheduled.Due).TotalSeconds;
        var onTime = late <= Difficulty.ScheduleToleranceSeconds;
        if (onTime)
        {
            Score.OnTimeStops++;
            Score.PunctualityPoints += 30;
            _events.Add(new SessionEvent(
                SessionEventKind.OnTime,
                late <= 5 ? "Right on time" : $"On time ({late:0}s late)"));
        }
        else
        {
            var penalty = (int)Math.Round((late - Difficulty.ScheduleToleranceSeconds) * 1.5);
            Score.LateStops++;
            Score.PunctualityPoints -= penalty;
            _events.Add(new SessionEvent(
                SessionEventKind.Late,
                $"Late by {late:0}s",
                penalty));
        }

        LastService = new ServiceResult(alighted, boarded, leftWaiting, onTime, late);

        if (Trip.IsFinalStop(_nextRouteIndex))
        {
            CompleteTrip();
        }
        else
        {
            _nextRouteIndex += Direction;
            // The bus keeps the heading the driver left it on: opening the
            // doors must not spin it round towards the next stop.
        }
    }

    private void CompleteTrip()
    {
        Score.TripBonus += 150;
        Score.TripsCompleted++;
        _events.Add(new SessionEvent(
            SessionEventKind.TripComplete,
            $"Trip complete at {Route.Stops[Direction > 0 ? Route.Stops.Count - 1 : 0].Name}"));
        _layover = LayoverSeconds;
        _events.Add(new SessionEvent(
            SessionEventKind.Layover,
            $"{LayoverSeconds:0}s layover"));
    }

    private void StartTrip(int direction, TimeSpan startTime)
    {
        Direction = direction;
        Trip = Timetable.Build(Route, direction, startTime);
        _nextRouteIndex = direction > 0 ? 1 : Route.Stops.Count - 2;

        _waiting.Clear();
        var order = direction > 0
            ? Enumerable.Range(0, Route.Stops.Count).ToArray()
            : Enumerable.Range(0, Route.Stops.Count).Reverse().ToArray();

        // The first stop is the terminus the bus is leaving, and the last is a
        // terminus, so neither takes boarding passengers.  Everyone else gets a
        // fresh queue with destinations ahead in this direction.
        for (var k = 1; k < order.Length; k++)
        {
            var index = order[k];
            var stop = Route.Stops[index];
            if (stop.IsTerminus)
            {
                _waiting[stop.Id] = new List<Passenger>();
                continue;
            }

            var count = Difficulty.PassengerBase + _rng.Next(Difficulty.PassengerSpread + 1);
            _waiting[stop.Id] = PassengerFactory.QueueForStop(
                _rng,
                Route,
                index,
                direction,
                count,
                ref _nextPassengerId);
        }

    }

    private void Finish(string reason)
    {
        if (IsFinished)
        {
            return;
        }

        IsFinished = true;
        FinishReason = reason;
        Score.DamagePenalty = (int)MathF.Round(Bus.Damage * 12f);
        _events.Add(new SessionEvent(SessionEventKind.ShiftEnd, reason));
    }
}
