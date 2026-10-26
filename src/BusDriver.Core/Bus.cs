using System.Numerics;

namespace BusDriver.Core;

public readonly record struct DriveInput(float Throttle, float Steering, bool Brake)
{
    public static DriveInput None => new(0f, 0f, false);
}

/// <summary>
/// A bus from the depot fleet: a simple kinematic vehicle with grip, braking
/// and a body that can be dented.  Minibus, single-decker and double-decker
/// differ in capacity, length, top speed and steering.
/// </summary>
public sealed class Bus
{
    public const float MaxForwardSpeed = 355f;
    public const float MaxReverseSpeed = 105f;

    public Bus(BusType busType)
    {
        Profile = BusTypeProfile.For(busType);
        Capacity = Profile.Capacity;
        Length = Profile.Length;
        Width = Profile.Width;
        MaxSpeed = Profile.MaxSpeed;
    }

    public BusTypeProfile Profile { get; }

    public BusType Type => Profile.Type;

    public int Capacity { get; }

    public float Length { get; }

    public float Width { get; }

    public float MaxSpeed { get; }

    public Vector2 Position { get; private set; }

    public float Heading { get; private set; }

    public float Speed { get; private set; }

    /// <summary>0 to 100.  At 100 the bus is written off and the shift ends.</summary>
    public float Damage { get; private set; }

    public List<Passenger> Passengers { get; } = new();

    public int PassengerCount => Passengers.Count;

    public int FreeSeats => Math.Max(0, Capacity - Passengers.Count);

    public bool CollisionThisFrame { get; private set; }

    /// <summary>True when this frame's collision was a kerb, not a wall.</summary>
    public bool KerbThisFrame { get; private set; }

    public double DistanceTravelled { get; private set; }

    public float SpeedMph => MathF.Abs(Speed) * 0.105f;

    public void PlaceAt(BusStop stop, BusStop? aimAt = null)
    {
        Position = stop.Position;
        Speed = 0;
        if (aimAt is not null)
        {
            AimAt(aimAt.Position);
        }
    }

    public void Teleport(Vector2 position, float heading = 0f, float speed = 0f)
    {
        Position = position;
        Heading = heading;
        Speed = speed;
    }

    public void AimAt(Vector2 target)
    {
        var delta = target - Position;
        if (delta.LengthSquared() > 0.01f)
        {
            Heading = MathF.Atan2(delta.Y, delta.X);
        }
    }

    public void Update(float dt, DriveInput input, TownMap town, WeatherState weather, float damageMultiplier)
    {
        if (dt <= 0)
        {
            return;
        }

        CollisionThisFrame = false;
        KerbThisFrame = false;
        var surface = town.SurfaceFactorAt(Position);
        var grip = weather.Grip * (0.72f + 0.28f * surface);
        var condition = 1f - Damage / 260f;
        var maxForward = MaxSpeed * weather.SpeedFactor * condition * (0.55f + 0.45f * surface);
        var maxReverse = MaxReverseSpeed * weather.SpeedFactor * condition;

        var throttle = Math.Clamp(input.Throttle, -1f, 1f);
        var steering = Math.Clamp(input.Steering, -1f, 1f);

        if (throttle > 0)
        {
            Speed += Profile.Acceleration * grip * throttle * dt;
        }
        else if (throttle < 0)
        {
            Speed += 112f * grip * throttle * dt;
        }

        if (input.Brake)
        {
            var drop = 310f * grip * dt;
            if (MathF.Abs(Speed) <= drop)
            {
                Speed = 0;
            }
            else
            {
                Speed -= MathF.Sign(Speed) * drop;
            }
        }

        // Rolling resistance and engine braking.
        var drag = (16f + MathF.Abs(Speed) * 0.22f) * (2f - surface) * dt;
        if (MathF.Abs(Speed) <= drag)
        {
            Speed = 0;
        }
        else
        {
            Speed -= MathF.Sign(Speed) * drag;
        }

        if (Speed > maxForward)
        {
            Speed = MathF.Max(maxForward, Speed - 240f * dt);
        }

        if (Speed < -maxReverse)
        {
            Speed = MathF.Min(-maxReverse, Speed + 240f * dt);
        }

        if (MathF.Abs(Speed) > 4f)
        {
            var direction = MathF.Sign(Speed);
            var steeringAuthority = Math.Clamp(MathF.Abs(Speed) / 120f, 0.15f, 1f);
            Heading += steering * Profile.Steering * steeringAuthority * grip * direction * dt;
        }

        var step = new Vector2(MathF.Cos(Heading), MathF.Sin(Heading)) * Speed * dt;
        var next = Position + step;

        var obstruction = ObstructionAt(town, next, Heading);
        if (obstruction != Obstruction.None)
        {
            // A bus that is already at rest against the kerb simply stays put;
            // only a moving bus takes the hit.
            if (MathF.Abs(Speed) < 10f)
            {
                Speed = 0f;
                return;
            }

            CollisionThisFrame = true;
            KerbThisFrame = obstruction == Obstruction.Kerb;
            var impact = MathF.Abs(Speed) * (KerbThisFrame ? 0.55f : 1f);
            Damage = Math.Clamp(Damage + MathF.Max(1.2f, impact * 0.055f) * damageMultiplier, 0f, 100f);
            Speed = -Speed * 0.14f;
            return;
        }

        Position = next;
        DistanceTravelled += step.Length();
    }

