//Copyright Thomas Greshake 2026

using Brieffreund.Streetmap.Functions;
using System.Numerics;

namespace Brieffreund.Streetmap
{
    internal interface IStreetPathCutter
    {
        public void CutRestricted();

        public void CutPaths();

        public IList<Tuple<string, string, CutterFailureReason>> FailedCuts { get; }
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal enum CutterFailureReason
    { FirstAddressNumber, FirstAddressStreet, SecondAddressNumber, SecondAddressStreet, Distance, Divide }

    internal class StreetPathCutter : IStreetPathCutter
    {
        private readonly IUserInput _input;
        private readonly IStreetMap _map;
        private readonly IStreetSegmentCreator _creator;
        private readonly IBuildingToPathCaster _caster;
        private readonly IStreetSegmentSplitter _splitter;
        private readonly IStreetSegmentMerger _merger;

        private readonly IList<Building> _buildings;
        private readonly IList<PolygonCollider> _colliders;

        private readonly List<Tuple<string, string, CutterFailureReason>> _failedCuts = new();
        public IList<Tuple<string, string, CutterFailureReason>> FailedCuts => _failedCuts;

        internal StreetPathCutter(IUserInput input, IStreetMap map,
            IStreetSegmentCreator creator, IBuildingToPathCaster caster, IStreetSegmentSplitter splitter, IStreetSegmentMerger merger,
            IList<Building> buildings, IList<PolygonCollider> colliders)
        {
            _input = input;
            _map = map;
            _creator = creator;
            _caster = caster;
            _splitter = splitter;
            _merger = merger;
            _buildings = buildings;
            _colliders = colliders;
        }

        public void CutRestricted()
        {
            if (CutRestrictedAccess())
            {
                _merger.Merge();
            }
        }

        public void CutPaths()
        {
            if (MakeInputCuts() | CutTinyPaths())
            {
                _merger.Merge();
            }
        }

        private bool MakeInputCuts()
        {
            if (_input.Cuts.Count == 0)
            {
                return false;
            }

            _caster.CastBuildings();

            bool recreateSegments = false;

            foreach (var cut in _input.Cuts)
            {
                Tuple<StreetPath, StreetNode>? first = Interpret(cut.Item1, cut.Item2, out AddressFailureReason reason);
                if (first == null)
                {
                    _failedCuts.Add(Tuple.Create(cut.Item1.Name + " " + cut.Item2,
                        cut.Item3.Name + " " + cut.Item4,
                        reason == AddressFailureReason.Number ? CutterFailureReason.FirstAddressNumber : CutterFailureReason.FirstAddressStreet));
                    continue;
                }

                Tuple<StreetPath, StreetNode>? second = Interpret(cut.Item3, cut.Item4, out reason);
                if (second == null)
                {
                    _failedCuts.Add(Tuple.Create(cut.Item1.Name + " " + cut.Item2,
                        cut.Item3.Name + " " + cut.Item4,
                        reason == AddressFailureReason.Number ? CutterFailureReason.SecondAddressNumber : CutterFailureReason.SecondAddressStreet));
                    continue;
                }

                IPathfinder<StreetNode, StreetPathway> path = IPathfinder.FindPath<StreetNode, StreetPathway>(first.Item2, second.Item2, w => w.Path.IsActive ? 1f : -1f);
                if (!path.Success)
                {
                    throw new Exception();
                }

                float length = path.GetLength();
                int interCount = path.GetPaths().Where(w => path.Last() != w).Count(w => _map.Intersections.ContainsKey(w.Towards));
                if (interCount > 2 || length > 200)
                {
                    _failedCuts.Add(Tuple.Create(cut.Item1.Name + " " + cut.Item2,
                        cut.Item3.Name + " " + cut.Item4, CutterFailureReason.Distance));
                    continue;
                }

                StreetPath cutPath;
                bool atStart;
                if (path.Count == 0)
                {
                    Tuple<StreetPath, StreetNode> cutTuple = first.Item1.Length > second.Item1.Length ? first : second;
                    cutPath = cutTuple.Item1;
                    atStart = cutPath.From == cutTuple.Item2;
                }
                else
                {
                    float halfLength = length * 0.5f;
                    float walkedLength = 0;

                    cutPath = path.First().Path;
                    atStart = path.First().Towards == cutPath.From;

                    foreach (StreetPathway way in path.GetPaths())
                    {
                        walkedLength += way.Length;
                        if (walkedLength > halfLength)
                        {
                            cutPath = way.Path;
                            atStart = way.Towards == cutPath.To;
                            if ((walkedLength - halfLength) < 0.5f * cutPath.Length)
                            {
                                atStart = !atStart;
                            }

                            break;
                        }
                    }
                }

                path = IPathfinder.FindPath<StreetNode, StreetPathway>(cutPath.From, cutPath.To, w => w.Path == cutPath || !w.Path.IsActive ? -1f : 1f);
                if (!path.Success)
                {
                    _failedCuts.Add(Tuple.Create(cut.Item1.Name + " " + cut.Item2,
                        cut.Item3.Name + " " + cut.Item4, CutterFailureReason.Divide));
                    continue;
                }

                if (cutPath.Segment != null)
                {
                    List<StreetSegment> toRemove = new List<StreetSegment>() { cutPath.Segment };
                    StreetSegment.Remove(toRemove);
                    recreateSegments = true;
                }

                cutPath.Cut(atStart);
            }

            if (recreateSegments)
            {
                _creator.CreateSegments();
            }

            return true;
        }

