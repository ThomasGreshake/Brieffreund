//Copyright Thomas Greshake 2026

namespace Brieffreund.Eulermap
{
    internal interface INodeConnectionCreator
    {
        public float CreateConnections(EulerNode node, out int[] connections);

        public float CreateConnections(EulerNode node, bool?[] leftTable);

        public float CreateConnectionsDeadEnd(EulerNode node, out int[] connections, out int[] opposite);

        public float EvaluateOptionScore(EulerNode node);

        public float EvaluateOptionScore(EulerNode node, int[] currentConnections);

        internal int[][] CreateConnections(IEulerMap map, out float[] nodeScores)
        {
            int[][] connections = new int[map.NodeCount][];
            nodeScores = new float[map.NodeCount];

            foreach (EulerNode node in map.Nodes.Values)
            {
                nodeScores[node.Index] = CreateConnections(node, out int[] cons);
                connections[node.Index] = cons;
            }

            return connections;
        }
    }
}

namespace Brieffreund.Eulermap.Functions
{
    internal class NodeConnectionCreator : INodeConnectionCreator
    {
        internal NodeConnectionCreator()
        { }

        public float CreateConnections(EulerNode node, out int[] connections)
        {
            if (node.Paths.All(p => p.IsRouteDeadEnd))
            {
                return CreateConnectionsDeadEnd(node, out connections, out _, p => p.FromLeft, p => p.ToLeft, false);
            }

            return CreateConnections(node, out connections, p => p.FromLeft, p => p.ToLeft, false);
        }

        public float CreateConnections(EulerNode node, bool?[] leftTable)
        {
            if (node.Paths.All(p => p.IsRouteDeadEnd))
            {
                return CreateConnectionsDeadEnd(node, out _, out _, p => leftTable[p.Index * 2], p => leftTable[p.Index * 2 + 1], true);
            }

            return CreateConnections(node, out _, p => leftTable[p.Index * 2], p => leftTable[p.Index * 2 + 1], true);
        }

        public float CreateConnectionsDeadEnd(EulerNode node, out int[] connections, out int[] opposite) =>
            CreateConnectionsDeadEnd(node, out connections, out opposite, p => p.FromLeft, p => p.ToLeft, false);

        public float EvaluateOptionScore(EulerNode node, int[] currentConnections)
        {
            List<int[]> connections = GetConnections(currentConnections);
            float best = float.MaxValue;
            foreach (int[] c in connections)
            {
                float score = GetPassingDistance(node, node.Pathways, c, p => p.FromLeft, p => p.ToLeft, false);
                if (score < best)
                {
                    best = score;
                }
            }
            return best;
        }

        public float EvaluateOptionScore(EulerNode node)
        {
            int[] current = GetEmpty(node.Count);
            return EvaluateOptionScore(node, current);
        }

        private float CreateConnections(EulerNode node, out int[] connections,
            Func<EulerPath, bool?> getLeftAtStart, Func<EulerPath, bool?> getLeftAtEnd, bool sortPathways)
        {
            IList<EulerPathway> pathways = GetPathways(node, sortPathways, getLeftAtStart, getLeftAtEnd);

            List<int[]> cons = GetConnections(node.Count);

            float best = float.MaxValue;
            connections = cons[0];
            foreach (int[] c in cons)
            {
                float score = GetPassingDistance(node, pathways, c, getLeftAtStart, getLeftAtEnd);
                if (score < best)
                {
                    best = score;
                    connections = c;
                }
            }
            return best;
        }

        private static Dictionary<int, List<int[]>> _precalc = new(11);

        private static List<int[]> GetConnections(int count)
        {
            lock (_precalc)
            {
                if (_precalc.TryGetValue(count, out List<int[]>? list))
                {
                    return list;
                }

                list = GetConnections(GetEmpty(count));
                _precalc.Add(count, list);
                return list;
            }
        }

        private static List<int[]> GetConnections(int[] start)
        {
            int count = start.Length;
            List<int[]> open = GetOpenList(start);
            List<int[]> closed = new List<int[]>();

            while (open.Count > 0)
            {
                int index = open.Count - 1;
                int[] current = open[index];
                open.RemoveAt(index);
                int c = current.Count(i => i == -1);
                if (c == 0)
                {
                    closed.Add(current);
                    continue;
                }

                index = -1;
                for (int i = 0; i < count; i++)
                {
                    if (current[i] != -1)
                    {
                        continue;
                    }

                    if (index == -1)
                    {
                        index = i;
                        continue;
                    }

                    int[] copy = new int[count];
                    Array.Copy(current, copy, current.Length);

                    copy[i] = index;
                    copy[index] = i;

                    if (c == 2)
                    {
                        closed.Add(copy);
                    }
                    else
                    {
                        open.Add(copy);
                    }
                }
            }

            return closed;
        }

        private static List<int[]> GetOpenList(int[] start)
        {
            List<int[]> open = new(10);
            if (start.Length % 2 == 0)
            {
                open.Add(start);
            }
            else
            {
                for (int i = 0; i < start.Length; i++)
                {
                    if (start[i] == -1)
                    {
                        int[] copy = new int[start.Length];
                        Array.Copy(start, copy, start.Length);
                        copy[i] = i;
                        open.Add(copy);
                    }
                }
            }
            return open;
        }

        private static int[] GetEmpty(int count)
        {
            int[] empty = new int[count];
            for (int i = 0; i < count; i++)
            {
                empty[i] = -1;
            }
            return empty;
        }

