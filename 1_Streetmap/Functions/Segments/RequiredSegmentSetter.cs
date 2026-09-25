//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap.Functions
{
    internal class RequiredSegmentSetter : InternalFlagSetter
    {
        internal RequiredSegmentSetter(IStreetMap map) : base(map)
        {
        }

        public override void SetFlags()
        {
            SetCurrentFlags();

            SetRequiredDueToMail();
            RequireShortConnections();
            SetRequiredDueToConnections();
            UpgradePaths();

            IInternalFlagSetter.Call(this);
        }

        private void SetRequiredDueToMail()
        {
            foreach (StreetSegment segment in Map.Segments)
            {
                if (segment.ReceivesMail)
                {
                    AddFlag(segment, InternalPathFlags.RequiredOnRoute);
                    continue;
                }

                if (!HasFlag(segment, InternalPathFlags.OnlyConnection))
                {
                    continue;
                }

                if (ConnectsToMail(segment.From, segment) && ConnectsToMail(segment.To, segment))
                {
                    AddFlag(segment, InternalPathFlags.RequiredOnRoute);
                }
            }
        }

        private bool ConnectsToMail(Intersection inter, StreetSegment segment)
        {
            List<Intersection> toCheck = new List<Intersection>() { inter };
            List<StreetSegment> done = new List<StreetSegment>() { segment };

            while (toCheck.Count > 0)
            {
                int index = toCheck.Count - 1;
                Intersection current = toCheck[index];
                toCheck.RemoveAt(index);

                foreach (StreetSegment seg in current.Segments.Where(s => s.IsActive))
                {
                    if (done.Contains(seg))
                    {
                        continue;
                    }
                    if (seg.ReceivesMail)
                    {
                        return true;
                    }
                    done.Add(seg);

                    Intersection other = seg.GetOther(current);
                    if (toCheck.Contains(other))
                    {
                        continue;
                    }
                    toCheck.Add(other);
                }
            }

            return false;
        }

        private void RequireShortConnections()
        {
            foreach (StreetSegment segment in Map.Segments)
            {
                if (HasFlag(segment, InternalPathFlags.RequiredOnRoute) ||
                    !segment.From.Segments.Any(s => s.IsActive && HasFlag(s, InternalPathFlags.RequiredOnRoute))
                    || !segment.To.Segments.Any(s => s.IsActive && HasFlag(s, InternalPathFlags.RequiredOnRoute)))
                {
                    continue;
                }

                if (segment.Length < 10 ||
                    (segment.Type != StreetType.Tiny && 2 * segment.Length < (segment.Paths[0].GetPassingWidth() + segment.Paths[segment.Paths.Count - 1].GetPassingWidth())))
                {
                    AddFlag(segment, InternalPathFlags.RequiredOnRoute);
                }
            }
        }

        private void SetRequiredDueToConnections()
        {
            List<List<Intersection>> sets = new();

            List<Intersection> allIntersections = Map.Intersections.Values
                .Where(i => i.Segments.Any(s => s.IsActive && HasFlag(s, InternalPathFlags.RequiredOnRoute))).ToList();
            while (allIntersections.Count > 0)
            {
                Intersection start = allIntersections[allIntersections.Count - 1];

                List<Intersection> set = new List<Intersection>();
                sets.Add(set);

                GetConnectedIntersections(start, set);

                foreach (Intersection inter in set)
                {
                    allIntersections.Remove(inter);
                }
            }

            if (sets.Count == 1)
            {
                return;
            }

            int index = sets.Count - 1;
            List<Intersection> first = sets[index];
            sets.RemoveAt(index);

            while (sets.Count > 0)
            {
                List<Intersection> toConnect = sets[0];
                IPathfinder<Intersection, SegmentPathway> best = IPathfinder.FindPath<Intersection, SegmentPathway>(first[0], toConnect[0], PathWeightFunc);
                float bestLength = GetLength(best);

                for (int i = 1; i < sets.Count; i++)
                {
                    List<Intersection> other = sets[i];
                    IPathfinder<Intersection, SegmentPathway> path = IPathfinder.FindPath<Intersection, SegmentPathway>(first[0], other[0], PathWeightFunc);
                    float length = GetLength(best);

                    if (length < bestLength)
                    {
                        toConnect = other;
                        best = path;
                        bestLength = length;
                    }
                }

                sets.Remove(toConnect);
                foreach (StreetSegment seg in best.GetPaths().Select(p => p.Segment))
                {
                    AddFlag(seg, InternalPathFlags.RequiredOnRoute);
                }
            }
        }

        private float PathWeightFunc(SegmentPathway way)
        {
            if (!way.Segment.IsActive)
            {
                return -1f;
            }

            if (HasFlag(way.Segment, InternalPathFlags.RequiredOnRoute))
            {
                return 1f;
            }

            return 1048576f / ((int)way.Segment.Type + 3f);
        }

        private void GetConnectedIntersections(Intersection inter, List<Intersection> set)
        {
            set.Add(inter);

            foreach (StreetSegment seg in inter.Segments.Where(s => s.IsActive && HasFlag(s, InternalPathFlags.RequiredOnRoute)))
            {
                Intersection other = seg.GetOther(inter);
                if (set.Contains(other))
                {
                    continue;
                }

                GetConnectedIntersections(other, set);
            }
        }

        private float GetLength(IPathfinder<Intersection, SegmentPathway> path) =>
            path.GetPaths().Where(p => !HasFlag(p.Segment, InternalPathFlags.RequiredOnRoute)).Sum(p => p.Segment.Length);

        private void UpgradePaths()
        {
            foreach (StreetSegment segment in Map.Segments)
            {
                if (segment.Type == StreetType.Tiny || !HasFlag(segment, InternalPathFlags.RequiredOnRoute))
                {
                    continue;
                }

                if (segment.LeftMailAmount > 0 && segment.RightMailAmount > 0 && segment.SinglePathingLength < 0)
                {
                    AddFlag(segment, InternalPathFlags.ForceUpgrade);
                }
            }

            while (true)
            {
                bool upgradedOne = false;

                foreach (StreetSegment segment in Map.Segments)
                {
                    if (segment.Type == StreetType.Tiny || segment.Type == StreetType.Minor
                        || HasFlag(segment, InternalPathFlags.ForceUpgrade)
                        || (!segment.From.Segments.Any(s => s.IsActive && HasFlag(s, InternalPathFlags.ForceUpgrade))
                        && !segment.To.Segments.Any(s => s.IsActive && HasFlag(s, InternalPathFlags.ForceUpgrade))))
                    {
                        continue;
                    }

                    if (2 * segment.Length < segment.Paths[0].GetPassingWidth() + segment.Paths[segment.Paths.Count - 1].GetPassingWidth())
                    {
                        AddFlag(segment, InternalPathFlags.ForceUpgrade);
                        upgradedOne = true;
                    }
                }

                if (!upgradedOne)
                {
                    break;
                }
            }
        }
    }
}