//Copyright Thomas Greshake 2026

namespace Brieffreund.Routegenerator
{
    internal interface IAddressSorter
    {
        public List<Address> Sort(List<Address> addresses);
    }
}

namespace Brieffreund.Routegenerator.Functions
{
    internal class AddressSorter : IAddressSorter
    {
        internal AddressSorter()
        { }

        public List<Address> Sort(List<Address> addresses)
        {
            if (addresses.Count == 0)
            {
                return addresses;
            }

            Dictionary<int, Dictionary<Street, List<Address>>> lists = SortIntoLists(addresses);
            Dictionary<int, Dictionary<Street, bool>> ascendingInfo = CreateAscendingInfo(lists);
            addresses = SortWithAscendingInfo(addresses, ascendingInfo);

            return addresses;
        }

        private Dictionary<int, Dictionary<Street, List<Address>>> SortIntoLists(List<Address> addresses)
        {
            Dictionary<int, Dictionary<Street, List<Address>>> lists = new();
            foreach (Address address in addresses)
            {
                if (address is Storage)
                {
                    continue;
                }

                int sideId = address.GetSidewalkId();
                if (!lists.TryGetValue(sideId, out Dictionary<Street, List<Address>>? streets))
                {
                    streets = new();
                    lists.Add(sideId, streets);
                }
                if (!streets.TryGetValue(address.Street, out List<Address>? list))
                {
                    list = new();
                    streets.Add(address.Street, list);
                }
                list.Add(address);
            }
            return lists;
        }

        private Dictionary<int, Dictionary<Street, bool>> CreateAscendingInfo(Dictionary<int, Dictionary<Street, List<Address>>> addressesOnSide)
        {
            Dictionary<int, Dictionary<Street, bool>> ascendingInfo = new();
            foreach (var sideStreets in addressesOnSide)
            {
                foreach (var streetLists in sideStreets.Value)
                {
                    List<Address> list = streetLists.Value;
                    bool? isAscending = IsAscending(list);
                    if (isAscending == null)
                    {
                        continue;
                    }

                    if (!ascendingInfo.TryGetValue(sideStreets.Key, out Dictionary<Street, bool>? info))
                    {
                        info = new();
                        ascendingInfo.Add(sideStreets.Key, info);
                    }

                    info.Add(streetLists.Key, isAscending != false);
                }
            }

            return ascendingInfo;
        }

        private bool? IsAscending(List<Address> addresses)
        {
            if (addresses.Count < 2)
            {
                return null;
            }

            int diff = 0;

            for (int i = 0; i < addresses.Count; i++)
            {
                Address a = addresses[i];

                if (a is Storage)
                {
                    continue;
                }

                for (int j = i + 1; j < addresses.Count; j++)
                {
                    Address b = addresses[j];

                    if (b is Storage || (a.Path == b.Path && Math.Abs(a.GetDistanceOnSegment() - b.GetDistanceOnSegment()) < 1))
                    {
                        continue;
                    }

                    diff += Math.Sign(b.NumberId - a.NumberId);
                }
            }

            if (diff == 0)
            {
                return null;
            }
            return diff > 0;
        }

        private List<Address> SortWithAscendingInfo(List<Address> addresses, Dictionary<int, Dictionary<Street, bool>> ascendingInfo)
        {
            List<Address> sorted = new List<Address>(addresses.Count);
            List<Address> group = new List<Address>();

            Street lastStreet = addresses[0].Street;
            int lastSideId = addresses[0].GetSidewalkId();

            for (int i = 0; i < addresses.Count; i++)
            {
                Address current = addresses[i];
                Street street = current.Street;
                int sideId = current.GetSidewalkId();
                bool isStorage = current is Storage;

                if (street != lastStreet || sideId != lastSideId || isStorage)
                {
                    SortSegmentGroup(group, ascendingInfo);
                    sorted.AddRange(group);
                    group.Clear();

                    lastStreet = street;
                    lastSideId = sideId;
                }

                if (isStorage)
                {
                    sorted.Add(current);
                }
                else
                {
                    group.Add(current);
                }
            }

            if (group.Count > 0)
            {
                SortSegmentGroup(group, ascendingInfo);
                sorted.AddRange(group);
            }

            return sorted;
        }

        private void SortSegmentGroup(List<Address> group, Dictionary<int, Dictionary<Street, bool>> ascendingInfo)
        {
            if (group.Count == 0)
            {
                return;
            }

            int sideId = group[0].GetSidewalkId();
            Street street = group[0].Street;

            if (!ascendingInfo.TryGetValue(sideId, out Dictionary<Street, bool>? streetInfo) || !streetInfo.TryGetValue(street, out bool ascending))
            {
                return;
            }

            while (SwapAnAddress(group, ascending))
            {
                continue;
            }
        }

        private bool SwapAnAddress(List<Address> group, bool ascending)
        {
            if (group.Count < 2)
            {
                return false;
            }

            Address last = group[0];
            for (int i = 1; i < group.Count; i++)
            {
                Address current = group[i];

                int diff = current.NumberId - last.NumberId;
                if (diff != 0 && diff > 0 != ascending)
                {
                    float lastDist = last.GetDistanceOnSegment();
                    float currDist = current.GetDistanceOnSegment();

                    if (Math.Abs(currDist - lastDist) < 7f)
                    {
                        group[i - 1] = current;
                        group[i] = last;

                        return true;
                    }
                }

                last = current;
            }

            return false;
        }
    }
}