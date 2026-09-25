//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IAddressRelocator : IEvent<IAddressRelocator>
    {
        public IStreetMap Map { get; }
        public IList<Tuple<Intersection, Storage>> Storages { get; }
        public Intersection?[] StartAndEndIntersections { get; }

        public void Relocate();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class AddressRelocator : IAddressRelocator
    {
        private readonly IStreetMap _map;
        public IStreetMap Map => _map;

        private readonly IList<Tuple<Intersection, Storage>> _storages = new List<Tuple<Intersection, Storage>>();
        public IList<Tuple<Intersection, Storage>> Storages => _storages;

        private readonly Intersection?[] _startAndEndIntersections = new Intersection?[2];
        public Intersection?[] StartAndEndIntersections => _startAndEndIntersections;

        internal AddressRelocator(IStreetMap map)
        { _map = map; }

        public void Relocate()
        {
            _storages.Clear();
            foreach (Storage storage in _map.Storages)
            {
                Tuple<Intersection, Storage> pair = Tuple.Create(storage.GetClosestIntersection(), storage);
                _storages.Add(pair);
            }

            _startAndEndIntersections[0] = _map.StartAndEndAddresses[0]?.GetClosestIntersection();
            _startAndEndIntersections[1] = _map.StartAndEndAddresses[1]?.GetClosestIntersection();

            IAddressRelocator.Call(this);
        }
    }
}