using System.Numerics;

namespace BusDriver.Core;

/// <summary>A named stop on the Scarthorpe network.</summary>
public sealed class BusStop
{
    public BusStop(string id, string name, int tileX, int tileY, bool isTerminus)
    {
        Id = id;
        Name = name;
        TileX = tileX;
        TileY = tileY;
        IsTerminus = isTerminus;
        Position = new Vector2(
            (tileX + 0.5f) * TownMap.TileSize,
            (tileY + 0.5f) * TownMap.TileSize);
    }

    public string Id { get; }

    public string Name { get; }

    public int TileX { get; }

    public int TileY { get; }

    /// <summary>
    /// Terminuses are the old goods depots re-opened as bus stations: everyone
    /// gets off there and nobody boards.
    /// </summary>
    public bool IsTerminus { get; }

    public Vector2 Position { get; }

    public override string ToString() => $"{Name} ({Id})";
}

/// <summary>One scheduled bus route.</summary>
public sealed class Route
{
    public Route(
        string number,
        string name,
        string description,
        bool heritage,
        uint colour,
        IReadOnlyList<BusStop> stops)
    {
        Number = number;
        Name = name;
        Description = description;
        Heritage = heritage;
        Colour = colour;
        Stops = stops;
    }

    /// <summary>The route blind, for example "1A" or "12".</summary>
    public string Number { get; }

    public string Name { get; }

    public string Description { get; }

    /// <summary>True for one of the four freight runs inherited from Juggernaut.</summary>
    public bool Heritage { get; }

    /// <summary>ARGB colour for the route map and the destination blind.</summary>
    public uint Colour { get; }

    public IReadOnlyList<BusStop> Stops { get; }

    public bool IsNight => Number == "10";

    public BusStop FirstStop => Stops[0];

    public BusStop LastStop => Stops[^1];

    public override string ToString() => $"{Number} {Name}";
}

/// <summary>
/// The whole network.  The four "heritage" routes follow the four freight
/// deliveries of the original game (timber, coal, oil and fruit & veg); the
/// other twelve are the new bus services.
/// </summary>
public static class RouteCatalog
{
    private static readonly Dictionary<string, BusStop> StopLookup;
    private static readonly List<BusStop> StopsList;

