//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStreetSegmentDisabler
    {
        public void DisableSegments();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StreetSegmentDisabler : IStreetSegmentDisabler
    {
        private readonly IStreetMap _map;

        internal StreetSegmentDisabler(IStreetMap map)
        {
            _map = map;
        }

        public void DisableSegments()
        {
            HashSet<StreetSegment> enabled = GetUsedSegments(s => s.ReceivesMail);
            HashSet<StreetSegment> useful = GetUsedSegments(s => s.HasStorages || enabled.Contains(s));

            List<StreetSegment> toDelete = _map.Segments.Where(s => CanBeDeleted(s, enabled, useful)).ToList();
            List<StreetSegment> toDeactivate = _map.Segments.Where(s => !enabled.Contains(s) && !toDelete.Contains(s)).ToList();

            int activeCount = _map.Segments.Count - toDeactivate.Count - toDelete.Count;

            StreetSegment.Delete(toDelete);
            StreetSegment.SetIsActive(toDeactivate, false);
            DisableConnections(true);
            DisableConnections(false);
            DisableMoreSegments();
            DeleteInactiveSegments();
        }

        private static bool CanBeDeleted(StreetSegment segment, HashSet<StreetSegment> enabled, HashSet<StreetSegment> useful)
        {
            if (useful.Contains(segment))
            {
                return false;
            }

            if ((!segment.From.Segments.Any(enabled.Contains) || segment.Paths[0].Type == StreetType.Tiny)
                && (!segment.To.Segments.Any(enabled.Contains) || segment.Paths[segment.Paths.Count - 1].Type == StreetType.Tiny))
            {
                return true;
            }

            return false;
        }

        private HashSet<StreetSegment> GetUsedSegments(Func<StreetSegment, bool> condition)
        {
            HashSet<StreetSegment> used = new HashSet<StreetSegment>(_map.Segments.Count);
            foreach (StreetSegment seg in _map.Segments.Where(condition))
            {
                used.Add(seg);
            }

            List<Intersection> allEdgeNodes = _map.Intersections.Values.Where(i => IsEdgeNode(i, condition)).ToList();

            while (allEdgeNodes.Count > 0)
            {
                Intersection current = allEdgeNodes[allEdgeNodes.Count - 1];

                List<Intersection> edgeNodes = new() { current };

                GetOtherEdgeNodes(current, edgeNodes, new(), condition);

                foreach (Intersection node in edgeNodes)
                {
                    allEdgeNodes.Remove(node);
                }

                for (int i = 0; i < edgeNodes.Count; i++)
                {
                    for (int j = i + 1; j < edgeNodes.Count; j++)
                    {
                        IPathfinder<Intersection, SegmentPathway> path =
                            IPathfinder.FindPath<Intersection, SegmentPathway>(edgeNodes[i], edgeNodes[j]);

                        if (!path.Success)
                        {
                            throw new Exception();
                        }

                        float dist = path.GetLength();
                        float mult = 1f + 0.001f * dist;

                        path = IPathfinder.FindPath<Intersection, SegmentPathway>(edgeNodes[i], edgeNodes[j], w => PathWeightFuncUsedSegments(w, condition, mult));

                        foreach (StreetSegment s in path.GetPaths().Select(p => p.Segment))
                        {
                            if (used.Contains(s))
                            {
                                continue;
                            }

                            used.Add(s);
                        }
                    }
                }
            }

            return used;
        }

        private static bool IsEdgeNode(Intersection i, Func<StreetSegment, bool> condition) =>
            i.Segments.Any(s => condition(s)) && i.Segments.Any(s => !condition(s));

        private static void GetOtherEdgeNodes(Intersection inter, List<Intersection> edgeNodes, List<Intersection> emptyNodes, Func<StreetSegment, bool> condition)
        {
            foreach (StreetSegment seg in inter.Segments.Where(s => !condition(s)))
            {
                Intersection other = seg.GetOther(inter);
                if (IsEdgeNode(other, condition))
                {
                    if (edgeNodes.Contains(other))
                    {
                        continue;
                    }
                    edgeNodes.Add(other);
                }
                else
                {
                    if (emptyNodes.Contains(other))
                    {
                        continue;
                    }
                    emptyNodes.Add(other);
                }

                GetOtherEdgeNodes(other, edgeNodes, emptyNodes, condition);
            }
        }

        private static float PathWeightFuncUsedSegments(SegmentPathway way, Func<StreetSegment, bool> condition, float mult)
        {
            float accessMult = way.Segment.RestrictedAccess ? 50f : 1f;

            if (condition(way.Segment))
            {
                return accessMult;
            }
            return (way.Segment.Type == StreetType.Tiny ? Constants.USELESS_TINY_SEGMENT_FACTOR : Constants.USELESS_SEGMENT_FACTOR) * mult * accessMult;
        }

        private void DisableMoreSegments()
        {
            HashSet<StreetSegment> ok = new HashSet<StreetSegment>();
            HashSet<StreetSegment> disable = new HashSet<StreetSegment>();

            while (true)
            {
                StreetSegment? segment = _map.Segments.Where(s => !s.ReceivesMail && !ok.Contains(s) && !disable.Contains(s)).MaxBy(s => s.Length);
                if (segment == null)
                {
                    break;
                }

                List<StreetSegment> group = new List<StreetSegment>() { segment };

                Intersection from = ExpandGroup(segment.From, group, disable);
                Intersection to = ExpandGroup(segment.To, group, disable);
                int type = group.Max(g => (int)g.Type);

                IPathfinder<Intersection, SegmentPathway> path =
                            IPathfinder.FindPath<Intersection, SegmentPathway>(from, to, w => PathWeightFuncDisableMore(w, disable, group, type));

                if (!path.Success)
                {
                    AddRange(ok, group);
                    continue;
                }

                float pathLength = path.GetLength();
                float groupLength = group.Sum(s => s.Length);

                if (pathLength < groupLength + 50 && pathLength < 2 * groupLength)
                {
                    AddRange(disable, group);
                }
                else
                {
                    AddRange(ok, group);
                }
            }

            StreetSegment.SetIsActive(disable, false);
        }

        private void DisableConnections(bool tiny)
        {
            List<StreetSegment> toDisable = new();

            foreach (StreetSegment segment in _map.Segments
                .Where(s => (s.Type == StreetType.Tiny) == tiny && !s.HasStorages && s.From.Count == 3 && s.To.Count == 3 && s.Length < 40 && !s.ReceivesMail
                && !s.From.Segments.Any(e => e.HasStorages) && !s.To.Segments.Any(e => e.HasStorages))
                .OrderByDescending(s => s.Length / ((int)s.Type + 3)))
            {
                List<Intersection> group = new(4);
                group.AddRange(segment.From.Pathways.Where(w => w.Segment != segment).Select(w => w.Towards));
                group.AddRange(segment.To.Pathways.Where(w => w.Segment != segment).Select(w => w.Towards));

                bool ok = true;
                int type = (int)segment.Type;
                for (int i = 0; i < group.Count; i++)
                {
                    for (int j = i + 1; j < group.Count; j++)
                    {
                        IPathfinder<Intersection, SegmentPathway> path = IPathfinder.FindPath<Intersection, SegmentPathway>(group[i], group[j],
                             w => w.Segment.IsActive && !w.Segment.RestrictedAccess && (int)w.Segment.Type >= type && w.Segment != segment && !toDisable.Contains(w.Segment) ? 1 : -1);
                        float length = path.GetLength();

                        if (!path.Success)
                        {
                            ok = false;
                            break;
                        }

                        path = IPathfinder.FindPath<Intersection, SegmentPathway>(group[i], group[j],
                             w => w.Segment.IsActive && !w.Segment.RestrictedAccess && (int)w.Segment.Type >= type && !toDisable.Contains(w.Segment) ? 1 : -1);

                        float directLength = path.GetLength();

                        if (length < directLength + 50 && length < 2 * directLength)
                        {
                            continue;
                        }

                        ok = false;
                        break;
                    }

                    if (!ok)
                    {
                        break;
                    }
                }

                if (ok)
                {
                    toDisable.Add(segment);
                }
            }

            StreetSegment.SetIsActive(toDisable, false);
        }

        private static float PathWeightFuncDisableMore(SegmentPathway w, HashSet<StreetSegment> disable, List<StreetSegment> group, int type)
        {
            if (w.Segment.IsActive && (int)w.Segment.Type >= type && !disable.Contains(w.Segment) && !group.Contains(w.Segment))
            {
                return w.Segment.RestrictedAccess ? 50f : 1f;
            }

            return -1f;
        }

        private static Intersection ExpandGroup(Intersection start, List<StreetSegment> group, HashSet<StreetSegment> disable)
        {
            while (true)
            {
                List<StreetSegment> segments = start.Segments.Where(s => s.IsActive && !disable.Contains(s) && !group.Contains(s)).ToList();
                if (segments.Count == 1 && !segments[0].ReceivesMail)
                {
                    start = segments[0].GetOther(start);
                    group.Add(segments[0]);
                }
                else
                {
                    return start;
                }
            }
        }

        private static void AddRange(HashSet<StreetSegment> set, List<StreetSegment> group)
        {
            foreach (var s in group)
            {
                set.Add(s);
            }
        }

        private void DeleteInactiveSegments()
        {
            StreetSegment.Delete(_map.InactiveSegments.Where(s => !s.HasStorages && (s.Type == StreetType.Tiny ||
            ((!s.From.IsActive || (!s.To.IsActive && s.Paths[0].Type == StreetType.Tiny))
            && (!s.To.IsActive || (!s.From.IsActive && s.Paths[s.Paths.Count - 1].Type == StreetType.Tiny))
            ))).ToList());
        }
    }
}