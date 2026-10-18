namespace AshenReliquary.Core.World;

/// <summary>A* over the tile grid, 8 directions, no cutting past blocked corners.</summary>
public static class Pathfinder
{
    private static readonly (int Dx, int Dy, double Cost)[] Steps =
    [
        (1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1),
        (1, 1, Math.Sqrt(2)), (1, -1, Math.Sqrt(2)), (-1, 1, Math.Sqrt(2)), (-1, -1, Math.Sqrt(2)),
    ];

    /// <summary>
    /// Cell centres from the start cell (exclusive) to the goal cell (inclusive), or null when the goal can't be
    /// reached within <paramref name="maxNodes"/> expansions. A blocked goal is swapped for its nearest open neighbour.
    /// </summary>
    public static List<(double X, double Y)>? Find(TileMap map, (int X, int Y) start, (int X, int Y) goal, int maxNodes = 4000)
    {
        if (map.IsBlocked(goal.X, goal.Y))
        {
            var open = Steps.Select(s => (X: goal.X + s.Dx, Y: goal.Y + s.Dy))
                .Where(c => !map.IsBlocked(c.X, c.Y))
                .OrderBy(c => Math.Abs(c.X - start.X) + Math.Abs(c.Y - start.Y))
                .Cast<(int X, int Y)?>()
                .FirstOrDefault();
            if (open is null)
                return null;
            goal = open.Value;
        }
        if (start == goal)
            return [];

        var frontier = new PriorityQueue<(int X, int Y), double>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var cost = new Dictionary<(int, int), double> { [start] = 0 };
        frontier.Enqueue(start, 0);
        var expanded = 0;

        while (frontier.TryDequeue(out var current, out _))
        {
            if (current == goal)
                return Rebuild(cameFrom, start, goal);
            if (++expanded > maxNodes)
                return null;
            foreach (var (dx, dy, stepCost) in Steps)
            {
                var next = (X: current.X + dx, Y: current.Y + dy);
                if (map.IsBlocked(next.X, next.Y))
                    continue;
                if (dx != 0 && dy != 0 && (map.IsBlocked(current.X + dx, current.Y) || map.IsBlocked(current.X, current.Y + dy)))
                    continue;
                var newCost = cost[current] + stepCost * map.StepCost(next.X, next.Y);
                if (cost.TryGetValue(next, out var old) && old <= newCost)
                    continue;
                cost[next] = newCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, newCost + Octile(next, goal));
            }
        }
        return null;
    }

    private static double Octile((int X, int Y) a, (int X, int Y) b)
    {
        int dx = Math.Abs(a.X - b.X), dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) + (Math.Sqrt(2) - 1) * Math.Min(dx, dy);
    }

    private static List<(double X, double Y)> Rebuild(Dictionary<(int, int), (int, int)> cameFrom, (int X, int Y) start, (int X, int Y) goal)
    {
        var path = new List<(double X, double Y)>();
        var at = goal;
        while (at != start)
        {
            path.Add((at.X + 0.5, at.Y + 0.5));
            at = cameFrom[at];
        }
        path.Reverse();
        return path;
    }
}
