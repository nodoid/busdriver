using BusDriver.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BusDriver.Game.Core;

/// <summary>Draws the town and everything that lives in it.</summary>
public sealed class WorldRenderer
{
    private readonly Assets _assets;
    private readonly Ui _ui;

    public WorldRenderer(Assets assets, Ui ui)
    {
        _assets = assets;
        _ui = ui;
    }

    public void Draw(SpriteBatch batch, BusDriverSession session, Vector2 camera, float time)
    {
        var town = session.Town;
        var viewLeft = camera.X - 680;
        var viewTop = camera.Y - 400;
        var viewRight = camera.X + 680;
        var viewBottom = camera.Y + 400;

        var minX = Math.Max(0, (int)MathF.Floor(viewLeft / TownMap.TileSize));
        var maxX = Math.Min(TownMap.Width - 1, (int)MathF.Ceiling(viewRight / TownMap.TileSize));
        var minY = Math.Max(0, (int)MathF.Floor(viewTop / TownMap.TileSize));
        var maxY = Math.Min(TownMap.Height - 1, (int)MathF.Ceiling(viewBottom / TownMap.TileSize));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var destination = new Rectangle(
                    x * (int)TownMap.TileSize,
                    y * (int)TownMap.TileSize,
                    (int)TownMap.TileSize,
                    (int)TownMap.TileSize);
                var texture = TileTexture(town, x, y);
                batch.Draw(texture, destination, Color.White);

                if (town[x, y] == TileKind.Park && town.VariantAt(x, y) == 0)
                {
                    batch.Draw(
                        _assets.Tex("tree"),
                        new Rectangle(destination.X + 8, destination.Y + 2,
                            (int)(TownMap.TileSize * 0.9f), (int)(TownMap.TileSize * 0.9f)),
                        Color.White);
                }
            }
        }

        DrawLaneMarkings(batch, town, viewLeft, viewTop, viewRight, viewBottom);
        DrawTerminusLights(batch, session);
        DrawStops(batch, session, time);
        DrawTraffic(batch, session);
        DrawBus(batch, session.Bus);
    }

    private Texture2D TileTexture(TownMap town, int x, int y)
    {
        var variant = town.VariantAt(x, y);
        return town[x, y] switch
        {
            TileKind.Grass => _assets.Tex($"tile_grass_{variant % 2}"),
            TileKind.Pavement => _assets.Tex($"tile_pavement_{variant % 2}"),
            TileKind.Road => _assets.Tex("tile_road"),
            TileKind.Terminus => _assets.Tex("tile_terminus"),
            TileKind.Building => _assets.Tex($"tile_building_{variant}"),
            TileKind.Park => _assets.Tex($"tile_park_{variant % 2}"),
            TileKind.Water => _assets.Tex($"tile_water_{variant % 2}"),
            _ => _assets.Tex("tile_grass_0")
        };
    }

    private void DrawLaneMarkings(
        SpriteBatch batch, TownMap town, float left, float top, float right, float bottom)
    {
        var dash = new Color(232, 232, 220) * 0.7f;
        var startX = (int)MathF.Floor(left / 90) * 90;
        foreach (var centre in TownMap.RoadCentresY)
        {
            if (centre < top - 40 || centre > bottom + 40)
            {
                continue;
            }

            for (var x = startX; x < right + 90; x += 90)
            {
                // Only paint the centre line where there is tarmac under it, so
                // no dashes trail across grass, roofs or the terminus aprons.
                if (!CentreLineRuns(town, x, centre, 44f, true))
                {
                    continue;
                }

                batch.Draw(_assets.Pixel,
                    new Rectangle(x, (int)centre - 3, 44, 6), dash);
            }
        }

        var startY = (int)MathF.Floor(top / 90) * 90;
        foreach (var centre in TownMap.RoadCentresX)
        {
            if (centre < left - 40 || centre > right + 40)
            {
                continue;
            }

            for (var y = startY; y < bottom + 90; y += 90)
            {
                if (!CentreLineRuns(town, centre, y, 44f, false))
                {
                    continue;
                }

                batch.Draw(_assets.Pixel,
                    new Rectangle((int)centre - 3, y, 6, 44), dash);
            }
        }
    }

    /// <summary>
    /// True when a dash of the given length lies completely on ordinary road
    /// tiles.  Junction tiles are excluded as well, because UK junctions are
    /// left unmarked.
    /// </summary>
    private static bool CentreLineRuns(
        TownMap town, float x, float y, float length, bool horizontal)
    {
        foreach (var step in stackalloc[] { 2f, length * 0.5f, length - 2f })
        {
            var probe = horizontal ? new Vector2(x + step, y) : new Vector2(x, y + step);
            var tx = (int)MathF.Floor(probe.X / TownMap.TileSize);
            var ty = (int)MathF.Floor(probe.Y / TownMap.TileSize);
            // Roads are two tiles wide, so the whole crossroads is a junction:
            // any tile that belongs to both a road row and a road column.
            var junction = tx % 8 is 6 or 7 && ty % 8 is 6 or 7;
            if (town[tx, ty] != TileKind.Road || junction)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Signal heads on the four corners of every terminus junction.</summary>
    private void DrawTerminusLights(SpriteBatch batch, BusDriverSession session)
    {
        foreach (var light in session.Traffic.Lights)
        {
            var texture = _assets.Tex(light.Red ? "traffic_light_red" : "traffic_light_green");
            const float scale = 0.34f;
            var width = texture.Width * scale;
            var height = texture.Height * scale;

            foreach (var corner in LightCorners)
            {
                var position = ToXna(light.Position) + corner;
                batch.Draw(
                    texture,
                    new Rectangle((int)(position.X - width / 2), (int)(position.Y - height / 2),
                        (int)width, (int)height),
                    Color.White);
            }
        }
    }

    private static readonly Vector2[] LightCorners =
    {
        new(-84f, -84f), new(84f, -84f), new(-84f, 84f), new(84f, 84f)
    };

    private void DrawStops(SpriteBatch batch, BusDriverSession session, float time)
    {
        var next = session.NextStop;
        foreach (var stop in session.Route.Stops)
        {
            var position = StopDisplayPosition(stop, session.Town);
            var texture = _assets.Tex(stop.IsTerminus ? "terminus" : "bus_stop");
            var scale = stop.IsTerminus ? 0.62f : 0.5f;
            var size = new Vector2(texture.Width, texture.Height) * scale;
            var tint = Color.White;
            if (stop.Id == next?.Id)
            {
                var pulse = 0.5f + 0.5f * MathF.Sin(time * 5f);
                tint = Color.Lerp(Color.White, new Color(255, 228, 120), pulse);
            }

            batch.Draw(
                texture,
                new Rectangle((int)(position.X - size.X / 2), (int)(position.Y - size.Y / 2),
                    (int)size.X, (int)size.Y),
                tint);

            if (stop.Id == next?.Id)
            {
                var stopPoint = ToXna(stop.Position);
                DrawRing(batch, stopPoint, 56f + pulse(0, time), new Color(255, 218, 92));
                _ui.Text("small", stop.Name,
                    new Vector2(stopPoint.X, stopPoint.Y - 128),
                    new Color(255, 244, 204), 0.9f, TextAlign.Centre);
            }
            else if (session.NextStop is not null &&
                     System.Numerics.Vector2.Distance(stop.Position, session.Bus.Position) < 700)
            {
                _ui.Text("tiny", stop.Name,
                    new Vector2(stop.Position.X, stop.Position.Y - 92),
                    new Color(230, 232, 228) * 0.82f, 0.9f, TextAlign.Centre);
            }
        }

        // Passenger queue dots.
        foreach (var stop in session.Route.Stops)
        {
            if (stop.IsTerminus || session.WaitingAt(stop.Id) <= 0)
            {
                continue;
            }

            var position = StopDisplayPosition(stop, session.Town);
            var count = Math.Min(8, session.WaitingAt(stop.Id));
            for (var i = 0; i < count; i++)
            {
                var x = position.X - (count - 1) * 9f + i * 18f;
                var y = position.Y + 42f;
                batch.Draw(_assets.Pixel, new Rectangle((int)x - 6, (int)y - 10, 12, 20),
                    new Color(44, 62, 82));
                batch.Draw(_assets.Pixel, new Rectangle((int)x - 4, (int)y - 20, 8, 8),
                    new Color(232, 198, 158));
            }
        }
    }

    private static float pulse(int unused, float time) => 4f * (0.5f + 0.5f * MathF.Sin(time * 5f));

    private void DrawTraffic(SpriteBatch batch, BusDriverSession session)
    {
        foreach (var vehicle in session.Traffic.Vehicles)
        {
            var (textureName, worldLength) = vehicle.Kind switch
            {
                VehicleKind.Car => (vehicle.Colour switch
                {
                    0xFFE53935 => "traffic_car_red",
                    0xFF1E88E5 => "traffic_car_blue",
                    0xFF43A047 => "traffic_car_green",
                    _ => "traffic_car_white"
                }, 68f),
                VehicleKind.Taxi => ("traffic_taxi", 70f),
                VehicleKind.Van => ("traffic_van", 86f),
                _ => ("traffic_lorry", 126f)
            };

            var texture = _assets.Tex(textureName);
            var scale = worldLength / texture.Width;
            batch.Draw(
                texture,
                ToXna(vehicle.Position),
                null,
                Color.White,
                vehicle.Heading,
                new Vector2(texture.Width / 2f, texture.Height / 2f),
                scale,
                SpriteEffects.None,
                0f);
        }
    }

    private void DrawBus(SpriteBatch batch, Bus bus)
    {
        var textureName = bus.Type switch
        {
            BusType.Minibus => "bus_minibus",
            BusType.SingleDecker => "bus_single",
            _ => "bus_double"
        };
        var texture = _assets.Tex(textureName);
        var scale = bus.Length / texture.Width;
        batch.Draw(
            texture,
            ToXna(bus.Position),
            null,
            Color.White,
            bus.Heading,
            new Vector2(texture.Width / 2f, texture.Height / 2f),
            scale,
            SpriteEffects.None,
            0f);
    }

    private void DrawRing(SpriteBatch batch, Vector2 centre, float radius, Color colour)
    {
        const int segments = 40;
        var previous = centre + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var point = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            batch.Draw(_assets.Pixel, previous, null, colour, MathF.Atan2(point.Y - previous.Y, point.X - previous.X),
                new Vector2(0f, 0.5f), new Vector2(Vector2.Distance(previous, point), 7f),
                SpriteEffects.None, 0f);
            previous = point;
        }
    }

    private static Vector2 StopDisplayPosition(BusStop stop, TownMap town)
    {
        var vertical = TownMap.IsRoadTile(stop.TileX, stop.TileY - 1) ||
                       TownMap.IsRoadTile(stop.TileX, stop.TileY + 1);
        var offset = vertical ? new Vector2(82, 0) : new Vector2(0, 82);
        var centre = ToXna(stop.Position);
        var position = centre + offset;
        if (town.IsSolidAt(ToNum(position)))
        {
            position = centre - offset;
        }

        return position;
    }

    private static Vector2 ToXna(System.Numerics.Vector2 value) => new(value.X, value.Y);

    private static System.Numerics.Vector2 ToNum(Vector2 value) => new(value.X, value.Y);
}