        private Tuple<StreetPath, StreetNode>? Interpret(Street street, string fullNumber, out AddressFailureReason reason)
        {
            reason = AddressFailureReason.None;

            if (!Street.FullnumberToNumberId(fullNumber, out int numberId))
            {
                reason = AddressFailureReason.Number;
                return null;
            }

            bool isEven = Street.NumberIdToNumber(numberId) % 2 == 0;

            Building? building = _caster.Casts.Keys.Where(b => b.Street == street && b.IsEven == isEven)
                    .MinBy(b => Math.Abs(numberId - b.NumberId));

            if (building != null)
            {
                StreetPath p = _caster.Casts[building];
                return Tuple.Create(p, p.GetClosestNode(building.Position));
            }

            building = _caster.Casts.Keys.Where(b => b.Street == street).MinBy(b => Math.Abs(numberId - b.NumberId));

            if (building != null)
            {
                StreetPath p = _caster.Casts[building];
                return Tuple.Create(p, p.GetClosestNode(building.Position));
            }

            StreetPath? path = _map.Paths.Where(p => p.Street == street).MaxBy(p => p.Length);
            if (path != null)
            {
                return Tuple.Create(path, path.From);
            }

            reason = AddressFailureReason.Street;
            return null;
        }

        private bool CutRestrictedAccess()
        {
            List<StreetSegment> segmentsToCut = new();
            foreach (StreetSegment segment in _map.Segments.Where(s => s.From.Count != 1 && s.To.Count != 1 && s.RestrictedAccess)
                .OrderByDescending(s => s.Length))
            {
                IPathfinder<Intersection, SegmentPathway> path =
                    IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, segment.To,
                    w => !w.Segment.IsActive || w.Segment == segment || segmentsToCut.Contains(w.Segment) ? -1 : 1);

                if (!path.Success)
                {
                    continue;
                }

                segmentsToCut.Add(segment);
            }

            List<Tuple<StreetPath, bool>> pathsToCut = new();
            if (pathsToCut.Count == 0)
            {
                return false;
            }

            List<StreetSegment> toRemove = new();
            foreach (var segment in segmentsToCut)
            {
                GetCutPosition(segment, out bool atStart);
                pathsToCut.Add(Tuple.Create(atStart ? segment.Paths[0] : segment.Paths[segment.Paths.Count - 1], atStart));
                toRemove.Add(segment);
            }

            StreetSegment.Remove(toRemove);

            foreach (var tuple in pathsToCut)
            {
                tuple.Item1.Cut(tuple.Item2);
            }

            _creator.CreateSegments();
            return true;
        }

