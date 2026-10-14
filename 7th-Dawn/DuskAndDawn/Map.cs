using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class NightMap
    {
        public int Columns { get; }
        public int Rows { get; }

        public MapNode[,] Grid { get; }
        public List<MapNode> Nodes { get; } = new List<MapNode>();

        public MapNode Entrance { get; private set; }
        public MapNode Hoard { get; private set; }
        public int MaxDepth { get; private set; }

        private static readonly (int dx, int dy)[] Directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        public NightMap(RoomGenerator roomGenerator, Random random, int columns, int rows)
        {
            Columns = columns;
            Rows = rows;
            Grid = new MapNode[columns, rows];

            for (int attempt = 0; attempt < 12; attempt++)
            {
                float rubbleChance = attempt < 11 ? 0.16f : 0f;
                if (TryCarve(random, rubbleChance)) break;
            }

            AddLoops(random);
            AssignDepths();
            AssignRoomTypes(roomGenerator);
        }

        public MapNode At(int column, int row) =>
            column >= 0 && column < Columns && row >= 0 && row < Rows ? Grid[column, row] : null;

        private bool TryCarve(Random random, float rubbleChance)
        {
            Array.Clear(Grid, 0, Grid.Length);
            Nodes.Clear();

            var open = new bool[Columns, Rows];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    open[x, y] = random.NextDouble() >= rubbleChance;

            int entranceRow = random.Next(Rows);
            open[0, entranceRow] = true;

            var stack = new Stack<MapNode>();
            Entrance = CreateNode(0, entranceRow, random);
            stack.Push(Entrance);

            while (stack.Count > 0)
            {
                var node = stack.Peek();
                var options = new List<(int x, int y)>();
                foreach (var (dx, dy) in Directions)
                {
                    int nx = node.Column + dx, ny = node.Row + dy;
                    if (nx < 0 || nx >= Columns || ny < 0 || ny >= Rows) continue;
                    if (!open[nx, ny] || Grid[nx, ny] != null) continue;
                    options.Add((nx, ny));
                }

                if (options.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var (cx, cy) = options[random.Next(options.Count)];
                var next = CreateNode(cx, cy, random);
                node.Link(next);
                stack.Push(next);
            }

            bool reachesFarSide = Nodes.Any(n => n.Column >= Columns - 2);
            return Nodes.Count >= Columns * Rows * 0.7f && reachesFarSide;
        }

        private MapNode CreateNode(int x, int y, Random random)
        {
            var node = new MapNode(RoomType.Empty, x, y, (float)(random.NextDouble() * MathF.PI * 2f));
            Grid[x, y] = node;
            Nodes.Add(node);
            return node;
        }

        private void AddLoops(Random random)
        {
            int loops = Math.Max(2, Nodes.Count / 9);
            for (int attempt = 0; attempt < 60 && loops > 0; attempt++)
            {
                var node = Nodes[random.Next(Nodes.Count)];
                var (dx, dy) = Directions[random.Next(Directions.Length)];
                var neighbor = At(node.Column + dx, node.Row + dy);
                if (neighbor == null || node.Links.Contains(neighbor)) continue;

                node.Link(neighbor);
                loops--;
            }
        }

        private void AssignDepths()
        {
            foreach (var node in Nodes) node.Depth = int.MaxValue;
            Entrance.Depth = 0;
            var queue = new Queue<MapNode>();
            queue.Enqueue(Entrance);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var link in node.Links)
                {
                    if (link.Depth != int.MaxValue) continue;
                    link.Depth = node.Depth + 1;
                    queue.Enqueue(link);
                }
            }

            Hoard = Nodes.OrderByDescending(n => n.Depth).First();
            MaxDepth = Hoard.Depth;
        }

        private void AssignRoomTypes(RoomGenerator roomGenerator)
        {
            foreach (var node in Nodes)
            {
                if (node == Entrance) node.Type = RoomType.Entrance;
                else if (node == Hoard) node.Type = RoomType.Hoard;
                else node.Type = roomGenerator.PickNext(node.Depth, node.IsDeadEnd);
            }

            foreach (var first in Entrance.Links)
            {
                if (first.Type == RoomType.Encounter) first.Type = RoomType.Empty;
            }
        }

        public List<MapNode> FindKnownPath(MapNode start, MapNode target)
        {
            if (start == null || target == null || start == target) return null;

            var cameFrom = new Dictionary<MapNode, MapNode> { { start, null } };
            var queue = new Queue<MapNode>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == target) break;
                foreach (var link in node.Links)
                {
                    if (cameFrom.ContainsKey(link)) continue;
                    if (link != target && !link.Visited) continue;
                    cameFrom[link] = node;
                    queue.Enqueue(link);
                }
            }

            if (!cameFrom.ContainsKey(target)) return null;

            var path = new List<MapNode>();
            for (var step = target; step != start; step = cameFrom[step]) path.Add(step);
            path.Reverse();
            return path;
        }

        public Dictionary<MapNode, int> StepsWithin(MapNode start, int maxSteps)
        {
            var result = new Dictionary<MapNode, int> { { start, 0 } };
            var queue = new Queue<MapNode>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int steps = result[node];
                if (steps >= maxSteps) continue;
                foreach (var link in node.Links)
                {
                    if (result.ContainsKey(link)) continue;
                    result[link] = steps + 1;
                    queue.Enqueue(link);
                }
            }
            return result;
        }
    }
}
