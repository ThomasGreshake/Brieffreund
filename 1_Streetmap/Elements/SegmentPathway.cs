//Copyright Thomas Greshake 2026

namespace Brieffreund
{
    internal class SegmentPathway : IPathway<Intersection, SegmentPathway>
    {
        internal readonly StreetSegment Segment;
        public float Length => Segment.Length;

        private readonly Intersection _towards;
        public Intersection Towards => _towards;
        public Intersection Origin => Segment.GetOther(_towards);

        internal SegmentPathway(StreetSegment segment, Intersection towards)
        {
            Segment = segment;
            _towards = towards;
        }

        internal StreetPathway GetOutgoingway() => Segment.Forward == this ? Segment.Paths[0].Forward : Segment.Paths[Segment.Paths.Count - 1].Backward;

        internal StreetPathway GetIncomingway() => Segment.Forward == this ? Segment.Paths[Segment.Paths.Count - 1].Forward : Segment.Paths[0].Backward;

        internal SegmentPathway GetOpposite() => Segment.GetOpposite(this);
    }
}