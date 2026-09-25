//Copyright Thomas Greshake 2026

namespace Brieffreund.Eulermap
{
    internal interface IEulerDeadendFinder : IEvent<IEulerDeadendFinder>
    {
        public IList<EulerPath> DeadEnds { get; }
        public IEulerMap Map { get; }

        public void FindDeadEnds();
    }
}

namespace Brieffreund.Eulermap.Functions
{
    internal class EulerDeadendFinder : IEulerDeadendFinder
    {
        private readonly IEulerMap _map;
        public IEulerMap Map => _map;

        private readonly List<EulerPath> _deadEnds = new List<EulerPath>();
        public IList<EulerPath> DeadEnds => _deadEnds;

        internal EulerDeadendFinder(IEulerMap map)
        {
            _map = map;
        }

        public void FindDeadEnds()
        {
            _deadEnds.Clear();

            HashSet<StreetSegment> deadEnds = _map.Streetmap.Segments.Where(s => s.IsRouteDeadEnd).ToHashSet();
            List<EulerNode> front = _map.Nodes.Values.Where(n => n.Intersection.Segments.Count(s => s.IsActive && !s.IsRouteDeadEnd && _map.GetPaths(s).Count > 0) == 1).ToList();

            while (front.Count > 0)
            {
                int index = front.Count - 1;
                EulerNode node = front[index];
                front.RemoveAt(index);

                StreetSegment segment = node.Intersection.Segments.Where(s => s.IsActive).First(s => !deadEnds.Contains(s) && _map.GetPaths(s).Count > 0);
                deadEnds.Add(segment);

                EulerNode other = _map.Nodes[segment.GetOther(node.Intersection)];
                int count = other.Intersection.Segments.Count(s => s.IsActive && !deadEnds.Contains(s) && _map.GetPaths(s).Count > 0);

                if (count == 0)
                {
                    front.Remove(other);
                }
                else if (count == 1)
                {
                    front.Add(other);
                }
            }

            _deadEnds.AddRange(_map.Paths.Where(p => deadEnds.Contains(p.Segment)));

            IEulerDeadendFinder.Call(this);
        }
    }
}