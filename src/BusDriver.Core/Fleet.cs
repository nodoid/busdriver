namespace BusDriver.Core;

public enum BusType
{
    Minibus,
    SingleDecker,
    DoubleDecker
}

/// <summary>
/// The three vehicles on the depot forecourt.  Capacity is the headline
/// difference, but the bigger buses are also longer and slower to steer.
/// </summary>
public sealed record BusTypeProfile(
    BusType Type,
    string Name,
    string Description,
    int Capacity,
    float Length,
    float Width,
    float MaxSpeed,
    float Steering,
    float Acceleration)
{
    public static readonly BusTypeProfile Minibus = new(
        BusType.Minibus,
        "Minibus",
        "Nippy and easy to thread through traffic, but only sixteen seats.",
        Capacity: 16,
        Length: 96f,
        Width: 42f,
        MaxSpeed: 355f,
        Steering: 2.25f,
        Acceleration: 205f);

    public static readonly BusTypeProfile SingleDecker = new(
        BusType.SingleDecker,
        "Single Decker",
        "The standard Scarthorpe bus: thirty-two seats and a sensible turning circle.",
        Capacity: 32,
        Length: 124f,
        Width: 46f,
        MaxSpeed: 330f,
        Steering: 1.95f,
        Acceleration: 178f);

    public static readonly BusTypeProfile DoubleDecker = new(
        BusType.DoubleDecker,
        "Double Decker",
        "Fifty-six seats and a proper UK bus, but it is long and slow to swing round.",
        Capacity: 56,
        Length: 148f,
        Width: 48f,
        MaxSpeed: 305f,
        Steering: 1.68f,
        Acceleration: 160f);

    public static IReadOnlyList<BusTypeProfile> All { get; } =
        new[] { Minibus, SingleDecker, DoubleDecker };

    public static BusTypeProfile For(BusType type) => type switch
    {
        BusType.Minibus => Minibus,
        BusType.SingleDecker => SingleDecker,
        BusType.DoubleDecker => DoubleDecker,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown bus type")
    };
}