        private bool CutTinyPaths()
        {
            if (!_input.DoNotCrossAlleys)
            {
                return false;
            }

            _splitter.SplitSegments();
            CutTinyPathsBasedOnBuildings();
            return CutTinyPathsBasedOnDistance();
        }

        private void CutTinyPathsBasedOnBuildings()
        {
            List<StreetSegment> segmentsToCut = new();

            List<PolygonCollider>[] colliders = new List<PolygonCollider>[2];
            colliders[0] = new List<PolygonCollider>();
            colliders[1] = new List<PolygonCollider>();

            float minWidth = 3f;

            foreach (StreetSegment segment in _map.Segments
                .Where(s => s.Type == StreetType.Tiny && s.From.Count > 1 && s.To.Count > 1 && s.Length > 10 && !s.Paths.All(p => p.Width > minWidth))
                .OrderByDescending(s => s.Length))
            {
                IPathfinder<Intersection, SegmentPathway> path =
                    IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, segment.To,
                    w => !w.Segment.IsActive || w.Segment == segment || segmentsToCut.Contains(w.Segment) ? -1 : 1);

                if (!path.Success)
                {
                    continue;
                }

                colliders[0].Clear();
                colliders[1].Clear();

                foreach (PolygonCollider c in GetColliders())
                {
                    if (GetDistance(segment, c, minWidth, out float _, out bool isLeft))
                    {
                        colliders[isLeft ? 1 : 0].Add(c);
                    }
                }

                if (colliders[0].Count == 0 || colliders[1].Count == 0)
                {
                    continue;
                }

                float distance = float.MaxValue;
                foreach (PolygonCollider right in colliders[0])
                {
                    foreach (PolygonCollider left in colliders[1])
                    {
                        if (Vector2.Distance(right.Position, left.Position) > minWidth + right.SpanRadius + left.SpanRadius)
                        {
                            continue;
                        }

                        float dist = right.GetDistance(left);
                        if (dist < distance)
                        {
                            distance = dist;
                        }
                    }
                }

                if (distance > minWidth)
                {
                    continue;
                }

                segmentsToCut.Add(segment);
            }

            if (segmentsToCut.Count == 0)
            {
                return;
            }

            List<Tuple<StreetPath, bool>> pathsToCut = new();
            List<StreetSegment> toRemove = new();
            foreach (var segment in segmentsToCut)
            {
                GetCutPosition(segment, out bool atStart);
                pathsToCut.Add(Tuple.Create(atStart ? segment.Paths[0] : segment.Paths[segment.Paths.Count - 1], atStart));
                toRemove.Add(segment);
            }

            StreetSegment.Remove(toRemove);

            foreach (var tuple in pathsToCut)
            {
                tuple.Item1.Cut(tuple.Item2);
            }

            _creator.CreateSegments();
        }

        private bool GetDistance(StreetSegment segment, PolygonCollider collider, float minWidth, out float distance, out bool isLeft)
        {
            isLeft = false;
            distance = float.MaxValue;
            foreach (StreetPath path in segment.Paths)
            {
                float dist = MyMath.DistanceLinePoint(path.From.Position, path.To.Position, collider.Position);
                if (dist < distance)
                {
                    distance = dist;
                }
            }

            if (distance > minWidth + collider.SpanRadius + 1f)
            {
                return false;
            }

            foreach (StreetPath path in segment.Paths)
            {
                float dist = collider.GetDistance(path.From.Position, path.To.Position);
                if (dist < distance)
                {
                    isLeft = path.IsLeftOfPath(collider.Position);
                    distance = dist;
                }
            }

            if (distance > minWidth + 1f)
            {
                return false;
            }

            return true;
        }

        private IEnumerable<PolygonCollider> GetColliders()
        {
            foreach (Building b in _buildings)
            {
                yield return b.Collider;
            }
            foreach (PolygonCollider c in _colliders)
            {
                yield return c;
            }
        }

