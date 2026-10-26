using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace BusDriver.Game.Core;

public sealed class Glyph
{
    public Rectangle Source { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public float Advance { get; init; }
}

/// <summary>
/// A generated bitmap font.  No content pipeline and no platform font
/// lookup, so the HUD looks identical on Android and iOS.
/// </summary>
public sealed class BitmapFont
{
    private readonly Dictionary<char, Glyph> _glyphs;

    public BitmapFont(Texture2D atlas, Dictionary<char, Glyph> glyphs, int lineHeight, int size)
    {
        Atlas = atlas;
        _glyphs = glyphs;
        LineHeight = lineHeight;
        Size = size;
    }

    public Texture2D Atlas { get; }
    public int LineHeight { get; }
    public int Size { get; }

    public bool TryGetGlyph(char c, out Glyph glyph) => _glyphs.TryGetValue(c, out glyph!);

    public float MeasureLineWidth(string text, float scale = 1f)
    {
        var width = 0f;
        foreach (var c in text)
        {
            if (_glyphs.TryGetValue(c, out var glyph))
            {
                width += glyph.Advance;
            }
            else if (_glyphs.TryGetValue('?', out var fallback))
            {
                width += fallback.Advance;
            }
        }

        return width * scale;
    }

    public Vector2 Measure(string text, float scale = 1f)
    {
        var lines = text.Split('\n');
        var widest = lines.Max(l => MeasureLineWidth(l, scale));
        return new Vector2(widest, lines.Length * LineHeight * scale);
    }
}

/// <summary>
/// Loads every texture, font and sound from the Content folder.  All files are
/// plain PNG/WAV/JSON, so there is no MGCB build step on any platform.
/// </summary>
public sealed class Assets : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapFont> _fonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _errors = new();

    public Assets(GraphicsDevice device) => _device = device;

    public IReadOnlyList<string> Errors => _errors;

    public Texture2D Pixel { get; private set; } = null!;

    public static readonly string[] TextureNames =
    {
        "splash", "title_bg", "logo", "panel", "button", "button_pressed",
        "bus_minibus", "bus_single", "bus_double",
        "traffic_car_red", "traffic_car_blue", "traffic_car_green",
        "traffic_car_white", "traffic_taxi", "traffic_van", "traffic_lorry",
        "bus_stop", "terminus", "tree",
        "traffic_light_red", "traffic_light_green",
        "tile_grass_0", "tile_grass_1", "tile_pavement_0", "tile_pavement_1",
        "tile_road", "tile_terminus",
        "tile_building_0", "tile_building_1", "tile_building_2", "tile_building_3",
        "tile_park_0", "tile_park_1", "tile_water_0", "tile_water_1",
        "weather_clear", "weather_overcast", "weather_rain", "weather_fog", "weather_snow"
    };

    public static readonly string[] FontNames = { "tiny", "small", "body", "medium", "large", "huge" };

    public static readonly string[] SoundNames =
    {
        "engine_loop", "rain_loop", "door_open", "door_close", "bell", "chime",
        "buzzer", "crash", "horn", "indicator", "ui_click", "ui_confirm",
        "fanfare", "skid"
    };

    public void LoadAll()
    {
        Pixel = new Texture2D(_device, 1, 1);
        Pixel.SetData(new[] { Color.White });

        foreach (var name in TextureNames)
        {
            try
            {
                _textures[name] = LoadTexture($"Content/art/{name}.png");
            }
            catch (Exception ex)
            {
                _errors.Add($"{name}: {ex.Message}");
            }
        }

        foreach (var name in FontNames)
        {
            try
            {
                _fonts[name] = LoadFont(name);
            }
            catch (Exception ex)
            {
                _errors.Add($"font {name}: {ex.Message}");
            }
        }

        foreach (var name in SoundNames)
        {
            try
            {
                using var stream = TitleContainer.OpenStream($"Content/audio/{name}.wav");
                _sounds[name] = SoundEffect.FromStream(stream);
            }
            catch (Exception ex)
            {
                _errors.Add($"sound {name}: {ex.Message}");
            }
        }
    }

    private Texture2D LoadTexture(string path)
    {
        using var stream = TitleContainer.OpenStream(path);
        var texture = Texture2D.FromStream(_device, stream);
        texture.Name = Path.GetFileNameWithoutExtension(path);
        return texture;
    }

    private BitmapFont LoadFont(string name)
    {
        using var stream = TitleContainer.OpenStream($"Content/font/font_{name}.png");
        var atlas = Texture2D.FromStream(_device, stream);

        string json;
        using (var jsonStream = TitleContainer.OpenStream($"Content/font/font_{name}.json"))
        using (var reader = new StreamReader(jsonStream))
        {
            json = reader.ReadToEnd();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var glyphs = new Dictionary<char, Glyph>();

        foreach (var property in root.GetProperty("glyphs").EnumerateObject())
        {
            if (property.Name.Length == 0)
            {
                continue;
            }

            var g = property.Value;
            glyphs[property.Name[0]] = new Glyph
            {
                Source = new Rectangle(
                    g.GetProperty("x").GetInt32(),
                    g.GetProperty("y").GetInt32(),
                    Math.Max(1, g.GetProperty("w").GetInt32()),
                    Math.Max(1, g.GetProperty("h").GetInt32())),
                OffsetX = g.GetProperty("ox").GetInt32(),
                OffsetY = g.GetProperty("oy").GetInt32(),
                Advance = g.GetProperty("adv").GetSingle()
            };
        }

        return new BitmapFont(
            atlas,
            glyphs,
            root.GetProperty("lineHeight").GetInt32(),
            root.GetProperty("size").GetInt32());
    }

    public Texture2D Tex(string name) =>
        _textures.TryGetValue(name, out var texture)
            ? texture
            : _textures.TryGetValue("logo", out var fallback)
                ? fallback
                : throw new KeyNotFoundException($"Texture '{name}' was not loaded.");

    public bool HasTexture(string name) => _textures.ContainsKey(name);

    public BitmapFont Font(string name) =>
        _fonts.TryGetValue(name, out var font)
            ? font
            : _fonts.TryGetValue("body", out var fallback)
                ? fallback
                : throw new KeyNotFoundException($"Font '{name}' was not loaded.");

    public SoundEffect? Sound(string name) =>
        _sounds.TryGetValue(name, out var sound) ? sound : null;

    public int LoadedTextureCount => _textures.Count;
    public int LoadedFontCount => _fonts.Count;
    public int LoadedSoundCount => _sounds.Count;

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        foreach (var font in _fonts.Values)
        {
            font.Atlas.Dispose();
        }

        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }

        Pixel?.Dispose();
        _textures.Clear();
        _fonts.Clear();
        _sounds.Clear();
    }
}