    static RouteCatalog()
    {
        // Terminuses - the former depots.  Passengers may only alight here.
        var stops = new List<BusStop>
        {
            new("T_BUS", "Scarthorpe Bus Station", 6, 54, true),
            new("T_NORTH", "Northgate Terminus", 78, 6, true),
            new("T_DOCKS", "Dockside Terminus", 6, 30, true),
            new("T_HOSP", "St Cuthbert's Hospital", 70, 54, true),
            new("T_RACE", "Racecourse Terminus", 78, 30, true),
            new("T_UNI", "University Terminus", 30, 6, true),
            new("T_RIVER", "Riverside Terminus", 46, 54, true),
            new("T_IND", "Industrial Park Terminus", 62, 6, true),

            // Ordinary stops - passengers board and alight here.
            new("S_TIMBER", "Timber Yard", 14, 10, false),
            new("S_COAL", "Coal Yard", 22, 14, false),
            new("S_OIL", "Oil Terminal", 38, 30, false),
            new("S_FRUIT", "Fruit & Veg Market", 6, 46, false),
            new("S_HIGH", "High Street", 22, 38, false),
            new("S_MARKET", "Market Cross", 30, 22, false),
            new("S_CATH", "Cathedral", 46, 14, false),
            new("S_STATION", "Railway Station", 38, 46, false),
            new("S_PARK", "Queen's Park", 54, 38, false),
            new("S_HARBOUR", "Harbour Road", 14, 54, false),
            new("S_MILL", "Old Mill", 62, 22, false),
            new("S_LEISURE", "Leisure Centre", 70, 38, false),
            new("S_COLLEGE", "Technical College", 54, 6, false),
            new("S_AIRPORT", "Airport Road", 78, 46, false),
            new("S_MOOR", "Moor Lane", 70, 14, false),
            new("S_CHURCH", "St Mary's Church", 38, 22, false),
            new("S_QUAY", "Quayside", 30, 54, false),
            new("S_WOOD", "Woodland Corner", 78, 54, false)
        };

        StopsList = stops;
        StopLookup = stops.ToDictionary(s => s.Id, StringComparer.Ordinal);
        Terminuses = stops.Where(s => s.IsTerminus).ToArray();

        Routes = new List<Route>
        {
            Make("1A", "Timber Run", "The old timber delivery, reborn as a works service.", true, 0xFF8D6E63,
                "T_BUS", "S_HIGH", "S_TIMBER", "S_COAL", "T_IND"),
            Make("1B", "Coal Run", "Coal Yard to the docks, calling at the market and cathedral.", true, 0xFF546E7A,
                "T_IND", "S_COAL", "S_MARKET", "S_CHURCH", "S_OIL", "T_DOCKS"),
            Make("1C", "Oil Run", "Tanker crews and shift workers between docks and river.", true, 0xFF5C6BC0,
                "T_DOCKS", "S_OIL", "S_STATION", "T_RIVER"),
            Make("1D", "Fruit & Veg Run", "Market garden produce and passengers to the bus station.", true, 0xFF66BB6A,
                "T_RIVER", "S_QUAY", "S_FRUIT", "S_HARBOUR", "T_BUS"),

            Make("2", "High Street Hopper", "The town centre shopper, four stops end to end.", false, 0xFFEF5350,
                "T_BUS", "S_HARBOUR", "S_HIGH", "S_MARKET", "T_UNI"),
            Make("3", "Dockside Loop", "Quayside and market traffic around the old harbour.", false, 0xFF26A69A,
                "T_DOCKS", "S_HARBOUR", "S_FRUIT", "S_STATION", "S_QUAY", "T_RIVER"),
            Make("4", "Hospital Express", "The quickest way from the bus station to St Cuthbert's.", false, 0xFF42A5F5,
                "T_BUS", "S_STATION", "S_PARK", "S_LEISURE", "T_HOSP"),
            Make("5", "University Circular", "Students, colleges and the cathedral quarter.", false, 0xFFAB47BC,
                "T_UNI", "S_COLLEGE", "S_CATH", "S_MARKET", "T_NORTH"),
            Make("6", "Market Cross", "A straight run up the High Street to Northgate.", false, 0xFFFFA726,
                "T_BUS", "S_HIGH", "S_MARKET", "S_CHURCH", "T_NORTH"),
            Make("7", "Riverside Ramble", "A slow, pretty run along the river to the hospital.", false, 0xFF29B6F6,
                "T_RIVER", "S_STATION", "S_PARK", "S_LEISURE", "T_HOSP"),
            Make("8", "Industrial Estate", "Works buses for the mill and the trading estates.", false, 0xFF8D6E63,
                "T_IND", "S_COAL", "S_MILL", "S_MOOR", "T_NORTH"),
            Make("9", "Park & Ride", "Leave the car at Northgate and ride into town.", false, 0xFF9CCC65,
                "T_NORTH", "S_MOOR", "S_PARK", "S_STATION", "T_BUS"),
            Make("10", "Night Bus N1", "The last bus home, after the pubs have shut.", false, 0xFF7E57C2,
                "T_BUS", "S_HIGH", "S_CHURCH", "S_CATH", "T_UNI"),
            Make("11", "Airport Link", "Hospital, leisure centre, airport and the racecourse.", false, 0xFF26C6DA,
                "T_HOSP", "S_LEISURE", "S_AIRPORT", "S_WOOD", "T_RACE"),
            Make("12", "Cathedral Climb", "Up through the market and the cathedral to Northgate.", false, 0xFFFFCA28,
                "T_DOCKS", "S_MARKET", "S_CATH", "S_MOOR", "T_NORTH"),
            Make("13", "Harbour Flyer", "A short, busy shuttle along the waterfront.", false, 0xFFEC407A,
                "T_RIVER", "S_QUAY", "S_HARBOUR", "S_FRUIT", "T_DOCKS")
        };
    }

    public static IReadOnlyList<BusStop> Stops => StopsList;

    public static IReadOnlyList<BusStop> Terminuses { get; }

    public static IReadOnlyList<Route> Routes { get; }

    public static Route Get(string number) =>
        Routes.FirstOrDefault(r => string.Equals(r.Number, number, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"No route '{number}'");

    public static BusStop Stop(string id) =>
        StopLookup.TryGetValue(id, out var stop)
            ? stop
            : throw new KeyNotFoundException($"No stop '{id}'");

    private static Route Make(
        string number,
        string name,
        string description,
        bool heritage,
        uint colour,
        params string[] stopIds)
    {
        var stops = stopIds.Select(Stop).ToArray();
        return new Route(number, name, description, heritage, colour, stops);
    }
}
