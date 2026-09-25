//Copyright Thomas Greshake 2026

namespace Brieffreund.Routegenerator
{
    internal interface IAddressGetter
    {
        public List<Address> GetAddresses();
    }
}

namespace Brieffreund.Routegenerator.Functions
{
    internal class AddressGetter : IAddressGetter
    {
        private readonly RouteReader _route;

        internal AddressGetter(RouteReader route)
        {
            _route = route;
        }

        public List<Address> GetAddresses()
        {
            List<Address> addresses = new();

            if (_route.Count == 0)
            {
                return addresses;
            }

            bool includedUpcomingStorage = false;

            while (true)
            {
                List<Address> currentAddresses = _route.GetCurrentAddresses();

                Storage? storage = _route.LastStorage;

                bool reversed = _route.CurrentPath.EulerPathway?.Eulerpath.Segment.To != _route.NextNode.Eulernode.Intersection;

                if (storage != null && !includedUpcomingStorage)
                {
                    int sideId = storage.GetSidewalkId();

                    if (currentAddresses.Count == 0 || currentAddresses[0].GetSidewalkId() != sideId)
                    {
                        addresses.Add(storage);
                    }
                    else
                    {
                        SortIntoList(currentAddresses, storage, reversed);
                    }
                }

                storage = _route.UpcomingStorage;

                if (storage != null)
                {
                    int sideId = storage.GetSidewalkId();

                    if (currentAddresses.Count == 0 || currentAddresses[currentAddresses.Count - 1].GetSidewalkId() != sideId)
                    {
                        includedUpcomingStorage = false;
                    }
                    else
                    {
                        SortIntoList(currentAddresses, storage, reversed);
                        includedUpcomingStorage = true;
                    }
                }
                else
                {
                    includedUpcomingStorage = false;
                }

                addresses.AddRange(currentAddresses);

                if (!_route.GoNext())
                {
                    break;
                }
            }

            return addresses;
        }

        private static void SortIntoList(List<Address> list, Address address, bool reversedSegmentOrder)
        {
            if (list.Count == 0)
            {
                list.Add(address);
                return;
            }

            StreetSegment segment = address.Segment;

            float distanceOnSegment = (float)Math.Round(address.GetDistanceOnSegment(), 2);
            float lastDist = reversedSegmentOrder ? segment.Length : 0;

            for (int i = 0; i < list.Count; i++)
            {
                Address current = list[i];

                float dist = (float)Math.Round(current.GetDistanceOnSegment(), 2);

                if ((distanceOnSegment > lastDist && distanceOnSegment < dist) || (distanceOnSegment < lastDist && distanceOnSegment > dist))
                {
                    list.Insert(i, address);
                    return;
                }

                if (distanceOnSegment == dist)
                {
                    if (reversedSegmentOrder ? (dist > segment.Length * .5f) : (dist < segment.Length * .5f))
                    {
                        list.Insert(i, address);
                        return;
                    }

                    if (i == list.Count - 1 || list[i + 1].GetDistanceOnSegment() != dist)
                    {
                        list.Insert(i + 1, address);
                        return;
                    }
                }

                lastDist = dist;
            }

            list.Add(address);
        }
    }
}