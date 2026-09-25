//Copyright Thomas Greshake 2026

using Brieffreund.AStar;
using Spectre.Console;

namespace Brieffreund.Eulermap
{
    internal interface IPathingCountGenerator
    {
        public void Generate(int count);

        public IList<PathingCount[]> Result { get; }
    }

    internal enum PathingCount : byte
    {
        NoPath = 0, Single = 1, Double = 2, Triple = 3,
        Undetermined = 6 //Undetermined must be an even number
    }
}

namespace Brieffreund.Eulermap.Functions
{
    internal class PathingCountGenerator : IPathingCountGenerator
    {
        private readonly IStreetMap _map;

        private readonly object _lock = new object();

        private PriorityQueue<PathingCount[], float> _queue = new(1024);

        private int _searchDepth = 0;

        private readonly List<Tuple<PathingCount[], float>> _completed = new();

        private readonly List<PathingCount[]> _result = new();
        public IList<PathingCount[]> Result => _result;

        internal PathingCountGenerator(IStreetMap map)
        {
            _map = map;
        }

        public void Generate(int searchDepth)
        {
            _searchDepth = Constants.PATHINGCOUNT_SEARCHDEPTH_MULT * searchDepth;
            _completed.Clear();
            _result.Clear();

            PathingCount[] initial = CreateInitialMap();
            Enqueue(initial);

            int threadCount = Constants.THREAD_COUNT;
            SeedQueue(threadCount);

            Thread[] threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(Process);
            }

            for (int i = 0; i < threadCount; i++)
            {
                threads[i].Start();
            }

            Process();

            for (int i = 0; i < threadCount; i++)
            {
                threads[i].Join();
            }

            _queue = new();

            for (int i = 0; i < Math.Min(_completed.Count, searchDepth); i++)
            {
                _result.Add(_completed[i].Item1);
            }
        }

        private void SeedQueue(int threadCount)
        {
            while (_queue.Count > 0 && _queue.Count <= threadCount * 2 + 1 && _completed.Count < _searchDepth)
            {
                ProcessNext();
            }
        }

        private void Process()
        {
            while (_completed.Count < _searchDepth && _queue.Count > 0)
            {
                ProcessNext();
            }
        }

