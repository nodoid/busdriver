using System.Numerics;

namespace BusDriver.Core;

public enum TurnDirection
{
    StraightOn,
    Left,
    Right,
    TurnAround
}

/// <summary>
/// The next turn the driver has to make.  Guidance is worked out from the bus
/// and the next stop rather than from a pre-baked path, so it still points the
/// right way after the bus has been shoved back onto the road.
/// </summary>
public sealed record TurnHint(
    TurnDirection Direction,
    float MetresToJunction,
    string Road)
{
    public static readonly TurnHint None = new(TurnDirection.StraightOn, 0f, string.Empty);
}

public static class RouteGuidance
{
    /// <summary>World pixels per metre, from the speed scale (355 px/s ~ 37 mph).</summary>
    public const float MetresPerPixel = 0.0465f;

    /// <summary>
    /// Works out the next turn towards <paramref name="target"/>.  Only the
    /// junction immediately ahead is considered: the player is expected to
    /// look at the route map for the rest.
    /// </summary>
    public static TurnHint Hint(Vector2 position, float heading, Vector2 target)
    {
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var delta = target - position;
        if (delta.LengthSquared() < 1f)
        {
            return TurnHint.None;
        }

        var horizontal = MathF.Abs(forward.X) >= MathF.Abs(forward.Y);
        var distance = JunctionAhead(position, forward, horizontal);

        // Which way the bus needs to be pointing when it leaves the junction.
        var targetHorizontal = MathF.Abs(delta.X) > MathF.Abs(delta.Y);
        TurnDirection direction;
        if (targetHorizontal == horizontal)
        {
            var along = horizontal ? MathF.Sign(delta.X) : MathF.Sign(delta.Y);
            var travel = horizontal ? MathF.Sign(forward.X) : MathF.Sign(forward.Y);
            direction = along == travel ? TurnDirection.StraightOn : TurnDirection.TurnAround;
        }
        else
        {
            var cross = forward.X * delta.Y - forward.Y * delta.X;
            direction = cross > 0f ? TurnDirection.Right : TurnDirection.Left;
        }

        return new TurnHint(direction, distance * MetresPerPixel, string.Empty);
    }

    /// <summary>Distance in world pixels to the next road centre along the bus's axis.</summary>
    private static float JunctionAhead(Vector2 position, Vector2 forward, bool horizontal)
    {
        var centres = horizontal ? TownMap.RoadCentresX : TownMap.RoadCentresY;
        var along = horizontal ? position.X : position.Y;
        float sign = horizontal ? MathF.Sign(forward.X) : MathF.Sign(forward.Y);
        if (sign == 0f)
        {
            sign = 1f;
        }

        var best = float.MaxValue;
        foreach (var centre in centres)
        {
            var gap = (centre - along) * sign;
            if (gap > 0.5f && gap < best)
            {
                best = gap;
            }
        }

        return best == float.MaxValue ? 0f : best;
    }
}
