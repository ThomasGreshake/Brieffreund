//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStreetSegmentCreator
    {
        public void CreateSegments();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StreetSegmentCreator : IStreetSegmentCreator
    {
        private readonly IStreetMap _map;

        internal StreetSegmentCreator(IStreetMap map)
        {
            _map = map;
        }

        public void CreateSegments()
        {
            foreach (StreetPath path in _map.GetAllPaths())
            {
                if (path.Segment == null)
                {
                    CreateSegment(path);
                }
            }
        }

        private void CreateSegment(StreetPath path)
        {
            List<StreetPath> paths = new List<StreetPath>() { path };
            FollowPath(path, paths, true);
            FollowPath(path, paths, false);

            Intersection from = Intersection.GetOrCreate(_map, paths[0].From);
            Intersection to = Intersection.GetOrCreate(_map, paths[paths.Count - 1].To);
            StreetSegment.Create(_map, from, to, paths);
        }

        private void FollowPath(StreetPath path, List<StreetPath> paths, bool reverse)
        {
            if (reverse ? _map.IsIntersection(path.From) : _map.IsIntersection(path.To))
            {
                return;
            }
            if (reverse ? path.From.Count != 2 : path.To.Count != 2)
            {
                return;
            }

            StreetPath next = GetOther(reverse ? path.From : path.To, path);
            if (path.IsActive != next.IsActive || (path.IsActive && path.RestrictedAccess != next.IsActive && next.RestrictedAccess))
            {
                return;
            }

            if (reverse ? path.From != next.To : path.To != next.From)
            {
                next.Reverse();
            }

            if (!paths.Contains(next))
            {
                if (reverse)
                {
                    paths.Insert(0, next);
                }
                else
                {
                    paths.Add(next);
                }
                FollowPath(next, paths, reverse);
            }
        }

        private static StreetPath GetOther(StreetNode node, StreetPath path) => node.Pathways[0].Path == path ? node.Pathways[1].Path : node.Pathways[0].Path;
    }
}