namespace BusDriver.Core;

/// <summary>The three shift difficulties offered on the duty board.</summary>
public enum DifficultyLevel
{
    Easy,
    Medium,
    Hard
}

/// <summary>How much help the driver gets finding the way.</summary>
public enum GuidanceLevel
{
    /// <summary>Announce every junction, including going straight on.</summary>
    TurnByTurn,

    /// <summary>Only announce junctions where the route actually turns.</summary>
    TurnsOnly,

    /// <summary>Read the route map and remember it.</summary>
    None
}

/// <summary>
/// Every number that makes one difficulty different from another.  The shift
/// length, timetable tolerance, traffic volume, passenger demand and bus size
/// all move together so the three levels feel like three different jobs.
/// </summary>
public sealed record DifficultyProfile(
    DifficultyLevel Level,
    string Name,
    string Blurb,
    int ShiftMinutes,
    int ScheduleToleranceSeconds,
    int TrafficVehicles,
    int PassengerBase,
    int PassengerSpread,
    float WeatherSeverity,
    float DamageMultiplier,
    GuidanceLevel Guidance)
{
    public static readonly DifficultyProfile Easy = new(
        DifficultyLevel.Easy,
        "Easy",
        "A quiet Sunday service.  Long shift, forgiving timetable, little traffic.",
        ShiftMinutes: 12,
        ScheduleToleranceSeconds: 75,
        TrafficVehicles: 4,
        PassengerBase: 2,
        PassengerSpread: 3,
        WeatherSeverity: 0.5f,
        DamageMultiplier: 0.6f,
        Guidance: GuidanceLevel.TurnByTurn);

    public static readonly DifficultyProfile Medium = new(
        DifficultyLevel.Medium,
        "Medium",
        "The normal school-run rush.  A working timetable and proper traffic.",
        ShiftMinutes: 10,
        ScheduleToleranceSeconds: 45,
        TrafficVehicles: 8,
        PassengerBase: 3,
        PassengerSpread: 3,
        WeatherSeverity: 1.0f,
        DamageMultiplier: 1.0f,
        Guidance: GuidanceLevel.TurnsOnly);

    public static readonly DifficultyProfile Hard = new(
        DifficultyLevel.Hard,
        "Hard",
        "Friday night in Scarthorpe.  Tight timing, bad weather and a small bus.",
        ShiftMinutes: 8,
        ScheduleToleranceSeconds: 25,
        TrafficVehicles: 14,
        PassengerBase: 4,
        PassengerSpread: 4,
        WeatherSeverity: 1.45f,
        DamageMultiplier: 1.4f,
        Guidance: GuidanceLevel.None);

    public static IReadOnlyList<DifficultyProfile> All { get; } = new[] { Easy, Medium, Hard };

    public static DifficultyProfile For(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => Easy,
        DifficultyLevel.Medium => Medium,
        DifficultyLevel.Hard => Hard,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown difficulty")
    };
}
