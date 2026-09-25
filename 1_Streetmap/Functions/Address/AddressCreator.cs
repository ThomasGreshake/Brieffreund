//Copyright Thomas Greshake 2026

using Brieffreund.Streetmap.Functions;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Brieffreund.Streetmap
{
    internal interface IAddressCreator
    {
        public void CreateAddresses();

        public IList<Tuple<string, AddressFailureReason>> FailedToCreate { get; }
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal enum AddressFailureReason
    { Number, Street, None }

    internal class AddressCreator : IAddressCreator
    {
        private readonly IUserInput _input;
        private readonly IBuildingToPathCaster _caster;
        private readonly IStreetMap _map;

        private Dictionary<Street, List<Tuple<Building, StreetPath>>> _castData = new();

        private readonly List<Tuple<string, AddressFailureReason>> _failed = new();
        public IList<Tuple<string, AddressFailureReason>> FailedToCreate => _failed;

        internal AddressCreator(IUserInput input, IStreetMap map, IBuildingToPathCaster caster)
        {
            _input = input;
            _map = map;
            _caster = caster;
        }

        public void CreateAddresses()
        {
            int addressCount = 0;

            CreateData();

            Dictionary<Tuple<Building, StreetPath>, List<string>> buildingAddresses = new();

            foreach (var addressList in _input.MailAddresses)
            {
                Street street = addressList.Key;

                foreach (string num in addressList.Value)
                {
                    if (EstimateBuilding(street, num, out Tuple<Building, StreetPath>? bPath))
                    {
                        if (!buildingAddresses.TryGetValue(bPath, out List<string>? nums))
                        {
                            nums = new List<string>() { num };
                            buildingAddresses.Add(bPath, nums);
                        }
                        else
                        {
                            nums.Add(num);
                        }
                    }
                    else if (EstimateAddress(street, num, out StreetPath? path, out Vector2 position, out int mailAmount))
                    {
                        MailAddress.Create(_map, street, num, mailAmount, path, position);
                        addressCount++;
                    }
                }
            }

            foreach (var bA in buildingAddresses)
            {
                Building building = bA.Key.Item1;
                StreetPath path = bA.Key.Item2;
                List<string> nums = bA.Value;
                int mailAmount = (int)EstimateMailAmount(building);

                MailAddress mailAddress = MailAddress.Create(_map, building.Street, nums[0], mailAmount, path, building.Position);
                addressCount++;
                for (int i = 1; i < nums.Count; i++)
                {
                    mailAddress.Add(nums[i]);
                    addressCount++;
                }
            }

            int storageCount = 0;
            foreach (var storage in _input.Storages)
            {
                Street street = storage.Item1;
                string num = storage.Item2;
                int count = storage.Item3;

                if (EstimateAddress(street, num, out StreetPath? path, out Vector2 position, out int _))
                {
                    Storage.Create(_map, street, num, path, position, count);
                    storageCount++;
                }
            }
        }

        private void CreateData()
        {
            _caster.CastBuildings();

            foreach (var kvp in _caster.Casts)
            {
                Street street = kvp.Key.Street;
                if (!_castData.TryGetValue(street, out List<Tuple<Building, StreetPath>>? list))
                {
                    list = new List<Tuple<Building, StreetPath>>();
                    _castData.Add(street, list);
                }

                list.Add(Tuple.Create(kvp.Key, kvp.Value));
            }
        }

        private bool EstimateBuilding(Street street, string num, [MaybeNullWhen(false)] out Tuple<Building, StreetPath> bPath)
        {
            bPath = null;

            if (!Street.FullnumberToNumberId(num, out int numId))
            {
                _failed.Add(Tuple.Create(street.Name + " " + num, AddressFailureReason.Number));
                return false;
            }

            bool isEven = Street.NumberIdToNumber(numId) % 2 == 0;

            if (!_castData.TryGetValue(street, out List<Tuple<Building, StreetPath>>? list) || list.Count == 0)
            {
                return false;
            }

            bPath = list.Where(t => t.Item1.IsEven == isEven).MinBy(t => Math.Abs(t.Item1.NumberId - numId));

            return bPath != null;
        }

        private bool EstimateAddress(Street street, string num, [MaybeNullWhen(false)] out StreetPath path, out Vector2 position, out int mailAmount)
        {
            path = null;
            position = Vector2.Zero;
            mailAmount = 100;

            if (!Street.FullnumberToNumberId(num, out int numId))
            {
                _failed.Add(Tuple.Create(street.Name + " " + num, AddressFailureReason.Number));
                return false;
            }

            bool isEven = Street.NumberIdToNumber(numId) % 2 == 0;
            bool leftSide = isEven;

            if (_castData.TryGetValue(street, out List<Tuple<Building, StreetPath>>? list) && list.Count > 0) //Same as EstimateBuilding but needed here for storages
            {
                Tuple<Building, StreetPath>? tuple = list.Where(t => t.Item1.IsEven == isEven).MinBy(t => Math.Abs(t.Item1.NumberId - numId));
                if (tuple != null)
                {
                    Building building = tuple.Item1;
                    path = tuple.Item2;
                    position = building.Position;
                    mailAmount = (int)EstimateMailAmount(building);
                    return true;
                }

                tuple = list.MinBy(t => Math.Abs(t.Item1.NumberId - numId));
                if (tuple != null)
                {
                    path = tuple.Item2;
                    leftSide = !tuple.Item2.IsLeftOfPath(tuple.Item1.Position);
                }
            }

            if (path == null)
            {
                path = _map.Paths.Where(p => p.Street == street).MaxBy(p => p.Length);
                if (path == null)
                {
                    _failed.Add(Tuple.Create(street.Name + " " + num, AddressFailureReason.Street));
                    return false;
                }
            }

            Vector2 perp = MyMath.Perpendicular(path.Direction);
            position = path.Lerp(numId / 1024f) + 10 * (leftSide ? perp : -perp);
            return true;
        }

        private float EstimateMailAmount(Building building) // 100 should be the rough average for a home address
        {
            float baseEstimate = (float)Math.Sqrt(100 * Math.Clamp(building.TotalArea, 100, 3500));

            switch (building.Type)
            {
                case BuildingType.Home:
                    return 80 + 0.3f * baseEstimate;

                case BuildingType.Secondary:
                    return 0.2f * baseEstimate;

                case BuildingType.Apartments:
                    return 10 + 0.55f * 0.9f * baseEstimate;

                case BuildingType.Residential:
                    return 30 + 0.65f * 0.7f * baseEstimate;

                case BuildingType.Primary:
                    return 150 + 0.05f * baseEstimate;

                case BuildingType.Address:
                    return 50 + 0.05f * baseEstimate;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}