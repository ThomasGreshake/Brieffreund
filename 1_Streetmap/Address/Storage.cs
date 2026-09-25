//Copyright Thomas Greshake 2026

using Brieffreund.Streetmap;
using System.Numerics;

namespace Brieffreund
{
    internal class Storage : Address
    {
        //Data -------------------------------------------------------------

        internal readonly int MaxUsageCount;

        private int _index = -1;
        internal int Index => _index;

        //Setup -------------------------------------------------------------

        internal static Storage Create(IStreetMap map, Street street, string fullNumber, StreetPath path, Vector2 position, int usageCount)
        {
            Storage storage = new(map, street, fullNumber, path, position, usageCount, map.Storages.Count);
            IOnAddressCreated.Call(storage);
            return storage;
        }

        private Storage(IStreetMap map, Street street, string fullNumber, StreetPath path, Vector2 position, int usageCount, int index)
            : base(map, street, fullNumber, path, position)
        {
            MaxUsageCount = usageCount;
            _index = index;
        }

        static Storage()
        {
            IStorageTrimmer.Register(OnStoragesRemoved);
        }

        //Listeners -------------------------------------------------------------

        private static void OnStoragesRemoved(IStorageTrimmer e)
        {
            List<Storage> storages = new List<Storage>(e.Map.Storages);

            foreach (Storage s in e.ToRemove)
            {
                storages.Remove(s);
            }

            for (int i = 0; i < storages.Count; i++)
            {
                storages[i]._index = i;
            }
        }
    }
}