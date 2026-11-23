using System.Text.Json;

namespace WorldEngine.Territory;

public static class TerritoryLoader
{
    public const float DefaultCellSize = 2f;

    private static readonly Lazy<TerritoryMap?> shared = new(Locate);

    public static TerritoryMap? Shared => shared.Value;

    public static TerritoryMap FromJson(string json, float cellSize = DefaultCellSize)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var width = root.GetProperty("width").GetSingle();
        var height = root.GetProperty("height").GetSingle();
        var columns = (int)Math.Round(width / cellSize);
        var rows = (int)Math.Round(height / cellSize);
        var cells = new short[columns * rows];
        var owners = new List<string>();
        foreach (var entity in root.GetProperty("entities").EnumerateArray())
        {
            var id = entity.GetProperty("id").GetString() ?? string.Empty;
            short value = 1;
            if (id.Length > 0)
            {
                owners.Add(id);
                value = (short)(owners.Count + 1);
            }
            foreach (var ring in entity.GetProperty("rings").EnumerateArray())
            {
                var coordinates = ring.EnumerateArray().Select(v => v.GetDouble()).ToArray();
                Fill(cells, columns, rows, cellSize, coordinates, value);
            }
        }
        return new TerritoryMap(columns, rows, cellSize, cells, owners);
    }

    private static void Fill(short[] cells, int columns, int rows, float cellSize, double[] ring, short value)
    {
        var count = ring.Length / 2;
        if (count < 3)
        {
            return;
        }
        var crossings = new List<double>();
        for (var row = 0; row < rows; row++)
        {
            var y = (row + 0.5) * cellSize;
            crossings.Clear();
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var x1 = ring[i * 2];
                var y1 = ring[i * 2 + 1];
                var x2 = ring[next * 2];
                var y2 = ring[next * 2 + 1];
                if ((y1 <= y && y < y2) || (y2 <= y && y < y1))
                {
                    crossings.Add(x1 + (y - y1) / (y2 - y1) * (x2 - x1));
                }
            }
            crossings.Sort();
            for (var k = 0; k + 1 < crossings.Count; k += 2)
            {
                var first = Math.Max(0, (int)Math.Ceiling(crossings[k] / cellSize - 0.5));
                var last = Math.Min(columns - 1, (int)Math.Ceiling(crossings[k + 1] / cellSize - 0.5) - 1);
                for (var column = first; column <= last; column++)
                {
                    var index = row * columns + column;
                    if (value == 1)
                    {
                        if (cells[index] == 0)
                        {
                            cells[index] = 1;
                        }
                    }
                    else
                    {
                        cells[index] = value;
                    }
                }
            }
        }
    }

    private static TerritoryMap? Locate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "frontend", "assets", "maps", "coldwar.json");
            if (File.Exists(candidate))
            {
                return FromJson(File.ReadAllText(candidate));
            }
        }
        return null;
    }
}
