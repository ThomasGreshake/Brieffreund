//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStreetSegmentTrimmer
    {
        public void Trim();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StreetSegmentTrimmer : IStreetSegmentTrimmer
    {
        private readonly IStreetMap _map;
        private readonly IStreetSegmentMerger _merger;

        internal StreetSegmentTrimmer(IStreetMap map, IStreetSegmentMerger merger)
        { _map = map; _merger = merger; }

        public void Trim()
        {
            DeleteRestrictedAccess();
            DeleteShortDeadEnds();
            DeleteShortSegments(true);
            DeleteShortSegments(false);
            DeleteShortDeadEnds();
        }

        private void DeleteRestrictedAccess()
        {
            List<StreetSegment> toDelete = new();

            foreach (StreetSegment segment in _map.GetAllSegments().Where(s => s.RestrictedAccess))
            {
                if (segment.From.Segments.All(s => s.RestrictedAccess) && segment.To.Segments.All(s => s.RestrictedAccess))
                {
                    toDelete.Add(segment);
                }
            }

            Delete(toDelete);
        }

        private void DeleteShortSegments(bool tiny)
        {
            List<StreetSegment> toDelete = new();

            foreach (StreetSegment segment in _map.GetAllSegments()
                .Where(s => s.Length < 70 && (s.Type == StreetType.Tiny) == tiny)
                .OrderByDescending(s => s.Paths.Sum(p => (p.IsTurnLane ? 4f : 1f) * p.Length) / (1 + (int)s.Type * (int)s.Type)))
            {
                if (segment.From.Count == 1 || segment.To.Count == 1)
                {
                    continue;
                }

                int type = (int)segment.Type;
                IPathfinder<Intersection, SegmentPathway> path = IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, segment.To,
                    w => w.Segment == segment || toDelete.Contains(w.Segment) || w.Segment.RestrictedAccess || (int)w.Segment.Type < type ? -1 : 1);

                if (!path.Success)
                {
                    continue;
                }

                float length = path.GetLength();

                if (length > 1.2f * segment.Length + 30)
                {
                    continue;
                }

                toDelete.Add(segment);
            }

            Delete(toDelete);
        }

        private void DeleteShortDeadEnds()
        {
            List<StreetSegment> toDelete = new();

            foreach (Intersection inter in _map.GetAllIntersections().Where(i => i.Count == 1))
            {
                StreetSegment segment = inter.Segments.First();
                float length = segment.Paths.Sum(p => (p.RestrictedAccess ? 0.65f : 1f) * (p.Type == StreetType.Tiny ? 0.65f : 1f) * p.Length);

                if (length < Constants.TRIM_LENGTH_METER && !segment.ReceivesMail && !segment.HasStorages && !toDelete.Contains(segment))
                {
                    toDelete.Add(segment);
                }
            }

            Delete(toDelete);
        }

        private void Delete(List<StreetSegment> toDelete)
        {
            if (toDelete.Count > 0)
            {
                StreetSegment.Delete(toDelete);
                _merger.Merge();
            }
        }
    }
}