        private IList<EulerPathway> GetPathways(EulerNode node, bool sort, Func<EulerPath, bool?> getLeftAtStart, Func<EulerPath, bool?> getLeftAtEnd)
        {
            if (sort)
            {
                List<EulerPathway> ps = node.Pathways.ToList();
                ps.Sort((a, b) =>
                    Comparer<float>.Default.Compare(GetSortScore(a, getLeftAtStart, getLeftAtEnd), GetSortScore(b, getLeftAtStart, getLeftAtEnd)));
                return ps;
            }
            else
            {
                return node.Pathways;
            }
        }

        private float CreateConnectionsDeadEnd(EulerNode node, out int[] connections, out int[] opposite,
            Func<EulerPath, bool?> getLeftAtStart, Func<EulerPath, bool?> getLeftAtEnd, bool sortPathways)
        {
            IList<EulerPathway> pathways = GetPathways(node, sortPathways, getLeftAtStart, getLeftAtEnd);

            int count = node.Count;
            int[] counterClockwise = new int[count];
            int[] clockwise = new int[count];

            for (int j = 0; j < count; j++)
            {
                int next = (j + 1) % count;
                if (j % 2 == 0)
                {
                    counterClockwise[j] = next;
                    counterClockwise[next] = j;
                }
                else
                {
                    clockwise[j] = next;
                    clockwise[next] = j;
                }
            }

            if (count % 2 == 0)
            {
                float counter = GetPassingDistance(node, pathways, counterClockwise, getLeftAtStart, getLeftAtEnd);
                float clock = GetPassingDistance(node, pathways, clockwise, getLeftAtStart, getLeftAtEnd);

                if (counter < clock)
                {
                    connections = counterClockwise;
                    opposite = clockwise;
                    return counter;
                }

                connections = clockwise;
                opposite = counterClockwise;
                return clock;
            }
            else
            {
                clockwise[0] = 0;
                connections = clockwise;
                float dist = GetPassingDistance(node, pathways, clockwise, getLeftAtStart, getLeftAtEnd);

                for (int shift = 1; shift < count; shift++)
                {
                    int[] shifted = new int[count];

                    for (int i = 0; i < count; i++)
                    {
                        int s = (i + shift) % count;
                        shifted[s] = (s + clockwise[i] - i + count) % count;
                    }

                    float d = GetPassingDistance(node, pathways, shifted, getLeftAtStart, getLeftAtEnd);
                    if (d < dist)
                    {
                        connections = shifted;
                        dist = d;
                    }
                }

                opposite = connections;
                return dist;
            }
        }

        private float GetPassingDistance(EulerNode node, IList<EulerPathway> pathways, int[] connections,
            Func<EulerPath, bool?> getSideAtStart, Func<EulerPath, bool?> getSideAtEnd, bool useModifiers = true)
        {
            float distance = 0f;

            List<EulerPathway> closed = new List<EulerPathway>();

            for (int i = 0; i < pathways.Count; i++)
            {
                EulerPathway from = pathways[i].GetOppositeDirection();
                if (closed.Contains(from))
                {
                    continue;
                }

                int pairing = connections[i];
                EulerPathway to = pathways[pairing];

                EulerPathway opposite = to.GetOppositeDirection();
                if (opposite == from)
                {
                    continue;
                }

                closed.Add(opposite);

                StreetPathway fromWay = from.GetIncomingWay();
                StreetPathway toWay = to.GetOutgoingWay();

                bool? fromLeftOfWay = GetLeftOfWayAtEnd(from, getSideAtStart, getSideAtEnd);
                bool? toLeftOfWay = GetLeftOfWayAtStart(to, getSideAtStart, getSideAtEnd);

                float passing = node.Intersection.Streetnode.GetPassingDistance(fromWay, fromLeftOfWay, toWay, toLeftOfWay);
                if (useModifiers && fromWay.Path == toWay.Path)
                {
                    if (from.Eulerpath.IsRouteDeadEnd)
                    {
                        passing = passing * 100 + 100;
                    }
                    else if (from.LeftOfWay(false) == to.LeftOfWay(true))
                    {
                        passing *= 1.01f;
                    }
                }
                distance += passing;
            }

            return distance;
        }

        private static bool? GetLeftOfWayAtStart(EulerPathway way, Func<EulerPath, bool?> getSideAtStart, Func<EulerPath, bool?> getSideAtEnd) =>
            way.Eulerpath.Forward == way ? getSideAtStart(way.Eulerpath) : !getSideAtEnd(way.Eulerpath);

        private static bool? GetLeftOfWayAtEnd(EulerPathway way, Func<EulerPath, bool?> getSideAtStart, Func<EulerPath, bool?> getSideAtEnd) =>
            way.Eulerpath.Forward == way ? getSideAtEnd(way.Eulerpath) : !getSideAtStart(way.Eulerpath);

        private static float GetSortScore(EulerPathway way, Func<EulerPath, bool?> getSideAtStart, Func<EulerPath, bool?> getSideAtEnd)
        {
            float score = way.Origin.Intersection.Streetnode.Pathways.IndexOf(way.GetOutgoingWay());
            bool? left = GetLeftOfWayAtStart(way, getSideAtStart, getSideAtEnd);
            if (left == null)
            {
                score += 0.1f;
            }
            else if (left == true)
            {
                score += 0.2f;
            }
            return score;
        }
    }
}