        private bool CutTinyPathsBasedOnDistance()
        {
            List<StreetSegment> segmentsToCut = new();

            foreach (StreetSegment segment in _map.Segments
                .Where(s => s.Type == StreetType.Tiny && s.From.Count > 2 && s.To.Count > 2 && s.Length > 50f && !s.Paths.All(p => p.Width > 3f))
                .OrderByDescending(s => s.Length))
            {
                IPathfinder<Intersection, SegmentPathway> path =
                    IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, segment.To,
                    w => w.Segment == segment || segmentsToCut.Contains(w.Segment) ? -1 : 1);

                if (!path.Success || 2f * segment.Length + 100 > path.GetLength())
                {
                    continue;
                }

                path = IPathfinder.FindPath<Intersection, SegmentPathway>(segment.From, segment.To,
                    w => !w.Segment.IsActive || w.Segment == segment || segmentsToCut.Contains(w.Segment) ? -1 : 1);

                if (!path.Success)
                {
                    continue;
                }

                segmentsToCut.Add(segment);
            }

            if (segmentsToCut.Count == 0)
            {
                return false;
            }

            List<Tuple<StreetPath, bool>> pathsToCut = new();
            List<StreetSegment> toRemove = new();
            foreach (var segment in segmentsToCut)
            {
                if (GetCutPosition(segment, out bool atStart))
                {
                    pathsToCut.Add(Tuple.Create(atStart ? segment.Paths[0] : segment.Paths[segment.Paths.Count - 1], atStart));
                    toRemove.Add(segment);
                }
            }

            StreetSegment.Remove(toRemove);

            foreach (var tuple in pathsToCut)
            {
                tuple.Item1.Cut(tuple.Item2);
            }

            _creator.CreateSegments();
            return true;
        }

        private bool GetCutPosition(StreetSegment segment, out bool atStart)
        {
            List<float> restrictedPositions = new List<float>();

            float length = 0;
            for (int i = 0; i < segment.Paths.Count; i++)
            {
                StreetPath path = segment.Paths[i];
                if (path.RestrictedAccess)
                {
                    restrictedPositions.Add((length + .5f * path.Length) / segment.Length);
                }
                length += path.Length;
            }

            if (restrictedPositions.Count > 0)
            {
                float average = restrictedPositions.Average();
                atStart = average < .5f;
                return true;
            }

            List<Street> fromStreets = new();
            List<Street> toStreets = new();
            List<StreetPath> done = new() { segment.Paths[0] };
            CollectStreets(segment.From.Streetnode, done, fromStreets, 0);
            done = new() { segment.Paths[segment.Paths.Count - 1] };
            CollectStreets(segment.To.Streetnode, done, toStreets, 0);

            List<MailAddress> addresses = segment.GetMailAddresses().ToList();
            if (addresses.Count == 0)
            {
                atStart = true;
                return !fromStreets.Any(toStreets.Contains);
            }

            bool fromContainsStreet = fromStreets.Contains(addresses[0].Street);
            bool toContainsStreet = toStreets.Contains(addresses[addresses.Count - 1].Street);

            float fromDist = addresses[0].GetDistanceOnSegment();
            float toDist = segment.Length - addresses[addresses.Count - 1].GetDistanceOnSegment();

            atStart = toDist < fromDist;

            if (fromContainsStreet && toContainsStreet)
            {
                return false;
            }

            if (fromContainsStreet && !toContainsStreet)
            {
                atStart = false;
                return true;
            }

            if (!fromContainsStreet && toContainsStreet)
            {
                atStart = true;
                return true;
            }

            if (addresses[0].Street != addresses[addresses.Count - 1].Street)
            {
                return false;
            }

            return true;
        }

        private void CollectStreets(StreetNode node, List<StreetPath> done, List<Street> streets, float dist)
        {
            if (dist > 75)
            {
                return;
            }

            foreach (StreetPath p in node.Paths)
            {
                if (done.Contains(p))
                {
                    continue;
                }

                done.Add(p);

                Street? street = p.Street;
                if (street != null && !streets.Contains(street))
                {
                    streets.Add(street);
                }

                StreetNode other = p.GetOther(node);
                CollectStreets(other, done, streets, dist + p.Length);
            }
        }
    }
}