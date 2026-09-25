//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface ISinglePathCreator : IEvent<ISinglePathCreator>
    {
        public IStreetMap Map { get; }
        public IList<MailAddress>[] PathingLists { get; }
        public float[] SinglePathingLengths { get; }

        public void GenerateSinglePaths(IList<StreetSegment> ignore);

        public void GenerateSinglePaths()
        {
            GenerateSinglePaths(new List<StreetSegment>());
        }
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class SinglePathCreator : ISinglePathCreator
    {
        private readonly IStreetMap _map;

        public IStreetMap Map
        { get { return _map; } }

        private List<MailAddress>[] _pathingLists;
        public IList<MailAddress>[] PathingLists => _pathingLists;

        private float[] _singlePathingLengths;
        public float[] SinglePathingLengths => _singlePathingLengths;

        internal SinglePathCreator(IStreetMap map)
        {
            _map = map;

            _pathingLists = Array.Empty<List<MailAddress>>();
            _singlePathingLengths = Array.Empty<float>();
        }

        public void GenerateSinglePaths(IList<StreetSegment> ignore)
        {
            _pathingLists = new List<MailAddress>[_map.Segments.Count];
            _singlePathingLengths = new float[_map.Segments.Count];

            foreach (StreetSegment segment in _map.Segments)
            {
                Generate(segment, ignore.Contains(segment));
            }

            ISinglePathCreator.Call(this);
        }

        private void Generate(StreetSegment segment, bool ignore)
        {
            List<MailAddress> mailAddresses = segment.GetMailAddresses().ToList();
            _pathingLists[segment.Index] = mailAddresses;

            bool left = segment.LeftMailAmount > 0;
            bool right = segment.RightMailAmount > 0;

            if (left && right && segment.Traffic != TrafficType.VeryLight)
            {
                _singlePathingLengths[segment.Index] = -1f;
                return;
            }

            if (!left || !right)
            {
                _singlePathingLengths[segment.Index] = segment.Length;
                return;
            }

            if (ignore)
            {
                _singlePathingLengths[segment.Index] = -1f;
                return;
            }

            List<MailAddress> leftList = Generate(segment, mailAddresses, true, out float leftLength);
            List<MailAddress> rightList = Generate(segment, mailAddresses, false, out float rightLength);

            if (leftLength < rightLength)
            {
                _singlePathingLengths[segment.Index] = leftLength;
                _pathingLists[segment.Index] = leftList;
            }
            else
            {
                _singlePathingLengths[segment.Index] = rightLength;
                _pathingLists[segment.Index] = rightList;
            }
        }

        private List<MailAddress> Generate(StreetSegment segment, List<MailAddress> mailAddresses, bool startLeft, out float length)
        {
            List<MailAddress> pathingList = new();

            float passingWidth =
                segment.Paths.Sum(p => (p.GetPassingWidth() + (p.Type == StreetType.Tiny ? 0 : Constants.PASSING_SINGLEPATH_MALUS)) * p.Length)
                / segment.Length;

            List<MailAddress> toDistribute = new(mailAddresses);
            bool lastLeft = startLeft;
            float lastDist = 0;
            length = 0;

            while (toDistribute.Count > 0)
            {
                MailAddress? nextSame = toDistribute.FirstOrDefault(b => b.LeftOfPath == lastLeft && !pathingList.Contains(b));
                MailAddress? nextOther = toDistribute.FirstOrDefault(b => b.LeftOfPath != lastLeft && !pathingList.Contains(b));
                MailAddress? overOther = toDistribute.FirstOrDefault(b => b.LeftOfPath != lastLeft && !pathingList.Contains(b) && b != nextOther);
                MailAddress next;
                if (nextSame != null && nextOther != null)
                {
                    float sameDist = nextSame.GetDistanceOnSegment();
                    float otherDist = nextOther.GetDistanceOnSegment();
                    float overOtherDist = overOther == null ? mailAddresses[mailAddresses.Count - 1].GetDistanceOnSegment() : overOther.GetDistanceOnSegment();
                    if (sameDist < otherDist || (passingWidth > 1.3f * (sameDist - otherDist) && passingWidth > overOtherDist - otherDist))
                    {
                        next = nextSame;
                    }
                    else
                    {
                        next = nextOther;
                    }
                }
                else if (nextSame != null)
                {
                    next = nextSame;
                }
                else if (nextOther != null)
                {
                    next = nextOther;
                }
                else
                {
                    break;
                }

                toDistribute.Remove(next);
                pathingList.Add(next);
                bool nextLeft = next.LeftOfPath;
                float nextDist = next.GetDistanceOnSegment();
                length += Math.Abs(nextDist - lastDist) + (nextLeft != lastLeft ? passingWidth : 0);
                lastLeft = nextLeft;
                lastDist = nextDist;
            }

            length += segment.Length - pathingList[pathingList.Count - 1].GetDistanceOnSegment();
            return pathingList;
        }
    }
}