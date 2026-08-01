using System;
using System.Collections.Generic;

namespace InkFlow.Core
{
    /// <summary>Звіт солвера про один рівень.</summary>
    public readonly struct SolveReport
    {
        public bool Solvable { get; }

        /// <summary>Мінімальна кількість ходів до перемоги. З неї виводяться пороги 2★/3★.</summary>
        public int MinMoves { get; }

        /// <summary>Скільки станів довелось переглянути — груба міра складності рівня.</summary>
        public int StatesExplored { get; }

        /// <summary>Розв'язок (послідовність ходів), якщо знайдено.</summary>
        public IReadOnlyList<ReplayMove> Solution { get; }

        /// <summary>Пошук зупинився через ліміт станів — «не довів», а не «нерозв'язний».</summary>
        public bool Exhausted { get; }

        public SolveReport(bool solvable, int minMoves, int statesExplored,
            IReadOnlyList<ReplayMove> solution, bool exhausted)
        {
            Solvable = solvable;
            MinMoves = minMoves;
            StatesExplored = statesExplored;
            Solution = solution;
            Exhausted = exhausted;
        }
    }

    /// <summary>
    /// Пошук у ширину по станах сітки на РЕАЛЬНОМУ коді Core, а не на копії правил (§15).
    /// Доводить, що рівень розв'язний у межах ходів, і знаходить мінімальну кількість ходів.
    ///
    /// Нерозв'язний рівень не має права потрапити в збірку — це перевіряється тестом,
    /// а не очима (§14.5, §18.6).
    ///
    /// BFS коректний саме тому, що правила детерміновані: той самий стан + той самий хід
    /// завжди дають той самий результат (§6).
    /// </summary>
    public static class LevelSolver
    {
        public const int DefaultStateLimit = 200_000;

        public static SolveReport Solve(LevelData level, BalanceData balance, int stateLimit = DefaultStateLimit)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (balance == null) throw new ArgumentNullException(nameof(balance));

            var start = new PuzzleSession(level, balance);
            if (start.State == GameState.Won)
                return new SolveReport(true, 0, 0, Array.Empty<ReplayMove>(), false);

            var visited = new HashSet<long> { start.Grid.StateHash() };
            var queue = new Queue<Node>();
            queue.Enqueue(new Node(Snapshot(start.Grid), null, default, 0));

            var explored = 0;

            while (queue.Count > 0)
            {
                if (explored >= stateLimit)
                    return new SolveReport(false, -1, explored, Array.Empty<ReplayMove>(), exhausted: true);

                var node = queue.Dequeue();
                explored++;

                if (node.Depth >= level.MaxMoves)
                    continue;

                foreach (var move in EnumerateMoves(node.Cells, level))
                {
                    // Кожен хід програється в чистій сесії з початку — детермінізм гарантує,
                    // що відтворення шляху дасть рівно той самий стан.
                    var session = new PuzzleSession(level, balance);
                    Restore(session.Grid, node.Cells);

                    var result = session.ApplyMove(move.From, move.To);
                    if (!result.Accepted)
                        continue;

                    if (session.State == GameState.Won)
                        return new SolveReport(true, node.Depth + 1, explored,
                            BuildPath(node, move), false);

                    if (session.State != GameState.Playing)
                        continue; // Lost або Deadlock — гілка мертва

                    var hash = session.Grid.StateHash();
                    if (!visited.Add(hash))
                        continue;

                    queue.Enqueue(new Node(Snapshot(session.Grid), node, move, node.Depth + 1));
                }
            }

            return new SolveReport(false, -1, explored, Array.Empty<ReplayMove>(), false);
        }

        private static IEnumerable<ReplayMove> EnumerateMoves(Cell[] cells, LevelData level)
        {
            var grid = new GridModel(level.Width, level.Height);
            Restore(grid, cells);

            foreach (var from in grid.AllPositions())
            {
                for (var i = 0; i < GridPos.NeighborCount; i++)
                {
                    var to = from.NeighborAt(i);
                    if (MoveValidator.IsValidMove(grid, from, to))
                        yield return new ReplayMove(from, to);
                }
            }
        }

        private static Cell[] Snapshot(GridModel grid)
        {
            var cells = new Cell[grid.Width * grid.Height];
            var index = 0;
            foreach (var pos in grid.AllPositions())
                cells[index++] = grid[pos];
            return cells;
        }

        private static void Restore(GridModel grid, Cell[] cells)
        {
            var index = 0;
            foreach (var pos in grid.AllPositions())
                grid[pos] = cells[index++];
        }

        private static IReadOnlyList<ReplayMove> BuildPath(Node node, ReplayMove last)
        {
            var path = new List<ReplayMove> { last };
            var current = node;
            while (current?.Parent != null)
            {
                path.Add(current.Move);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        private sealed class Node
        {
            public readonly Cell[] Cells;
            public readonly Node? Parent;
            public readonly ReplayMove Move;
            public readonly int Depth;

            public Node(Cell[] cells, Node? parent, ReplayMove move, int depth)
            {
                Cells = cells;
                Parent = parent;
                Move = move;
                Depth = depth;
            }
        }
    }
}