    /// <summary>
    /// Lifts a jammed bus back onto the nearest lane, still pointing the way the
    /// driver was heading.  Used when a missed turn leaves the bus nosed into
    /// a wall with nowhere to go.
    /// </summary>
    public void RecoverOnRoad()
    {
        var forward = new Vector2(MathF.Cos(Heading), MathF.Sin(Heading));
        if (MathF.Abs(forward.X) >= MathF.Abs(forward.Y))
        {
            var direction = forward.X >= 0f ? 1 : -1;
            var centre = NearestCentre(TownMap.RoadCentresY, Position.Y);
            Position = new Vector2(
                Math.Clamp(Position.X, TownMap.TileSize, TownMap.WorldWidth - TownMap.TileSize),
                centre + (direction > 0 ? -TrafficVehicle.LaneOffset : TrafficVehicle.LaneOffset));
            Heading = direction > 0 ? 0f : MathF.PI;
        }
        else
        {
            var direction = forward.Y >= 0f ? 1 : -1;
            var centre = NearestCentre(TownMap.RoadCentresX, Position.X);
            Position = new Vector2(
                centre + (direction > 0 ? TrafficVehicle.LaneOffset : -TrafficVehicle.LaneOffset),
                Math.Clamp(Position.Y, TownMap.TileSize, TownMap.WorldHeight - TownMap.TileSize));
            Heading = direction > 0 ? MathF.PI / 2f : -MathF.PI / 2f;
        }

        Speed = 0f;
        CollisionThisFrame = false;
    }

    private static float NearestCentre(IReadOnlyList<float> centres, float value)
    {
        var best = centres[0];
        foreach (var centre in centres)
        {
            if (MathF.Abs(centre - value) < MathF.Abs(best - value))
            {
                best = centre;
            }
        }

        return best;
    }

    public void AddImpactDamage(float amount)
    {
        Damage = Math.Clamp(Damage + amount, 0f, 100f);
    }

    public void ScrubSpeed(float factor)
    {
        Speed *= Math.Clamp(factor, 0f, 1f);
    }

    private enum Obstruction
    {
        None,
        Wall,
        Kerb
    }

    /// <summary>
    /// What, if anything, the bus would hit at <paramref name="position"/>.
    /// Walls stop it dead; pavements are kerbs, which are also crashes - a bus
    /// belongs on the tarmac, not on the footway.
    /// </summary>
    private Obstruction ObstructionAt(TownMap town, Vector2 position, float heading)
    {
        Span<Vector2> probes = stackalloc Vector2[5];
        BuildProbes(position, heading, probes);
        foreach (var probe in probes)
        {
            var tile = town.TileAt(probe);
            if (town.IsSolid(tile))
            {
                return Obstruction.Wall;
            }

            if (tile == TileKind.Pavement)
            {
                return Obstruction.Kerb;
            }
        }

        if (position.X < TownMap.TileSize || position.Y < TownMap.TileSize ||
            position.X > TownMap.WorldWidth - TownMap.TileSize ||
            position.Y > TownMap.WorldHeight - TownMap.TileSize)
        {
            return Obstruction.Wall;
        }

        return Obstruction.None;
    }

    /// <summary>
    /// The five points of the body that are tested against the map: nose,
    /// centre, tail and the two rear quarters.  They follow the body, so a
    /// long double-decker cannot clip a kerb that a minibus clears.
    /// </summary>
    private void BuildProbes(Vector2 position, float heading, Span<Vector2> probes)
    {
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var side = new Vector2(-forward.Y, forward.X);
        var halfWidth = Width * 0.36f;

        probes[0] = position + forward * (Length * 0.34f);
        probes[1] = position + forward * (Length * 0.08f);
        probes[2] = position - forward * (Length * 0.34f);
        probes[3] = position - forward * (Length * 0.20f) + side * halfWidth;
        probes[4] = position - forward * (Length * 0.20f) - side * halfWidth;
    }
}
