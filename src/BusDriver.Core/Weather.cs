namespace BusDriver.Core;

public enum WeatherKind
{
    Clear,
    Overcast,
    Rain,
    Fog,
    Snow
}

/// <summary>
/// A weather state and how much of it there is.  Grip feeds the bus physics,
/// speed factor caps the bus and visibility thickens the windscreen overlay.
/// </summary>
public readonly record struct WeatherState(WeatherKind Kind, float Intensity)
{
    public string Label => Kind switch
    {
        WeatherKind.Clear => "Clear",
        WeatherKind.Overcast => "Overcast",
        WeatherKind.Rain => "Rain",
        WeatherKind.Fog => "Fog",
        WeatherKind.Snow => "Snow",
        _ => "?"
    };

    public float Grip => Kind switch
    {
        WeatherKind.Clear => 1.00f,
        WeatherKind.Overcast => 0.95f,
        WeatherKind.Rain => 0.78f,
        WeatherKind.Fog => 0.86f,
        WeatherKind.Snow => 0.60f,
        _ => 1f
    };

    public float SpeedFactor => Kind switch
    {
        WeatherKind.Clear => 1.00f,
        WeatherKind.Overcast => 0.98f,
        WeatherKind.Rain => 0.90f,
        WeatherKind.Fog => 0.80f,
        WeatherKind.Snow => 0.72f,
        _ => 1f
    };

    public float Visibility => Kind switch
    {
        WeatherKind.Clear => 1.00f,
        WeatherKind.Overcast => 0.94f,
        WeatherKind.Rain => 0.76f,
        WeatherKind.Fog => 0.46f,
        WeatherKind.Snow => 0.58f,
        _ => 1f
    };
}

/// <summary>
/// Weather that rolls in and out on its own.  The sequence is seeded so a
/// route attempt can be replayed exactly, while the severity setting makes the
/// harder shifts wetter, foggier and snowier.
/// </summary>
public sealed class WeatherSystem
{
    private readonly Random _rng;
    private readonly float _severity;

    public WeatherSystem(int seed, float severity)
    {
        _rng = new Random(seed);
        _severity = Math.Clamp(severity, 0.1f, 2f);
        Current = new WeatherState(WeatherKind.Clear, 0f);
        RemainingSeconds = 75;
    }

    public WeatherState Current { get; private set; }

    public double RemainingSeconds { get; private set; }

    public double ElapsedSeconds { get; private set; }

    public void Update(float dt)
    {
        if (dt < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dt));
        }

        ElapsedSeconds += dt;
        RemainingSeconds -= dt;
        if (RemainingSeconds > 0)
        {
            return;
        }

        Current = NextState();
        RemainingSeconds = 55 + _rng.Next(0, 95);
    }

    /// <summary>Used by tests and by the route intro so a run can open in rain.</summary>
    public void Force(WeatherKind kind, float intensity = 1f, double seconds = 90)
    {
        Current = new WeatherState(kind, Math.Clamp(intensity, 0f, 1f));
        RemainingSeconds = Math.Max(1, seconds);
    }

    private WeatherState NextState()
    {
        // Bad weather is weighted up on the harder shifts.
        var weights = new (WeatherKind Kind, double Weight)[]
        {
            (WeatherKind.Clear, 3.2 / _severity),
            (WeatherKind.Overcast, 2.6),
            (WeatherKind.Rain, 1.4 * _severity),
            (WeatherKind.Fog, 0.9 * _severity),
            (WeatherKind.Snow, 0.7 * _severity * _severity)
        };

        var total = weights.Sum(w => w.Weight);
        var pick = _rng.NextDouble() * total;
        foreach (var (kind, weight) in weights)
        {
            pick -= weight;
            if (pick <= 0)
            {
                return new WeatherState(kind, (float)(0.65 + _rng.NextDouble() * 0.35));
            }
        }

        return new WeatherState(WeatherKind.Clear, 1f);
    }
}
