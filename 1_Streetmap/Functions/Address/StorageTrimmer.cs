//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStorageTrimmer : IEvent<IStorageTrimmer>
    {
        public IStreetMap Map { get; }

        public IList<Storage> ToRemove { get; }

        public void TrimStorages();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StorageTrimmer : IStorageTrimmer
    {
        private readonly IStreetMap _map;
        public IStreetMap Map => _map;

        private readonly IStorageDistanceCalculator _distanceCalculator;

        private readonly List<Storage> _toRemove = new();
        public IList<Storage> ToRemove => _toRemove;

        internal StorageTrimmer(IStreetMap map, IStorageDistanceCalculator distanceCalculator)
        {
            _map = map;
            _distanceCalculator = distanceCalculator;
        }

        public void TrimStorages()
        {
            _distanceCalculator.CalculateStorageDistances();

            foreach (Storage storage in _map.Storages)
            {
                float minDistance = float.MaxValue;

                foreach (StreetSegment segment in _map.Segments.Where(s => s.ReceivesMail))
                {
                    float dist = segment.From.Streetnode.GetDistance(storage);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                    }

                    dist = segment.To.Streetnode.GetDistance(storage);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                    }

                    if (minDistance < Constants.MAX_STORAGE_DISTANCE_METER)
                    {
                        break;
                    }
                }

                if (minDistance > Constants.MAX_STORAGE_DISTANCE_METER)
                {
                    _toRemove.Add(storage);
                }
            }

            IStorageTrimmer.Call(this);
        }
    }
}