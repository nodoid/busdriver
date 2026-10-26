using System.Numerics;

namespace BusDriver.Core;

public enum VehicleKind
{
    Car,
    Taxi,
    Van,
    Lorry
}

/// <summary>
/// A vehicle on the road grid.  It follows a lane centre line and picks a new
/// direction at each junction, always keeping to the left as it should in
/// Scarthorpe.  The position is derived from the road line, so traffic can
/// never wander into a building.
/// </summary>
public sealed class TrafficVehicle
{
    public const float LaneOffset = 30f;

    public VehicleKind Kind { get; set; }

    public uint Colour { get; set; }

    /// <summary>True when travelling along a horizontal road.</summary>
    public bool Horizontal { get; set; }

    /// <summary>The centre line of the road: y for a horizontal road, x for a vertical one.</summary>
    public float Line { get; set; }

    /// <summary>+1 east/south, -1 west/north.</summary>
    public int Direction { get; set; }

    /// <summary>Distance along the road axis.</summary>
    public float Along { get; set; }

    public float Speed { get; set; }

    public float StunnedSeconds { get; set; }

    public float TurnCooldown { get; set; }

    public float CollisionCooldown { get; set; }

    public float Lane => Horizontal
        ? (Direction > 0 ? -LaneOffset : LaneOffset)
        : (Direction > 0 ? LaneOffset : -LaneOffset);

    public Vector2 Position => Horizontal
        ? new Vector2(Along, Line + Lane)
        : new Vector2(Line + Lane, Along);

    public Vector2 Forward => Horizontal
        ? new Vector2(Direction, 0)
        : new Vector2(0, Direction);

    public float Heading => Horizontal
        ? (Direction > 0 ? 0f : MathF.PI)
        : (Direction > 0 ? MathF.PI / 2f : -MathF.PI / 2f);

    public void TickCooldowns(float dt)
    {
        StunnedSeconds = MathF.Max(0, StunnedSeconds - dt);
        TurnCooldown = MathF.Max(0, TurnCooldown - dt);
        CollisionCooldown = MathF.Max(0, CollisionCooldown - dt);
    }
}

/// <summary>
/// A signal on one of the terminus junctions.  The light is red, keeping
/// traffic out of the yard, while a bus is standing in that terminus.
/// </summary>
public sealed class TerminusLight
{
    public TerminusLight(string stopId, Vector2 position)
    {
        StopId = stopId;
        Position = position;
    }

    public string StopId { get; }

    /// <summary>Centre of the junction, in world pixels.</summary>
    public Vector2 Position { get; }

    public bool Red { get; set; }
}

public sealed class TrafficSystem
{
    private const float LightRadius = 210f;
    private const float StopLineGap = 96f;

    private readonly Random _rng;
    private readonly TownMap _town;
    private readonly List<TrafficVehicle> _vehicles = new();
    private readonly List<TerminusLight> _lights = new();

    public TrafficSystem(int seed, TownMap town, int count, Vector2 keepAwayFrom)
    {
        _rng = new Random(seed);
        _town = town;
        foreach (var stop in RouteCatalog.Terminuses)
        {
            // The signal governs the junction at the depot gates, which is the
            // centre of the road corridor rather than the kerbside stand.
            var junction = new Vector2(
                TownMap.RoadCentreOf(stop.Position.X),
                TownMap.RoadCentreOf(stop.Position.Y));
            _lights.Add(new TerminusLight(stop.Id, junction));
        }

        for (var i = 0; i < count; i++)
        {
            _vehicles.Add(Spawn(keepAwayFrom));
        }
    }

    public IReadOnlyList<TrafficVehicle> Vehicles => _vehicles;

    /// <summary>One signal per terminus, all on the junction at the depot gates.</summary>
    public IReadOnlyList<TerminusLight> Lights => _lights;

    public int Count => _vehicles.Count;

    public void Update(
        float dt,
        WeatherState weather,
        Bus bus,
        float damageMultiplier,
        ICollection<SessionEvent> events)
    {
        foreach (var light in _lights)
        {
            light.Red = Vector2.Distance(bus.Position, light.Position) < LightRadius;
        }

        foreach (var vehicle in _vehicles)
        {
            vehicle.TickCooldowns(dt);
            Move(vehicle, dt, weather);
            CollideWithBus(vehicle, bus, damageMultiplier, events);
        }
    }

    private void Move(TrafficVehicle vehicle, float dt, WeatherState weather)
    {
        if (vehicle.StunnedSeconds > 0)
        {
            return;
        }

        var centres = vehicle.Horizontal ? TownMap.RoadCentresX : TownMap.RoadCentresY;
        var speed = vehicle.Speed * weather.SpeedFactor;
        var old = vehicle.Along;
        var next = old + vehicle.Direction * speed * dt;

        if (next < centres[0] - 140 || next > centres[^1] + 140)
        {
            Respawn(vehicle);
            return;
        }

        next = StopAtRedLight(vehicle, old, next);

        // A junction is just a road centre crossed on the current axis.
        if (vehicle.TurnCooldown <= 0)
        {
            var crossed = centres
                .Where(c => c > MathF.Min(old, next) && c <= MathF.Max(old, next))
                .OrderBy(c => MathF.Abs(c - old))
                .FirstOrDefault(float.NaN);

            if (!float.IsNaN(crossed))
            {
                Turn(vehicle, crossed);
                return;
            }
        }

        vehicle.Along = next;
    }

