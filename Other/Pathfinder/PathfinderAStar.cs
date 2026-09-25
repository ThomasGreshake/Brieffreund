//Copyright Thomas Greshake 2026

using Priority_Queue;
using System.Diagnostics;
using System.Numerics;

namespace Brieffreund.AStar
{
    public class PathfinderAStar<N, P> : IPathfinder<N, P> where N : class, IPathnode<N, P> where P : class, IPathway<N, P>
    {
        //Data -------------------------------------------------------------

        private readonly List<P> _path = new();
        public P this[int index] => _path[_path.Count - index - 1];

        public int Count => _path.Count;

        private readonly bool _success = true;
        public bool Success => _success;

        //Setup -------------------------------------------------------------

        public PathfinderAStar(N start, N end, Func<P, float> pathMult) //Negative mult => exclude path
        {
            List<N> closedSet = new();
            Dictionary<N, P> cameFrom = new();
            Dictionary<N, float> gScore = new() { { start, 0 } };
            IPriorityQueue<N> openSet = new SimplePriorityQueue<N>();
            float dist = Vector2.Distance(start.Position, end.Position);
            openSet.Enqueue(start, dist);

            while (openSet.Count > 0)
            {
                N current = openSet.Dequeue();
                if (current == end)
                {
                    N last = ConstructPath(cameFrom, end);
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
                    g += Vector2.Distance(n.Position, end.Position);

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
    }
}