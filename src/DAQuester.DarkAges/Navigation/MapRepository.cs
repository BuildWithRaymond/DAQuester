using DAQuester.Core;
using System.Text.Json;
using System.Text;
using System.Buffers.Binary;

namespace DAQuester.DarkAges.Navigation;

public record Portal(Tile From, int Map, Tile To, bool WorldMap = false);
public record MapTransfer(Tile From, Tile To);
public record MapRecord(int Id, string Name, int Width, int Height, Portal[] Portals)
{
    public MapTransfer[] InternalTransfers { get;
        init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<Tile> ExitTiles => Portals.Select(p => p.From).Concat(ShinewoodFaerieEvidence.ReturnExits.Where(e => e.SourceMap == Id).Select(e => e.From)).Distinct();
}

public partial class MapRepository(string clientDirectory, string routesPath, string serverProfile = "datester-7.41")
{
    readonly string collisionProfile = serverProfile, routeFile = routesPath;
    readonly Dictionary<int, MapRecord> records = (JsonSerializer.Deserialize<MapRecord[]>(File.ReadAllText(routesPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []).ToDictionary(x => x.Id);
    readonly Dictionary<int, Grid> grids = [];
    public Grid Load(int id)
    {
        if (grids.TryGetValue(id, out var cached))
            return cached;
        var r = Record(id);
        var paths = new List<string>
        {
            Path.Combine(clientDirectory, "maps", "lod" + id + ".map")
        };
        var drive = Path.GetPathRoot(clientDirectory);
        if (drive is not null)
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VirtualStore", clientDirectory[drive.Length..], "maps", "lod" + id + ".map"));
        string? file = paths.FirstOrDefault(File.Exists);
        if (file is null)
            return LoadPackaged(id) ?? throw new FileNotFoundException("Missing collision data for " + MapLabel(id) + ". This server profile has no packaged grid; visit the map manually to load client assets.");
        var map = File.ReadAllBytes(file);
        if (map.Length < r.Width * r.Height * 6)
            throw new InvalidDataException("Truncated map asset.");
        string archive = Path.Combine(clientDirectory, "ia.dat");
        if (!File.Exists(archive))
            return LoadPackaged(id) ?? throw new FileNotFoundException("Missing client collision table for " + MapLabel(id) + ". This server profile has no packaged grid.");
        var sotp = ReadSotp(archive);
        bool Wall(ushort sprite) => sprite != 0 && (sprite > sotp.Length || (sotp[sprite - 1] & 15) == 15);
        bool Blocked(int x, int y)
        {
            if (id == 97 && x == 46 && y == 21)
                return false;
            int o = (y * r.Width + x) * 6;
            return Wall(BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(o + 2))) || Wall(BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan(o + 4)));
        }

        var result = new Grid(r.Width, r.Height, Blocked);
        grids[id] = result;
        return result;
    }

    public static byte[] ReadSotp(string ia)
    {
        using var reader = new BinaryReader(File.OpenRead(ia));
        int count = reader.ReadInt32() - 1;
        if (count < 1 || count > 100000)
            throw new InvalidDataException("Invalid ia.dat directory.");
        for (int i = 0; i < count; i++)
        {
            int offset = reader.ReadInt32();
            string name = Encoding.ASCII.GetString(reader.ReadBytes(13)).TrimEnd('\0');
            int end = reader.ReadInt32();
            if (name == "sotp.dat")
            {
                if (offset < 0 || end <= offset || end > reader.BaseStream.Length)
                    throw new InvalidDataException("Invalid sotp.dat bounds.");
                reader.BaseStream.Position = offset;
                return reader.ReadBytes(end - offset);
            }

            reader.BaseStream.Position -= 4;
        }

        throw new InvalidDataException("sotp.dat not found in client ia.dat.");
    }

    public IReadOnlyList<Portal> Route(int from, int destination)
    {
        if (from == destination)
            return [];
        var queue = new Queue<int>();
        queue.Enqueue(from);
        var parents = new Dictionary<int, (int Map, Portal Via)>();
        var visited = new HashSet<int>
        {
            from
        };
        while (queue.TryDequeue(out int map))
        {
            if (!records.TryGetValue(map, out var r))
                continue;
            foreach (var p in r.Portals)
            {
                if (!visited.Add(p.Map))
                    continue;
                parents[p.Map] = (map, p);
                if (p.Map == destination)
                {
                    var path = new List<Portal>();
                    int n = destination;
                    while (n != from)
                    {
                        var prev = parents[n];
                        path.Add(prev.Via);
                        n = prev.Map;
                    }

                    path.Reverse();
                    return path;
                }

                queue.Enqueue(p.Map);
            }
        }

        throw new InvalidOperationException("No recorded portal route from " + from + " to " + destination + ". Record a route.");
    }

    public MapRecord Record(int id) => records.TryGetValue(id, out var r) ? r : throw new InvalidOperationException("Map " + id + " has no recorded dimensions or portals.");
    string MapLabel(int id) => records.TryGetValue(id, out var r) && !string.IsNullOrWhiteSpace(r.Name) ? r.Name + " (" + id + ")" : "Map " + id;
    public MapTransfer? TransferTo(int map, Tile start, Tile target, ISet<Tile>? obstacles = null, CancellationToken ct = default)
    {
        if (Record(map).InternalTransfers.Length == 0)
            return null;
        var step = Plan(map, start, map, target, obstacles, ct).Steps.FirstOrDefault();
        return step is null ? null : new(step.From, step.To);
    }

    public Arbiter.Net.Server.Types.ServerWorldMapNode WorldDestination(int destination, IEnumerable<Arbiter.Net.Server.Types.ServerWorldMapNode> nodes)
    {
        var reachable = new List<(Arbiter.Net.Server.Types.ServerWorldMapNode Node, int Distance)>();
        foreach (var node in nodes)
            try
            {
                reachable.Add((node, Route(node.MapId, destination).Count));
            }
            catch (InvalidOperationException)
            {
            }

        return reachable.OrderBy(x => x.Distance).Select(x => x.Node).FirstOrDefault() ?? throw new QuestBlockedException("World-map route unavailable. Record a route to the destination.");
    }
}
