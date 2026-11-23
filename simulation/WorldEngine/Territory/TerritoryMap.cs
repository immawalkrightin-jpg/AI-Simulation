using System.Numerics;
using WorldEngine.Countries;
using WorldEngine.Units;

namespace WorldEngine.Territory;

public sealed class TerritoryMap
{
    private const short SeaCell = 0;
    private const short FreeLandCell = 1;
    private const short FirstOwnerCell = 2;

    private readonly short[] cells;
    private readonly string[] owners;

    public int Columns { get; }
    public int Rows { get; }
    public float CellSize { get; }
    public float Width => Columns * CellSize;
    public float Height => Rows * CellSize;

    public TerritoryMap(int columns, int rows, float cellSize, short[] cells, IReadOnlyList<string> owners)
    {
        if (columns <= 0 || rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }
        if (cellSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }
        if (cells.Length != columns * rows)
        {
            throw new ArgumentException("Cell count does not match the grid size.", nameof(cells));
        }
        Columns = columns;
        Rows = rows;
        CellSize = cellSize;
        this.cells = cells;
        this.owners = owners.ToArray();
    }

    public bool Contains(Vector2 point) =>
        point.X >= 0 && point.Y >= 0 && point.X < Width && point.Y < Height;

    public bool IsSea(Vector2 point) => Contains(point) && cells[IndexOf(point)] == SeaCell;

    public bool IsLand(Vector2 point) => Contains(point) && cells[IndexOf(point)] != SeaCell;

    public CountryId? OwnerAt(Vector2 point)
    {
        if (!Contains(point))
        {
            return null;
        }
        var value = cells[IndexOf(point)];
        return value >= FirstOwnerCell ? new CountryId(owners[value - FirstOwnerCell]) : null;
    }

    public bool Permits(Domain domain, CountryId owner, Vector2 point) =>
        Contains(point) && Allows(cells[IndexOf(point)], domain, owner);

    public bool SegmentPermits(Domain domain, CountryId owner, Vector2 from, Vector2 to)
    {
        if (!Contains(from) || !Contains(to))
        {
            return false;
        }
        var startX = from.X / (double)CellSize;
        var startY = from.Y / (double)CellSize;
        var finishX = to.X / (double)CellSize;
        var finishY = to.Y / (double)CellSize;
        var x = Math.Min(Columns - 1, (int)Math.Floor(startX));
        var y = Math.Min(Rows - 1, (int)Math.Floor(startY));
        var endX = Math.Min(Columns - 1, (int)Math.Floor(finishX));
        var endY = Math.Min(Rows - 1, (int)Math.Floor(finishY));
        var deltaX = finishX - startX;
        var deltaY = finishY - startY;
        var stepX = Math.Sign(deltaX);
        var stepY = Math.Sign(deltaY);
        var nextX = stepX == 0 ? double.PositiveInfinity : ((stepX > 0 ? x + 1 : x) - startX) / deltaX;
        var nextY = stepY == 0 ? double.PositiveInfinity : ((stepY > 0 ? y + 1 : y) - startY) / deltaY;
        var strideX = stepX == 0 ? double.PositiveInfinity : stepX / deltaX;
        var strideY = stepY == 0 ? double.PositiveInfinity : stepY / deltaY;
        var budget = Math.Abs(endX - x) + Math.Abs(endY - y) + 1;
        for (var i = 0; i <= budget; i++)
        {
            if (!Allows(cells[y * Columns + x], domain, owner))
            {
                return false;
            }
            if (x == endX && y == endY)
            {
                return true;
            }
            if (nextX < nextY)
            {
                x = Math.Clamp(x + stepX, 0, Columns - 1);
                nextX += strideX;
            }
            else
            {
                y = Math.Clamp(y + stepY, 0, Rows - 1);
                nextY += strideY;
            }
        }
        return Allows(cells[endY * Columns + endX], domain, owner);
    }

    public Vector2? FindNearest(Domain domain, CountryId owner, Vector2 origin, float maxDistance)
    {
        if (!Contains(origin))
        {
            return null;
        }
        var originColumn = Math.Min(Columns - 1, (int)(origin.X / CellSize));
        var originRow = Math.Min(Rows - 1, (int)(origin.Y / CellSize));
        var maxRing = (int)Math.Ceiling(maxDistance / CellSize) + 1;
        Vector2? best = null;
        var bestDistance = float.MaxValue;
        var foundRing = -1;
        for (var ring = 0; ring <= maxRing; ring++)
        {
            if (foundRing >= 0 && ring > foundRing + 1)
            {
                break;
            }
            for (var row = originRow - ring; row <= originRow + ring; row++)
            {
                if (row < 0 || row >= Rows)
                {
                    continue;
                }
                for (var column = originColumn - ring; column <= originColumn + ring; column++)
                {
                    if (column < 0 || column >= Columns)
                    {
                        continue;
                    }
                    if (Math.Max(Math.Abs(column - originColumn), Math.Abs(row - originRow)) != ring)
                    {
                        continue;
                    }
                    if (!Allows(cells[row * Columns + column], domain, owner))
                    {
                        continue;
                    }
                    var center = new Vector2((column + 0.5f) * CellSize, (row + 0.5f) * CellSize);
                    var distance = Vector2.Distance(center, origin);
                    if (distance > maxDistance || distance >= bestDistance)
                    {
                        continue;
                    }
                    best = center;
                    bestDistance = distance;
                }
            }
            if (best is not null && foundRing < 0)
            {
                foundRing = ring;
            }
        }
        return best;
    }

    private int IndexOf(Vector2 point)
    {
        var column = Math.Min(Columns - 1, (int)(point.X / CellSize));
        var row = Math.Min(Rows - 1, (int)(point.Y / CellSize));
        return row * Columns + column;
    }

    private bool IsOwnedBy(short value, CountryId owner) =>
        string.Equals(owners[value - FirstOwnerCell], owner.Value, StringComparison.Ordinal);

    private bool Allows(short value, Domain domain, CountryId owner) => domain switch
    {
        Domain.Sea => value == SeaCell,
        Domain.Land => value >= FirstOwnerCell && IsOwnedBy(value, owner),
        _ => value < FirstOwnerCell || IsOwnedBy(value, owner)
    };
}
