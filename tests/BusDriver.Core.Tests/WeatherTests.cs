using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class WeatherTests
{
    [Fact]
    public void Snow_has_less_grip_and_visibility_than_clear()
    {
        var clear = new WeatherState(WeatherKind.Clear, 1);
        var rain = new WeatherState(WeatherKind.Rain, 1);
        var snow = new WeatherState(WeatherKind.Snow, 1);

        Assert.True(clear.Grip > rain.Grip);
        Assert.True(rain.Grip > snow.Grip);
        Assert.True(clear.SpeedFactor > snow.SpeedFactor);
        Assert.True(clear.Visibility > rain.Visibility);
        Assert.True(rain.Visibility > new WeatherState(WeatherKind.Fog, 1).Visibility);
    }

    [Fact]
    public void Forced_weather_is_reported()
    {
        var weather = new WeatherSystem(1, 1f);
        weather.Force(WeatherKind.Fog, 0.8f, 30);
        Assert.Equal(WeatherKind.Fog, weather.Current.Kind);
        Assert.Equal(0.8f, weather.Current.Intensity, 3);
        Assert.Equal("Fog", weather.Current.Label);
    }

    [Fact]
    public void Weather_changes_when_its_time_expires()
    {
        var weather = new WeatherSystem(2, 1f);
        weather.Force(WeatherKind.Clear, 1, 1);
        weather.Update(1.1f);
        Assert.True(weather.RemainingSeconds > 0);
        Assert.Equal(1.1, weather.ElapsedSeconds, 3);
    }

    [Fact]
    public void Same_seed_gives_the_same_weather_sequence()
    {
        var a = new WeatherSystem(123, 1f);
        var b = new WeatherSystem(123, 1f);
        for (var i = 0; i < 20; i++)
        {
            a.Update(60);
            b.Update(60);
            Assert.Equal(a.Current.Kind, b.Current.Kind);
        }
    }
}
