//Copyright Thomas Greshake 2026

namespace Brieffreund
{
    internal class Street
    {
        //Data -------------------------------------------------------------

        internal readonly string Name;

        //Setup ------------------------------------------------------------

        internal static Street GetOrCreate(IDictionary<string, Street> streets, string name) => GetOrCreate(streets, NameToId(name), name);

        private static Street GetOrCreate(IDictionary<string, Street> streets, string id, string name)
        {
            if (streets.TryGetValue(id, out Street? street))
            {
                return street;
            }

            street = new(name);
            streets.Add(id, street);
            return street;
        }

        private Street(string name)
        {
            Name = name;
        }

        //Internals --------------------------------------------------------

        internal static string NameToId(string name) =>
            name.Replace(" ", string.Empty)
            .Replace(".", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .ToLower()
            .Replace("ß", "ss")
            .Replace("strasse", "str")
            .Replace("ö", "oe")
            .Replace("ä", "ae")
            .Replace("ü", "ue");

        internal static int NumberIdToNumber(int numId) => numId / Constants.ADDRESS_ADDON_RANGE;

        internal static bool SplitStreetAddress(string streetAddress, out string streetName, out string fullNumber)
        {
            streetName = string.Concat(streetAddress.TakeWhile(x => !Char.IsDigit(x)));
            fullNumber = streetAddress.Substring(streetName.Length).Trim();
            streetName = streetName.Trim();
            return !string.IsNullOrWhiteSpace(streetName) && !string.IsNullOrWhiteSpace(fullNumber);
        }

        internal static bool FullnumberToNumberId(string fullNumber, out int numberId)
        {
            string[] parts = fullNumber.Split(new char[] { '-', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (!SplitNumberAndAddon(parts[0].Trim(), out int num, out int add))
            {
                numberId = 0;
                return false;
            }
            if (parts.Length > 1)
            {
                add = Constants.ADDRESS_ADDON_RANGE - 1;
            }

            numberId = AddressToId(num, add);
            return true;
        }

        //Privates --------------------------------------------------------

        private static bool SplitNumberAndAddon(string numberAndAddon, out int num, out int addon)
        {
            string numberPart = string.Concat(numberAndAddon.TakeWhile(Char.IsDigit));
            string addonPart = numberAndAddon.Substring(numberPart.Length).Trim();
            if (!int.TryParse(numberPart, out num))
            {
                addon = 0;
                return false;
            }

            if (!string.IsNullOrEmpty(addonPart) && addonPart.Any(Char.IsLetter))
            {
                char add = addonPart.First(Char.IsLetter);
                addon = Math.Clamp(PositionInAlphabet(add), 0, Constants.ADDRESS_ADDON_RANGE);
            }
            else
            {
                addon = 0;
            }
            return true;
        }

        private static int AddressToId(int number, int addonPos) => number * Constants.ADDRESS_ADDON_RANGE + addonPos;

        private static int PositionInAlphabet(char character) => Char.ToUpper(character) - 'A' + 1;
    }
}