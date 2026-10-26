using System.Numerics;

namespace BusDriver.Core;

public enum TileKind
{
    Grass,
    Road,
    Pavement,
    Building,
    Park,
    Water,
    Terminus
}

public enum RoadShape
{
    None,
    Horizontal,
    Vertical,
    Cross
}

/// <summary>
/// Scarthorpe, rebuilt for the bus era.  A regular grid of two-tile roads
/// with pavements, blocks of buildings and the occasional park or canal.  The
/// map is deterministic for a given seed, so the traffic, stops and tests all
/// agree on where the kerbs are.
/// </summary>
public sealed class TownMap
{
    public const int Width = 88;
    public const int Height = 64;
    public const float TileSize = 64f;
    public const float WorldWidth = Width * TileSize;
    public const float WorldHeight = Height * TileSize;

    private readonly TileKind[,] _tiles;

    public TownMap(int seed = 1985)
    {
        Seed = seed;
        _tiles = Build(seed);
    }

    public int Seed { get; }

    // Roads are two tiles wide, so the centre line of a road - the one the
    // median is painted on and the one traffic keeps to - is the boundary
    // between tiles 6 and 7 of each block, not the middle of tile 6.
    public static IReadOnlyList<float> RoadCentresX { get; } =
        Enumerable.Range(0, 10).Select(i => (7 + i * 8) * TileSize).ToArray();

    public static IReadOnlyList<float> RoadCentresY { get; } =
        Enumerable.Range(0, 7).Select(i => (7 + i * 8) * TileSize).ToArray();

    /// <summary>The centre line of the road corridor nearest this coordinate.</summary>
    public static float RoadCentreOf(float coordinate)
    {
        var block = MathF.Floor(coordinate / (8 * TileSize));
        return (block * 8 + 7) * TileSize;
    }

    public static bool IsRoadTile(int x, int y)
    {
        if (x < 6 || x > 79 || y < 6 || y > 55)
        {
            return false;
        }

        var vertical = x % 8 is 6 or 7;
        var horizontal = y % 8 is 6 or 7;
        return vertical || horizontal;
    }

    public TileKind this[int x, int y]
    {
        get
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return TileKind.Grass;
            }

            return _tiles[x, y];
        }
    }

    public TileKind TileAt(Vector2 world)
    {
        var x = (int)MathF.Floor(world.X / TileSize);
        var y = (int)MathF.Floor(world.Y / TileSize);
        return this[x, y];
    }

    public bool IsDriveable(TileKind kind) =>
        kind is TileKind.Road or TileKind.Pavement or TileKind.Terminus;

    public bool IsSolid(TileKind kind) =>
        kind is TileKind.Building or TileKind.Water or TileKind.Park or TileKind.Grass;

    public bool IsSolidAt(Vector2 world) => IsSolid(TileAt(world));

    public bool IsRoadAt(Vector2 world) => TileAt(world) == TileKind.Road;

    /// <summary>1.0 on tarmac, less on the kerb.</summary>
    public float SurfaceFactorAt(Vector2 world) => TileAt(world) switch
    {
        TileKind.Road => 1f,
        TileKind.Terminus => 1f,
        TileKind.Pavement => 0.82f,
        _ => 0.55f
    };

    public RoadShape ShapeAt(int x, int y)
    {
        if (this[x, y] != TileKind.Road)
        {
            return RoadShape.None;
        }

        var horizontal = IsRoadTile(x - 1, y) || IsRoadTile(x + 1, y);
        var vertical = IsRoadTile(x, y - 1) || IsRoadTile(x, y + 1);
        return (horizontal, vertical) switch
        {
            (true, true) => RoadShape.Cross,
            (true, false) => RoadShape.Horizontal,
            (false, true) => RoadShape.Vertical,
            _ => RoadShape.Horizontal
        };
    }

    /// <summary>A stable asset index for building and tree variants.</summary>
    public int VariantAt(int x, int y)
    {
        unchecked
        {
            var h = Seed * 374761393 + x * 668265263 + y * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return Math.Abs(h ^ (h >> 16)) % 4;
        }
    }

    private static TileKind[,] Build(int seed)
    {
        var rng = new Random(seed);
        var tiles = new TileKind[Width, Height];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                tiles[x, y] = IsRoadTile(x, y) ? TileKind.Road : TileKind.Grass;
            }
        }

        // A pavement skirt around every road.
        for (var y = 1; y < Height - 1; y++)
        {
            for (var x = 1; x < Width - 1; x++)
            {
                if (tiles[x, y] != TileKind.Grass)
                {
                    continue;
                }

                var touchingRoad =
                    tiles[x - 1, y] == TileKind.Road || tiles[x + 1, y] == TileKind.Road ||
                    tiles[x, y - 1] == TileKind.Road || tiles[x, y + 1] == TileKind.Road;
                if (touchingRoad)
                {
                    tiles[x, y] = TileKind.Pavement;
                }
            }
        }

        // Each 8x8 block has a 4x4 interior: mostly terraces, sometimes a park
        // or a canal basin, so the town is not a uniform maze of rooftops.
        for (var by = 0; by < 8; by++)
        {
            for (var bx = 0; bx < 11; bx++)
            {
                var ox = bx * 8 + 9;
                var oy = by * 8 + 9;
                var roll = rng.NextDouble();
                var kind = roll switch
                {
                    < 0.075 => TileKind.Water,
                    < 0.19 => TileKind.Park,
                    _ => TileKind.Building
                };

                for (var y = oy; y < oy + 4; y++)
                {
                    for (var x = ox; x < ox + 4; x++)
                    {
                        if (x > 0 && y > 0 && x < Width && y < Height)
                        {
                            tiles[x, y] = kind;
                        }
                    }
                }
            }
        }

        // Terminus yards: a five by five apron of tarmac around each terminus.
        foreach (var stop in RouteCatalog.Terminuses)
        {
            for (var y = stop.TileY - 2; y <= stop.TileY + 2; y++)
            {
                for (var x = stop.TileX - 2; x <= stop.TileX + 2; x++)
                {
                    if (x > 1 && y > 1 && x < Width - 1 && y < Height - 1)
                    {
                        tiles[x, y] = TileKind.Terminus;
                    }
                }
            }
        }

        return tiles;
    }
}
