using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class ScoreTests
{
    [Fact]
    public void Total_combines_fares_punctuality_and_trip_bonus()
    {
        var score = new ScoreBreakdown
        {
            FaresPence = 1234,
            PunctualityPoints = 90,
            TripBonus = 150,
            DamagePenalty = 20
        };

        Assert.Equal(12 + 90 + 150 - 20, score.Total);
        Assert.Equal("£12.34", score.FaresText);
    }

    [Fact]
    public void Damage_cannot_make_the_score_negative()
    {
        var score = new ScoreBreakdown
        {
            FaresPence = 100,
            PunctualityPoints = 0,
            TripBonus = 0,
            DamagePenalty = 900
        };

        Assert.Equal(0, score.Total);
    }

    [Fact]
    public void Pence_are_formatted_with_two_digits()
    {
        Assert.Equal("£0.05", new ScoreBreakdown { FaresPence = 5 }.FaresText);
        Assert.Equal("£10.00", new ScoreBreakdown { FaresPence = 1000 }.FaresText);
    }
}
