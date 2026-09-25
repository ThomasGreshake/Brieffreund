//Copyright Thomas Greshake 2026

using Priority_Queue;
using System.Diagnostics;
using System.Numerics;

namespace Brieffreund.AStar
{
    public class PathfinderMulti<N, P> : IPathfinder<N, P> where N : class, IPathnode<N, P> where P : class, IPathway<N, P>
    {
        //Data -------------------------------------------------------------

        private readonly List<P> _path = new();
        public P this[int index] => _path[_path.Count - index - 1];

        public int Count => _path.Count;

        private readonly bool _success = true;
        public bool Success => _success;

        //Setup -------------------------------------------------------------

        public PathfinderMulti(N start, IEnumerable<N> ends, Func<P, float> pathMult) //Negative mult => exclude path
        {
            List<N> closedSet = new();
            Dictionary<N, P> cameFrom = new();
            Dictionary<N, float> gScore = new() { { start, 0 } };
            IPriorityQueue<N> openSet = new SimplePriorityQueue<N>();
            float dist = GetDistance(start.Position, ends);
            openSet.Enqueue(start, dist);

            while (openSet.Count > 0)
            {
                N current = openSet.Dequeue();
                if (ends.Contains(current))
                {
                    N last = ConstructPath(cameFrom, current);
                    Debug.Assert(last == start);
                    return;
                }

                closedSet.Add(current);

                IList<P> neighbours = current.GetPaths();
                for (int i = 0; i < neighbours.Count; i++)
                {
                    P p = neighbours[i];
                    float mult = pathMult(p);
                    if (mult < 0)
                    {
                        continue;
                    }

                    N n = p.Towards;
                    if (closedSet.Contains(n))
                    {
                        continue;
                    }

                    float g = gScore[current] + p.Length * mult;

                    if (openSet.Contains(n) && gScore[n] <= g)
                    {
                        continue;
                    }

                    cameFrom[n] = p;
                    gScore[n] = g;
                    g += GetDistance(n.Position, ends);

                    if (openSet.Contains(n))
                    {
                        openSet.UpdatePriority(n, g);
                    }
                    else
                    {
                        openSet.Enqueue(n, g);
                    }
                }
            }

            _success = false;
        }

        private N ConstructPath(Dictionary<N, P> cameFrom, N end)
        {
            N current = end;

            while (cameFrom.TryGetValue(current, out var path))
            {
                current = path.Origin;
                _path.Add(path);
            }

            return current;
        }

        private float GetDistance(Vector2 position, IEnumerable<N> endPoints)
        {
            float best = float.MaxValue;
            foreach (N n in endPoints)
            {
                float dist = (n.Position - position).LengthSquared();
                if (dist < best)
                {
                    best = dist;
                }
            }
            return (float)Math.Sqrt(best);
        }
    }
}