        private void ProcessNext()
        {
            PathingCount[] current;
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    return;
                }
                current = _queue.Dequeue();
            }

            StreetSegment? segment = null;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == PathingCount.Undetermined)
                {
                    segment = _map.Segments[i];
                    break;
                }
            }

            if (segment != null)
            {
                bool? isEven = IsEven(segment, current);
                if (isEven != false)
                {
                    PathingCount[] even = CreateBranch(current, segment.Index, GetPathingCount(segment, true), isEven == null);
                    Enqueue(even);
                }
                if (isEven != true)
                {
                    PathingCount[] uneven = CreateBranch(current, segment.Index, GetPathingCount(segment, false), false);
                    Enqueue(uneven);
                }
                return;
            }

            RemoveUnreachable(current);

            if (!IsEven(current))
            {
                throw new Exception();
            }

            AddToCompleted(Tuple.Create(current, GetLength(current)));
        }

        private void Enqueue(PathingCount[] map)
        {
            float length = DeepLengthEstimate(map);
            lock (_lock)
            {
                _queue.Enqueue(map, length);
            }
        }

        private void AddToCompleted(Tuple<PathingCount[], float> item)
        {
            lock (_lock)
            {
                for (int i = 0; i < _completed.Count; i++)
                {
                    if (item.Item2 < _completed[i].Item2)
                    {
                        _completed.Insert(i, item);
                        return;
                    }
                }

                _completed.Add(item);
            }
        }

        private bool IsEven(Intersection i, PathingCount[] map) =>
            (i.Segments.Where(s => s.IsActive).Sum(s => (int)map[s.Index]) + (i.IsStart ? 1 : 0) + (i.IsEnd ? 1 : 0)) % 2 == 0;

        private bool IsEven(PathingCount[] protoMap) => _map.Intersections.Values.All(i => IsEven(i, protoMap));

        #region InitialMap

        private PathingCount[] CreateInitialMap()
        {
            PathingCount[] initial = new PathingCount[_map.Segments.Count];
            for (int i = 0; i < initial.Length; i++)
            {
                initial[i] = PathingCount.Undetermined;
            }

            CollapseBranch(initial);
            CollapseOnlyConnections(initial);
            CollapseBranch(initial);
            return initial;
        }

        private void CollapseOnlyConnections(PathingCount[] initial)
        {
            Intersection start = _map.StartAndEndIntersections[0];
            Intersection end = _map.StartAndEndIntersections[1];

            for (int i = 0; i < initial.Length; i++)
            {
                if (initial[i] != PathingCount.Undetermined)
                {
                    continue;
                }

                StreetSegment segment = _map.Segments[i];
                if (!segment.IsOnlyConnection)
                {
                    continue;
                }

                IPathfinder<Intersection, SegmentPathway> toStart = IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, start,
                    w => w.Segment == segment || !w.Segment.IsActive ? -1 : 1);
                IPathfinder<Intersection, SegmentPathway> toEnd = IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, end,
                    w => w.Segment == segment || !w.Segment.IsActive ? -1 : 1);

                initial[segment.Index] = GetPathingCount(segment, toStart.Success == toEnd.Success);
            }
        }

        #endregion InitialMap

        #region Branches

        private bool? IsEven(StreetSegment segment, PathingCount[] map)
        {
            IPathfinder<Intersection, SegmentPathway> path =
                IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, segment.To, w => PathWeightFunc(w, segment, map));

            if (path.Success)
            {
                return null;
            }

            List<Intersection> set = new();
            CreateUndeterminedSet(segment, segment.From, set, map);

            int count = set.Sum(i => IsEven(i, map) ? 0 : 1);

            return count % 2 == 0;
        }

        private static float PathWeightFunc(SegmentPathway way, StreetSegment segment, PathingCount[] protoMap)
            => !way.Segment.IsActive || way.Segment == segment || protoMap[way.Segment.Index] != PathingCount.Undetermined ? -1 : 1;

        private void CreateUndeterminedSet(StreetSegment test, Intersection inter, List<Intersection> set, PathingCount[] map)
        {
            set.Add(inter);

            foreach (StreetSegment segment in inter.Segments.Where(s => s.IsActive))
            {
                if (map[segment.Index] != PathingCount.Undetermined || segment == test)
                {
                    continue;
                }

                Intersection other = segment.GetOther(inter);
                if (set.Contains(other))
                {
                    continue;
                }

                CreateUndeterminedSet(test, other, set, map);
            }
        }

        private PathingCount[] CreateBranch(PathingCount[] original, int index, PathingCount change, bool newBranch)
        {
            PathingCount[] branch;
            if (newBranch)
            {
                branch = new PathingCount[original.Length];
                Array.Copy(original, branch, original.Length);
            }
            else
            {
                branch = original;
            }
            branch[index] = change;
            CollapseBranch(branch);
            return branch;
        }

        private bool CollapseBranch(PathingCount[] branch)
        {
            List<Intersection> intersections = _map.Intersections.Values
                .Where(i => i.Segments.Count(s => s.IsActive && branch[s.Index] == PathingCount.Undetermined) == 1).ToList();

            if (intersections.Count == 0)
            {
                return false;
            }

            while (intersections.Count > 0)
            {
                int index = intersections.Count - 1;
                Intersection inter = intersections[index];
                intersections.RemoveAt(index);

                StreetSegment segment = inter.Segments.First(s => s.IsActive && branch[s.Index] == PathingCount.Undetermined);
                branch[segment.Index] = GetPathingCount(segment, IsEven(inter, branch));

                Intersection other = segment.GetOther(inter);
                int count = other.Segments.Count(s => s.IsActive && branch[s.Index] == PathingCount.Undetermined);
                if (count == 1)
                {
                    intersections.Add(other);
                }
                else if (count == 0)
                {
                    intersections.Remove(other);
                }
            }
            return true;
        }

        #endregion Branches

        #region Length

        internal float EstimateLength(PathingCount[] protoMap)
        {
            float length = 0;

            foreach (Intersection inter in _map.Intersections.Values)
            {
                float interLength = 0;
                int idealCount = (inter.IsStart ? 1 : 0) + (inter.IsEnd ? 1 : 0);
                float bestToNeighbour = float.MaxValue;

                foreach (StreetSegment seg in inter.Segments.Where(s => s.IsActive))
                {
                    float dist = 0;

                    switch (protoMap[seg.Index])
                    {
                        case PathingCount.NoPath:
                            break;

                        case PathingCount.Single:
                            idealCount += 1;
                            interLength += seg.SinglePathingLength;
                            break;

                        case PathingCount.Double:
                            interLength += 2 * seg.Length;
                            break;

                        case PathingCount.Triple:
                            idealCount += 1;
                            interLength += 3 * seg.Length;
                            break;

                        case PathingCount.Undetermined:
                            if (AllowSinglePath(seg) && seg.SinglePathingLength < 2 * seg.Length)
                            {
                                if (seg.IsRequiredOnRoute)
                                {
                                    idealCount += 1;
                                    interLength += seg.SinglePathingLength;
                                    dist = 2 * seg.Length - seg.SinglePathingLength;
                                }
                                else
                                {
                                    dist = seg.SinglePathingLength;
                                }
                            }
                            else
                            {
                                dist = seg.Length;
                                if (seg.IsRequiredOnRoute)
                                {
                                    interLength += 2 * seg.Length;
                                }
                            }
                            break;

                        default:
                            throw new NotImplementedException();
                    }

                    Intersection other = seg.GetOther(inter);
                    if (!IdealIsEven(other, protoMap))
                    {
                        dist /= 2f;
                    }

                    if (dist < bestToNeighbour)
                    {
                        bestToNeighbour = dist;
                    }
                }

                length += interLength / 2f;
                if (idealCount % 2 == 1)
                {
                    length += bestToNeighbour;
                }
            }

            return length;
        }

        internal float DeepLengthEstimate(PathingCount[] protoMap)
        {
            float length = GetLength(protoMap);
            List<Intersection> uneven = _map.Intersections.Values.Where(i => !IdealIsEven(i, protoMap)).ToList();
            for (int i = 0; i < uneven.Count; i++)
            {
                Intersection inter = uneven[i];
                IPathfinder<Intersection, SegmentPathway> path =
                    new PathfinderMulti<Intersection, SegmentPathway>(inter, uneven.Where(i => i != inter), w => 1f);
                length += path.GetLength() / 2f;
            }
            return length;
        }

        private bool IdealIsEven(Intersection inter, PathingCount[] protoMap)
        {
            int count = (inter.IsStart ? 1 : 0) + (inter.IsEnd ? 1 : 0);
            foreach (StreetSegment seg in inter.Segments.Where(s => s.IsActive))
            {
                switch (protoMap[seg.Index])
                {
                    case PathingCount.Single:
                        count++;
                        continue;
                    case PathingCount.Triple:
                        count++;
                        continue;
                    case PathingCount.Undetermined:
                        if (seg.IsRequiredOnRoute && AllowSinglePath(seg) && seg.SinglePathingLength < 2 * seg.Length)
                        {
                            count++;
                        }
                        continue;
                    default:
                        continue;
                }
            }
            return count % 2 == 0;
        }

        internal float GetLength(PathingCount[] protoMap) => _map.Segments.Sum(s => GetPathingLength(s, protoMap));

        private float GetPathingLength(StreetSegment segment, PathingCount[] protoMap)
        {
            PathingCount pathCount = protoMap[segment.Index];
            switch (pathCount)
            {
                case PathingCount.NoPath:
                    return 0;

                case PathingCount.Single:
                    return segment.SinglePathingLength;

                case PathingCount.Double:
                    return 2 * segment.Length;

                case PathingCount.Triple:
                    return 3 * segment.Length;

                case PathingCount.Undetermined:
                    return GetOptimalPathLength(segment);

                default:
                    throw new NotImplementedException();
            }
        }

        private float GetOptimalPathLength(StreetSegment segment)
        {
            if (!segment.IsRequiredOnRoute)
            {
                return 0;
            }

            if (AllowSinglePath(segment) && segment.SinglePathingLength < 2 * segment.Length)
            {
                return segment.SinglePathingLength;
            }
            return 2 * segment.Length;
        }

        #endregion Length

        #region ConvertEvenAndUneven

        private PathingCount GetPathingCount(StreetSegment segment, bool even)
        {
            if (even)
            {
                return segment.IsRequiredOnRoute || segment.ForceUpgrade ? PathingCount.Double : PathingCount.NoPath;
            }
            else
            {
                return AllowSinglePath(segment) ? PathingCount.Single : PathingCount.Triple;
            }
        }

        private static bool AllowSinglePath(StreetSegment segment) => !segment.ForceUpgrade && segment.SinglePathingLength > 0 && segment.SinglePathingLength < 2.9f * segment.Length;

        private void RemoveUnreachable(PathingCount[] map)
        {
            bool[] reachable = new bool[map.Length];

            foreach (StreetSegment segment in _map.Segments.Where(s => s.IsRequiredOnRoute && !reachable[s.Index]))
            {
                GetAllReachable(segment.From, reachable, map);
            }

            for (int i = 0; i < map.Length; i++)
            {
                if (!reachable[i])
                {
                    map[i] = PathingCount.NoPath;
                }
            }
        }

        private static void GetAllReachable(Intersection inter, bool[] reachable, PathingCount[] map)
        {
            foreach (StreetSegment seg in inter.Segments.Where(s => s.IsActive && !reachable[s.Index] && map[s.Index] != PathingCount.NoPath))
            {
                reachable[seg.Index] = true;
                Intersection other = seg.GetOther(inter);
                GetAllReachable(other, reachable, map);
            }
        }

        private void EnsureAllIsConnected(PathingCount[] map)
        {
            List<List<Intersection>> sets = new();

            List<Intersection> allIntersections = _map.Intersections.Values.Where(i => i.Segments.Any(s => s.IsActive && map[s.Index] != PathingCount.NoPath)).ToList();
            while (allIntersections.Count > 0)
            {
                Intersection start = allIntersections[allIntersections.Count - 1];

                List<Intersection> set = new List<Intersection>();
                sets.Add(set);

                GetConnectedIntersections(start, set, map);

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
                IPathfinder<Intersection, SegmentPathway> best = IPathfinder.FindPath<Intersection, SegmentPathway>(first[0], toConnect[0], w => PathWeightFunc(w, map));
                float bestLength = GetLength(best, map);

                for (int i = 1; i < sets.Count; i++)
                {
                    List<Intersection> other = sets[i];
                    IPathfinder<Intersection, SegmentPathway> path = IPathfinder.FindPath<Intersection, SegmentPathway>(first[0], other[0], w => PathWeightFunc(w, map));
                    float length = GetLength(best, map);

                    if (length < bestLength)
                    {
                        toConnect = other;
                        best = path;
                        bestLength = length;
                    }
                }

                sets.Remove(toConnect);
                foreach (StreetSegment seg in best.GetPaths().Select(p => p.Segment).Where(s => map[s.Index] == PathingCount.NoPath))
                {
                    map[seg.Index] = PathingCount.Double;
                }
            }
        }

        private static void GetConnectedIntersections(Intersection inter, List<Intersection> set, PathingCount[] map)
        {
            set.Add(inter);

            foreach (StreetSegment seg in inter.Segments.Where(s => s.IsActive && map[s.Index] != PathingCount.NoPath))
            {
                Intersection other = seg.GetOther(inter);
                if (set.Contains(other))
                {
                    continue;
                }

                GetConnectedIntersections(other, set, map);
            }
        }

        private static float PathWeightFunc(SegmentPathway way, PathingCount[] map)
        {
            if (!way.Segment.IsActive)
            {
                return -1f;
            }

            return map[way.Segment.Index] == PathingCount.NoPath ? 1048576f : 1f;
        }

        private static float GetLength(IPathfinder<Intersection, SegmentPathway> path, PathingCount[] map) =>
            path.GetPaths().Where(p => map[p.Segment.Index] == PathingCount.NoPath).Sum(p => p.Segment.Length);

        #endregion ConvertEvenAndUneven
    }
}