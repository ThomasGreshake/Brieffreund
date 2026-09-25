//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStreetSegmentMerger
    {
        public void Merge();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StreetSegmentMerger : IStreetSegmentMerger
    {
        private readonly IStreetMap _map;
        private readonly IStreetSegmentCreator _creator;
        private readonly IAddressRelocator _relocator;

        internal StreetSegmentMerger(IStreetMap map, IStreetSegmentCreator creator, IAddressRelocator relocator)
        {
            _map = map;
            _creator = creator;
            _relocator = relocator;
        }

        public void Merge()
        {
            List<StreetSegment> toMerge = new List<StreetSegment>();
            List<Intersection> toDelete = new List<Intersection>();
            List<StreetSegment> toDisable = new List<StreetSegment>();

            foreach (Intersection inter in _map.GetAllIntersections())
            {
                if (inter.Count != 2)
                {
                    continue;
                }

                StreetSegment one = inter.Segments.First();
                StreetSegment two = inter.Segments.Last();
                if (one.IsActive != two.IsActive || (one.IsActive && one.RestrictedAccess != two.IsActive && two.RestrictedAccess))
                {
                    continue;
                }

                if (inter.Storages.Count > 0 && inter.Storages.Any(s => RequiredForAddress(inter, s)))
                {
                    continue;
                }

                Address? address = _map.StartAndEndAddresses[0];
                if (inter.IsStart && address != null && RequiredForAddress(inter, address))
                {
                    continue;
                }

                address = _map.StartAndEndAddresses[1];
                if (inter.IsEnd && address != null && RequiredForAddress(inter, address))
                {
                    continue;
                }

                if (one.IsActive && two.IsActive && (one.ReceivesMail || one.IsOnlyConnection || one.IsRequiredOnRoute)
                    != (two.ReceivesMail || two.IsOnlyConnection || two.IsRequiredOnRoute))
                {
                    StreetSegment receivesMail = one.ReceivesMail ? one : two;
                    bool interIsFrom = inter == receivesMail.From;
                    MailAddress last = interIsFrom ? receivesMail.GetMailAddresses().First() : receivesMail.GetMailAddresses().Last();
                    float distance = interIsFrom ? last.GetDistanceOnSegment() : receivesMail.Length - last.GetDistanceOnSegment();
                    StreetSegment receivesNoMail = one.ReceivesMail ? two : one;
                    distance = GetTotalNoMailLength(inter, receivesNoMail, distance);
                    if (distance > Constants.MAXIMUM_NOBUILDINGS_DISTANCE)
                    {
                        continue;
                    }
                }

                toDelete.Add(inter);
                if (!toMerge.Contains(one))
                {
                    toMerge.Add(one);
                }
                if (!toMerge.Contains(two))
                {
                    toMerge.Add(two);
                }
            }

            StreetSegment.SetIsActive(toDisable, false);
            StreetSegment.Remove(toMerge);
            Intersection.Remove(toDelete);

            _creator.CreateSegments();
            _relocator.Relocate();
        }

        private static float GetTotalNoMailLength(Intersection from, StreetSegment to, float distance)
        {
            if (to.ReceivesMail)
            {
                bool interIsFrom = to.From == from;
                MailAddress first = interIsFrom ? to.GetMailAddresses().First() : to.GetMailAddresses().Last();
                float bonus = interIsFrom ? first.GetDistanceOnSegment() : to.Length - first.GetDistanceOnSegment();

                return distance + bonus;
            }

            distance += to.Length;

            Intersection other = to.GetOther(from);
            if (other.Count != 2)
            {
                return distance;
            }

            StreetSegment one = other.Segments.First();
            StreetSegment two = other.Segments.Last();
            StreetSegment next = one == to ? two : one;

            return GetTotalNoMailLength(other, next, distance);
        }

        private static bool RequiredForAddress(Intersection inter, Address? address)
        {
            if (address == null)
            {
                return false;
            }

            float maxDistance = Constants.MAX_DISTANCE_CLOSEST_INTERSECTION;
            float maxLength = 2 * maxDistance;
            if (inter.Pathways[0].Length > maxLength && inter.Pathways[1].Length > maxLength)
            {
                return true;
            }

            StreetSegment segment = address.Segment;

            StreetSegment other = inter.Pathways[0].Segment == segment ? inter.Pathways[1].Segment : inter.Pathways[0].Segment;

            float dist = address.GetDistanceOnSegment();
            if (segment.To != inter)
            {
                dist = segment.Length - dist;
            }

            return (segment.Length + other.Length - dist) > maxDistance && dist > maxDistance;
        }
    }
}