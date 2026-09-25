//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStreetSegmentSplitter
    {
        public void SplitSegments();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StreetSegmentSplitter : IStreetSegmentSplitter
    {
        private readonly IStreetMap _map;
        private readonly IStreetSegmentCreator _creator;
        private readonly IAddressRelocator _relocator;

        internal StreetSegmentSplitter(IStreetMap map, IStreetSegmentCreator creator, IAddressRelocator relocator)
        {
            _map = map;
            _creator = creator;
            _relocator = relocator;
        }

        public void SplitSegments()
        {
            List<Tuple<StreetNode, StreetSegment>> splits = new(); //Node to become an intersection, segment it is inside of

            foreach (Storage storage in _map.Storages)
            {
                if (NeedsSplit(storage, out StreetNode storageNode) && !Contains(splits, storageNode))
                {
                    splits.Add(Tuple.Create(storageNode, storage.Segment));
                }
            }

            Address? address = _map.StartAndEndAddresses[0];
            if (address != null && NeedsSplit(address, out StreetNode addNode) && !Contains(splits, addNode))
            {
                splits.Add(Tuple.Create(addNode, address.Segment));
            }
            address = _map.StartAndEndAddresses[1];
            if (address != null && NeedsSplit(address, out addNode) && !Contains(splits, addNode))
            {
                splits.Add(Tuple.Create(addNode, address.Segment));
            }

            foreach (StreetSegment segment in _map.Segments)
            {
                CreateSplitsInSegment(splits, segment);
            }

            List<StreetSegment> segmentsToRemove = new();
            foreach (var split in splits)
            {
                if (segmentsToRemove.Contains(split.Item2))
                {
                    continue;
                }
                segmentsToRemove.Add(split.Item2);
            }

            StreetSegment.Remove(segmentsToRemove);

            foreach (var split in splits)
            {
                Intersection.GetOrCreate(_map, split.Item1);
            }

            _creator.CreateSegments();

            DisableDeadEnds();
            DeleteInactiveSegments();

            _relocator.Relocate();
        }

        private void CreateSplitsInSegment(List<Tuple<StreetNode, StreetSegment>> splits, StreetSegment segment)
        {
            if (!segment.ReceivesMail)
            {
                return;
            }

            if (segment.IsRouteDeadEnd)
            {
                CreateSplitsOnDeadEnd(splits, segment);
                return;
            }

            if (segment.IsOnlyConnection)
            {
                return;
            }

            float longestDiff;
            StreetNode startNode;
            StreetNode endNode;
            if (segment.Traffic == TrafficType.VeryLight)
            {
                longestDiff = GetSplitOption(segment, null, out startNode, out endNode);
            }
            else
            {
                float leftDiff = GetSplitOption(segment, true, out StreetNode leftStartNode, out StreetNode leftEndNode);
                float rightDiff = GetSplitOption(segment, false, out StreetNode rightStartNode, out StreetNode rightEndNode);

                if (leftDiff > rightDiff)
                {
                    longestDiff = leftDiff;
                    startNode = leftStartNode;
                    endNode = rightStartNode;
                }
                else
                {
                    longestDiff = rightDiff;
                    startNode = rightStartNode;
                    endNode = rightEndNode;
                }
            }

            if (longestDiff <= Constants.MAXIMUM_NOBUILDINGS_DISTANCE || startNode == endNode)
            {
                return;
            }

            if (startNode != segment.From.Streetnode)
            {
                splits.Add(Tuple.Create(startNode, segment));
            }
            if (endNode != segment.To.Streetnode)
            {
                splits.Add(Tuple.Create(endNode, segment));
            }
        }

        private float GetSplitOption(StreetSegment segment, bool? leftSide, out StreetNode startNode, out StreetNode endNode)
        {
            startNode = segment.From.Streetnode;
            endNode = segment.To.Streetnode;

            List<MailAddress> mail = segment.GetMailAddresses().ToList();
            MailAddress? address = mail[0];
            if (leftSide == null)
            {
                address = mail[0];
            }
            else
            {
                address = mail.FirstOrDefault(m => m.LeftOfPath == leftSide);
                if (address == null)
                {
                    return -1;
                }
            }
            endNode = address.Path.From;

            float lastDist = address.GetDistanceOnSegment();
            float longestDiff = GetFromToLength(segment, address, true);

            if (!segment.IsRouteDeadEnd && !segment.IsOnlyConnection)
            {
                for (int i = 0; i < mail.Count; i++)
                {
                    MailAddress current = mail[i];
                    if (current == address || (leftSide != null && current.LeftOfPath != leftSide))
                    {
                        continue;
                    }

                    float dist = current.GetDistanceOnSegment();
                    float diff = dist - lastDist;
                    lastDist = dist;

                    if (diff > longestDiff)
                    {
                        longestDiff = diff;
                        startNode = address.Path.To;
                        endNode = current.Path.From;
                    }

                    address = current;
                }
            }

            address = mail[mail.Count - 1];
            float endDiff = GetFromToLength(segment, address, false);
            if (endDiff > longestDiff)
            {
                longestDiff = endDiff;
                startNode = address.Path.To;
                endNode = segment.To.Streetnode;
            }

            return longestDiff;
        }

        private void CreateSplitsOnDeadEnd(List<Tuple<StreetNode, StreetSegment>> splits, StreetSegment segment)
        {
            if (segment.From.Segments.Count(s => s.IsActive) != 1 && segment.To.Segments.Count(s => s.IsActive) != 1)
            {
                return;
            }

            bool fromIsEnd = segment.From.Segments.Count(s => s.IsActive) == 1;
            MailAddress mail = fromIsEnd ? segment.GetMailAddresses().First() : segment.GetMailAddresses().Last();
            StreetNode node = fromIsEnd ? mail.Path.From : mail.Path.To;
            StreetNode interNode = fromIsEnd ? segment.From.Streetnode : segment.To.Streetnode;

            if (node == interNode || Contains(splits, node))
            {
                return;
            }

            splits.Add(Tuple.Create(node, segment));
        }

        private static float GetFromToLength(StreetSegment segment, MailAddress address, bool from)
        {
            Intersection inter = from ? segment.From : segment.To;

            float dist = address.GetDistanceOnSegment();
            if (!from)
            {
                dist = segment.Length - dist;
            }

            if (inter.Count < 2)
            {
                return dist;
            }

            if (inter.Count == 2)
            {
                StreetSegment first = inter.Segments.First();
                StreetSegment last = inter.Segments.Last();
                StreetSegment other = first == segment ? last : first;

                if (!other.ReceivesMail)
                {
                    return dist + other.Length;
                }
            }

            return Math.Max(dist - inter.Radius, 0);
        }

        private static bool NeedsSplit(Address address, out StreetNode node)
        {
            StreetSegment segment = address.Segment;
            node = address.ClosestNode;

            float dist = address.GetDistanceOnSegment();
            return dist - segment.From.Radius > Constants.MAX_DISTANCE_CLOSEST_INTERSECTION
                && segment.Length - segment.To.Radius - dist > Constants.MAX_DISTANCE_CLOSEST_INTERSECTION
                && node != segment.From.Streetnode && node != segment.To.Streetnode;
        }

        private static bool Contains(List<Tuple<StreetNode, StreetSegment>> splits, StreetNode node) => splits.Any(t => t.Item1 == node);

        private void DisableDeadEnds()
        {
            StreetSegment.SetIsActive(_map.Segments
                .Where(s => !s.ReceivesMail && (s.From.Segments.Count(x => x.IsActive) == 1 || s.To.Segments.Count(x => x.IsActive) == 1)).ToList(), false);
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