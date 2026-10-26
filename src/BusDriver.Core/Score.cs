namespace BusDriver.Core;

/// <summary>The end-of-shift score card.  Fares are kept in pence, the rest in points.</summary>
public sealed class ScoreBreakdown
{
    public int FaresPence { get; set; }

    public int PunctualityPoints { get; set; }

    public int TripBonus { get; set; }

    public int PassengersCarried { get; set; }

    public int PassengersDelivered { get; set; }

    public int OnTimeStops { get; set; }

    public int LateStops { get; set; }

    public int TripsCompleted { get; set; }

    public int DamagePenalty { get; set; }

    public int Total =>
        Math.Max(0, FaresPence / 100 + PunctualityPoints + TripBonus - DamagePenalty);

    public string FaresText => $"£{FaresPence / 100}.{FaresPence % 100:00}";
}
