//Copyright Thomas Greshake 2026

namespace Brieffreund.Eulermap
{
    internal interface IStorageDistributor : IEvent<IStorageDistributor>
    {
        public IList<Tuple<EulerNode, Storage, float>> Storages { get; }

        public void DistributeStorages();
    }
}

namespace Brieffreund.Eulermap.Functions
{
    internal class StorageDistributor : IStorageDistributor
    {
        private readonly IEulerMap _map;

        private readonly List<Tuple<EulerNode, Storage, float>> _storages;
        public IList<Tuple<EulerNode, Storage, float>> Storages => _storages;

        internal StorageDistributor(IEulerMap map)
        {
            _map = map;
            _storages = new(map.Streetmap.Storages.Count);
        }

        public void DistributeStorages()
        {
            foreach (Storage storage in _map.Streetmap.Storages)
            {
                EulerNode closest = _map.Nodes.Values.First();
                float distance = float.MaxValue;

                //The storage is extremly likely to be at an intersection with a street, or else the driver had trouble reaching it
                foreach (EulerNode node in _map.Nodes.Values.Where(n => n.Paths.Any(p => p.Segment.Type != StreetType.Tiny)))
                {
                    float dist = node.Intersection.Streetnode.GetDistance(storage);
                    if (dist < distance)
                    {
                        closest = node;
                        distance = dist;
                    }
                }

                if (distance > Constants.MAX_STORAGE_DISTANCE_METER)
                {
                    continue;
                }

                Tuple<EulerNode, Storage, float> tuple = Tuple.Create(closest, storage, distance);
                _storages.Add(tuple);
            }

            IStorageDistributor.Call(this);
            _storages.Clear();
        }
    }
}