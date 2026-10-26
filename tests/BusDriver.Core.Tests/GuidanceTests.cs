using System.Numerics;
using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class GuidanceTests
{
    private static readonly Vector2 Junction =
        new(TownMap.RoadCentresX[1], TownMap.RoadCentresY[1]);

    [Fact]
    public void A_stop_straight_ahead_is_no_turn()
    {
        var bus = Junction - new Vector2(200f, 0f);
        var target = Junction + new Vector2(200f, 0f);

        var hint = RouteGuidance.Hint(bus, 0f, target);

        Assert.Equal(TurnDirection.StraightOn, hint.Direction);
    }

    [Fact]
    public void A_stop_to_the_south_of_an_eastbound_bus_is_a_right_turn()
    {
        var bus = Junction - new Vector2(200f, 0f);
        var target = Junction + new Vector2(0f, 220f);

        var hint = RouteGuidance.Hint(bus, 0f, target);

        Assert.Equal(TurnDirection.Right, hint.Direction);
    }

    [Fact]
    public void A_stop_to_the_north_of_an_eastbound_bus_is_a_left_turn()
    {
        var bus = Junction - new Vector2(200f, 0f);
        var target = Junction - new Vector2(0f, 220f);

        var hint = RouteGuidance.Hint(bus, 0f, target);

        Assert.Equal(TurnDirection.Left, hint.Direction);
    }

    [Fact]
    public void The_turn_prompt_counts_down_to_the_junction()
    {
        var bus = Junction - new Vector2(200f, 0f);

        var hint = RouteGuidance.Hint(bus, 0f, Junction + new Vector2(400f, 0f));

        Assert.Equal(200f * RouteGuidance.MetresPerPixel, hint.MetresToJunction, 0);
    }

    [Fact]
    public void Easy_announces_every_junction_and_hard_announces_none()
    {
        Assert.Equal(GuidanceLevel.TurnByTurn, DifficultyProfile.Easy.Guidance);
        Assert.Equal(GuidanceLevel.TurnsOnly, DifficultyProfile.Medium.Guidance);
        Assert.Equal(GuidanceLevel.None, DifficultyProfile.Hard.Guidance);
    }
}
