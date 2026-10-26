using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class DifficultyTests
{
    [Fact]
    public void Three_levels_are_offered()
    {
        Assert.Equal(
            new[] { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard },
            DifficultyProfile.All.Select(d => d.Level));
    }

    [Fact]
    public void Harder_shifts_are_shorter_and_tighter()
    {
        var easy = DifficultyProfile.Easy;
        var medium = DifficultyProfile.Medium;
        var hard = DifficultyProfile.Hard;

        Assert.True(easy.ShiftMinutes > medium.ShiftMinutes);
        Assert.True(medium.ShiftMinutes > hard.ShiftMinutes);
        Assert.True(easy.ScheduleToleranceSeconds > medium.ScheduleToleranceSeconds);
        Assert.True(medium.ScheduleToleranceSeconds > hard.ScheduleToleranceSeconds);
    }

    [Fact]
    public void Harder_shifts_have_more_traffic_and_worse_weather()
    {
        Assert.True(DifficultyProfile.Easy.TrafficVehicles < DifficultyProfile.Medium.TrafficVehicles);
        Assert.True(DifficultyProfile.Medium.TrafficVehicles < DifficultyProfile.Hard.TrafficVehicles);
        Assert.True(DifficultyProfile.Easy.WeatherSeverity < DifficultyProfile.Hard.WeatherSeverity);
        Assert.True(DifficultyProfile.Easy.DamageMultiplier < DifficultyProfile.Hard.DamageMultiplier);
    }

    [Fact]
    public void Harder_shifts_have_more_waiting_passengers()
    {
        Assert.True(DifficultyProfile.Hard.PassengerBase > DifficultyProfile.Easy.PassengerBase);
        Assert.True(DifficultyProfile.Hard.PassengerBase + DifficultyProfile.Hard.PassengerSpread >
                    DifficultyProfile.Easy.PassengerBase + DifficultyProfile.Easy.PassengerSpread);
    }

    [Fact]
    public void Lookup_returns_the_requested_profile()
    {
        Assert.Same(DifficultyProfile.Easy, DifficultyProfile.For(DifficultyLevel.Easy));
        Assert.Same(DifficultyProfile.Medium, DifficultyProfile.For(DifficultyLevel.Medium));
        Assert.Same(DifficultyProfile.Hard, DifficultyProfile.For(DifficultyLevel.Hard));
    }
}