    /// <summary>Holds a vehicle at the stop line when a terminus signal is red.</summary>
    private float StopAtRedLight(TrafficVehicle vehicle, float from, float to)
    {
        foreach (var light in _lights)
        {
            if (!light.Red)
            {
                continue;
            }

            // The signal sits on a junction, so the vehicle's own line has to be
            // the one that crosses it.
            var crossing = vehicle.Horizontal ? light.Position.X : light.Position.Y;
            var onThisRoad = vehicle.Horizontal
                ? MathF.Abs(vehicle.Line - light.Position.Y) < 2f
                : MathF.Abs(vehicle.Line - light.Position.X) < 2f;

            if (!onThisRoad)
            {
                // The terminus yard is tarmac, not a lane: no traffic drives in.
                continue;
            }

            var stopLine = crossing - vehicle.Direction * StopLineGap;
            var approaches = vehicle.Direction > 0
                ? from <= stopLine && to > stopLine
                : from >= stopLine && to < stopLine;

            if (approaches)
            {
                return stopLine;
            }
        }

        return to;
    }

    private void Turn(TrafficVehicle vehicle, float junction)
    {
        var wasHorizontal = vehicle.Horizontal;
        var oldLine = vehicle.Line;
        var oldLane = vehicle.Lane;
        var along = vehicle.Along;

        // The place on the new road is the old lane's position, and the new
        // line is the junction centre of the old road.
        var newAlong = oldLine + oldLane;

        // Straight on 55%, left 25%, right 20%.
        var roll = _rng.NextDouble();
        if (roll < 0.55)
        {
            vehicle.Along = junction;
            vehicle.TurnCooldown = 1.1f;
            return;
        }

        var left = roll < 0.80;
        vehicle.Horizontal = !wasHorizontal;
        vehicle.Line = junction;
        vehicle.Along = newAlong;

        if (wasHorizontal)
        {
            // Heading east: left is north (-1), right is south (+1).
            // Heading west: left is south (+1), right is north (-1).
            vehicle.Direction = vehicle.Direction > 0
                ? (left ? -1 : 1)
                : (left ? 1 : -1);
        }
        else
        {
            // Heading south: left is east (+1), right is west (-1).
            // Heading north: left is west (-1), right is east (+1).
            vehicle.Direction = vehicle.Direction > 0
                ? (left ? 1 : -1)
                : (left ? -1 : 1);
        }

        vehicle.TurnCooldown = 1.2f;
    }

    private void CollideWithBus(
        TrafficVehicle vehicle,
        Bus bus,
        float damageMultiplier,
        ICollection<SessionEvent> events)
    {
        if (vehicle.CollisionCooldown > 0)
        {
            return;
        }

        var distance = Vector2.Distance(vehicle.Position, bus.Position);
        if (distance > 52f)
        {
            return;
        }

        vehicle.CollisionCooldown = 2.2f;
        vehicle.StunnedSeconds = 1.4f;
        bus.AddImpactDamage(5.5f * damageMultiplier);
        bus.ScrubSpeed(0.35f);
        events.Add(new SessionEvent(SessionEventKind.Crash, "Traffic collision!"));
    }

    private TrafficVehicle Spawn(Vector2 keepAwayFrom)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var horizontal = _rng.Next(2) == 0;
            var centres = horizontal ? TownMap.RoadCentresX : TownMap.RoadCentresY;
            var line = horizontal
                ? TownMap.RoadCentresY[_rng.Next(TownMap.RoadCentresY.Count)]
                : TownMap.RoadCentresX[_rng.Next(TownMap.RoadCentresX.Count)];
            var along = centres[_rng.Next(centres.Count)]
                        + (_rng.NextSingle() - 0.5f) * 300f;
            var direction = _rng.Next(2) == 0 ? 1 : -1;
            var kind = (VehicleKind)_rng.Next(4);

            var vehicle = new TrafficVehicle
            {
                Kind = kind,
                Colour = VehicleColours[_rng.Next(VehicleColours.Length)],
                Horizontal = horizontal,
                Line = line,
                Direction = direction,
                Along = along,
                Speed = kind switch
                {
                    VehicleKind.Lorry => 112f + _rng.NextSingle() * 26f,
                    VehicleKind.Van => 132f + _rng.NextSingle() * 30f,
                    VehicleKind.Taxi => 150f + _rng.NextSingle() * 34f,
                    _ => 142f + _rng.NextSingle() * 38f
                }
            };

            if (Vector2.Distance(vehicle.Position, keepAwayFrom) > 520f)
            {
                return vehicle;
            }
        }

        return new TrafficVehicle
        {
            Kind = VehicleKind.Car,
            Colour = 0xFFE53935,
            Horizontal = true,
            Line = TownMap.RoadCentresY[3],
            Direction = 1,
            Along = TownMap.RoadCentresX[5],
            Speed = 150f
        };
    }

    private void Respawn(TrafficVehicle vehicle)
    {
        var fresh = Spawn(vehicle.Position);
        vehicle.Kind = fresh.Kind;
        vehicle.Colour = fresh.Colour;
        vehicle.Horizontal = fresh.Horizontal;
        vehicle.Line = fresh.Line;
        vehicle.Direction = fresh.Direction;
        vehicle.Along = fresh.Along;
        vehicle.Speed = fresh.Speed;
        vehicle.StunnedSeconds = 0;
        vehicle.TurnCooldown = 0;
        vehicle.CollisionCooldown = 0;
    }

    private static readonly uint[] VehicleColours =
    {
        0xFFE53935, 0xFF1E88E5, 0xFF43A047, 0xFFFDD835,
        0xFFFB8C00, 0xFF8E24AA, 0xFF00ACC1, 0xFFF5F5F5,
        0xFF37474F, 0xFFC0CA33
    };
}
