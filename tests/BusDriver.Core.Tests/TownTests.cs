using System.Numerics;
using BusDriver.Core;

namespace BusDriver.Core.Tests;

public class TownTests
{
    [Fact]
    public void The_town_is_deterministic()
    {
        var a = new TownMap(1985);
        var b = new TownMap(1985);
        for (var y = 0; y < TownMap.Height; y += 3)
        {
            for (var x = 0; x < TownMap.Width; x += 3)
            {
                Assert.Equal(a[x, y], b[x, y]);
            }
        }
    }

    [Fact]
    public void Roads_form_the_expected_grid()
    {
        Assert.True(TownMap.IsRoadTile(6, 6));
        Assert.True(TownMap.IsRoadTile(14, 10));
        Assert.True(TownMap.IsRoadTile(38, 22));
        Assert.False(TownMap.IsRoadTile(10, 10));
        Assert.False(TownMap.IsRoadTile(0, 0));
        Assert.False(TownMap.IsRoadTile(87, 63));
    }

    [Fact]
    public void Terminus_yards_are_open_tarmac()
    {
        foreach (var terminus in RouteCatalog.Terminuses)
        {
            Assert.Equal(TileKind.Terminus, new TownMap()[terminus.TileX, terminus.TileY]);
        }
    }

    [Fact]
    public void Buildings_are_solid_and_roads_are_not()
    {
        var town = new TownMap();
        var building = FindTile(town, TileKind.Building);
        var road = FindTile(town, TileKind.Road);

        Assert.True(town.IsSolidAt(Centre(building)));
        Assert.False(town.IsSolidAt(Centre(road)));
        Assert.Equal(1f, town.SurfaceFactorAt(Centre(road)));
    }

    [Fact]
    public void Outside_the_map_is_solid()
    {
        var town = new TownMap();
        Assert.True(town.IsSolidAt(new Vector2(-1, -1)));
        Assert.True(town.IsSolidAt(new Vector2(TownMap.WorldWidth + 5, 100)));
    }

    [Fact]
    public void Road_centres_are_inside_roads()
    {
        var town = new TownMap();
        foreach (var x in TownMap.RoadCentresX)
        {
            Assert.False(town.IsSolidAt(new Vector2(x, TownMap.RoadCentresY[2])));
        }

        foreach (var y in TownMap.RoadCentresY)
        {
            Assert.False(town.IsSolidAt(new Vector2(TownMap.RoadCentresX[3], y)));
        }
    }

    private static (int X, int Y) FindTile(TownMap town, TileKind kind)
    {
        for (var y = 0; y < TownMap.Height; y++)
        {
            for (var x = 0; x < TownMap.Width; x++)
            {
                if (town[x, y] == kind)
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException($"No {kind} tile");
    }

    private static Vector2 Centre((int X, int Y) tile) =>
        new((tile.X + 0.5f) * TownMap.TileSize, (tile.Y + 0.5f) * TownMap.TileSize);
}
