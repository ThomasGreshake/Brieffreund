//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap.Functions
{
    internal class OnlyConnectionSetter : InternalFlagSetter
    {
        internal OnlyConnectionSetter(IStreetMap map) : base(map)
        {
        }

        public override void SetFlags()
        {
            SetCurrentFlags();

            FindDeadEnds();
            FindOnlyPathConnections();

            IInternalFlagSetter.Call(this);
        }

        private void FindDeadEnds()
        {
            List<Intersection> onDeadEnd = Map.Intersections.Values.Where(i =>
            i.Segments.Count(s => s.IsActive && !HasFlag(s, InternalPathFlags.RouteDeadEnd)) == 1).ToList();

            while (onDeadEnd.Count > 0)
            {
                int index = onDeadEnd.Count - 1;
                Intersection inter = onDeadEnd[index];
                onDeadEnd.RemoveAt(index);

                StreetSegment segment = inter.Segments.First(s => s.IsActive && !HasFlag(s, InternalPathFlags.RouteDeadEnd));
                AddFlag(segment, InternalPathFlags.RouteDeadEnd);

                Intersection other = segment.GetOther(inter);
                int count = other.Segments.Count(s => s.IsActive && !HasFlag(s, InternalPathFlags.RouteDeadEnd));
                if (count == 0)
                {
                    onDeadEnd.Remove(other);
                }
                else if (count == 1)
                {
                    onDeadEnd.Add(other);
                }
            }
        }

        private void FindOnlyPathConnections()
        {
            bool[] onlyConnections = new bool[Map.Segments.Count];
            for (int i = 0; i < onlyConnections.Length; i++)
            {
                onlyConnections[i] = true;
            }

            foreach (StreetSegment segment in Map.Segments)
            {
                if (HasFlag(segment, InternalPathFlags.RouteDeadEnd)
                    || !onlyConnections[segment.Index]) //All dead ends are onlyConnections and all false are already set
                {
                    continue;
                }

                IPathfinder<Intersection, SegmentPathway> path =
                    IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, segment.To,
                    w => (w.Segment == segment || !w.Segment.IsActive
                    || HasFlag(w.Segment, InternalPathFlags.RouteDeadEnd)) ? -1f : 1f); //Avoiding dead ends makes pathfinding more efficient

                if (path.Success)
                {
                    onlyConnections[segment.Index] = false;
                    foreach (StreetSegment seg in path.GetPaths().Select(p => p.Segment))
                    {
                        onlyConnections[seg.Index] = false;
                    }
                }
            }

            foreach (StreetSegment seg in Map.Segments)
            {
                if (onlyConnections[seg.Index])
                {
                    AddFlag(seg, InternalPathFlags.OnlyConnection);
                }
            }
        }
    }
}