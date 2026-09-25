//Copyright Thomas Greshake 2026

namespace Brieffreund.Printer
{
    internal interface IRouteToStringConverter
    {
        public string ConvertRouteToString(IList<Address> addresses);
    }
}

namespace Brieffreund.Printer.Functions
{
    internal class RouteToStringConverter : IRouteToStringConverter
    {
        internal RouteToStringConverter()
        { }

        public string ConvertRouteToString(IList<Address> addresses)
        {
            string result = "";

            int max = Constants.ADDRESS_LINE_LENGTH;
            int lineLength = 0;
            int lineAddressCount = 0;

            Dictionary<Storage, int> storageParts = GetStoragesWithMultipleUsages(addresses);

            Street? street = null;
            for (int i = 0; i < addresses.Count; i++)
            {
                Address address = addresses[i];

                if (address is MailAddress mail)
                {
                    if (mail.Street != street)
                    {
                        street = address.Street;
                        string streetString = Convert(street, result.Length == 0);
                        result += streetString;
                        lineLength = streetString.Length;
                        lineAddressCount = 0;
                    }

                    bool ascending = IsAscending(addresses, i);
                    List<string> numbers = address.GetNumbers(ascending);
                    foreach (string fullNumber in numbers)
                    {
                        int add = fullNumber.Length;
                        if (lineLength + add + 2 > max)
                        {
                            string streetString = Convert(street, false);
                            result += streetString;
                            lineLength = streetString.Length;
                            lineAddressCount = 0;
                        }
                        else if (lineAddressCount > 0)
                        {
                            add += 2;
                            result += ", ";
                        }

                        result += fullNumber;
                        lineLength += add;
                        lineAddressCount++;
                    }
                }
                else if (address is Storage storage)
                {
                    street = null;

                    if (storageParts.TryGetValue(storage, out int part))
                    {
                        part += 1;
                        storageParts[storage] = part;
                    }
                    else
                    {
                        part = 0;
                    }

                    result += Convert(storage, part);
                }
            }

            return result;
        }

        private static string Convert(Street street, bool isFirst)
        {
            string name = street.Name + " ";

            int count = Math.Max(Constants.STREET_PADDING_FOR_PRINT - name.Length, 0);
            for (int i = 0; i < count; i++)
            {
                name += " ";
            }

            return isFirst ? name : "\n" + name;
        }

        private static string Convert(Storage storage, int part)
        {
            string name = "Ablage ─ " + storage.Name + " ";
            if (part > 0)
            {
                name += "─ [Teil " + part + "] ";
            }

            int count = Math.Max(Constants.ADDRESS_LINE_LENGTH - name.Length, 3);
            for (int i = 0; i < count; i++)
            {
                name += "─";
            }

            return "\n\n" + name;
        }

        private static Dictionary<Storage, int> GetStoragesWithMultipleUsages(IEnumerable<Address> addresses)
        {
            List<Storage> usedStorages = new List<Storage>();
            Dictionary<Storage, int> multiStorages = new(usedStorages.Count / 2 + 1);

            foreach (Address a in addresses)
            {
                if (a is not Storage storage)
                {
                    continue;
                }

                usedStorages.Add(storage);
            }

            foreach (Storage storage in usedStorages)
            {
                int count = usedStorages.Count(s => s == storage);
                if (count > 1)
                {
                    multiStorages.TryAdd(storage, 0);
                }
            }

            return multiStorages;
        }

        private static bool IsAscending(IList<Address> addresses, int index)
        {
            Address address = addresses[index];
            int max = Math.Max(index, addresses.Count - index);

            for (int i = 1; i < max; i++)
            {
                int upperIndex = index + i;
                if (upperIndex < addresses.Count)
                {
                    Address upper = addresses[upperIndex];
                    if (IsComparable(upper, address))
                    {
                        return upper.NumberId > address.NumberId;
                    }
                }

                int lowerIndex = index - i;
                if (lowerIndex > 0)
                {
                    Address lower = addresses[lowerIndex];
                    if (IsComparable(lower, address))
                    {
                        return lower.NumberId < address.NumberId;
                    }
                }
            }
            return true;
        }

        private static bool IsComparable(Address a, Address b) => a.Street == b.Street && a.IsEven == b.IsEven;
    }
}