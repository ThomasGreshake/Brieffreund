//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IStorageDistanceCalculator : IEvent<IStorageDistanceCalculator>
    {
        public IDictionary<StreetNode, float[]> NodeDistances { get; }

        public void CalculateStorageDistances();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StorageDistanceCalculator : IStorageDistanceCalculator
    {
        internal const float MAX_DISTANCE = 150;

        private readonly IStreetMap _map;
        public IStreetMap Map => _map;

        private Dictionary<StreetNode, float[]> _nodeDists;
        public IDictionary<StreetNode, float[]> NodeDistances => _nodeDists;

        internal StorageDistanceCalculator(IStreetMap map)
        {
            _map = map;
            _nodeDists = new Dictionary<StreetNode, float[]>();
        }

        public void CalculateStorageDistances()
        {
            int count = _map.Storages.Count;
            if (count == 0)
            {
                return;
            }

            SetDefaultDistances(count);
            CalculateDistances();

            IStorageDistanceCalculator.Call(this);

            Clear();
        }

        private void SetDefaultDistances(int count)
        {
            _nodeDists.EnsureCapacity(_map.Nodes.Count + _map.InactiveNodes.Count);

            foreach (StreetNode node in _map.GetAllNodes())
            {
                float[] distances = new float[count];
                for (int i = 0; i < count; i++)
                {
                    distances[i] = 2 * MAX_DISTANCE;
                }

                _nodeDists[node] = distances;
            }
        }

        private void CalculateDistances()
        {
            foreach (Storage storage in _map.Storages)
            {
                float dist = storage.GetDistanceOnSegment();
                StreetSegment storageSeg = storage.Segment;

                _nodeDists[storageSeg.From.Streetnode][storage.Index] = Math.Max(dist - storageSeg.From.Radius, 0);
                _nodeDists[storageSeg.To.Streetnode][storage.Index] = Math.Max(storageSeg.Length - dist - storageSeg.To.Radius, 0);

                List<StreetNode> nodesToUpdate = new(_map.Nodes.Count / 32) { storageSeg.From.Streetnode, storageSeg.To.Streetnode };

                while (nodesToUpdate.Count > 0)
                {
                    int i = nodesToUpdate.Count - 1;
                    StreetNode current = nodesToUpdate[i];
                    nodesToUpdate.RemoveAt(i);

                    foreach (StreetPath path in current.Paths)
                    {
                        StreetNode other = path.GetOther(current);

                        float distance = _nodeDists[current][storage.Index] + path.Length;
                        if (distance > MAX_DISTANCE || distance >= _nodeDists[other][storage.Index])
                        {
                            continue;
                        }

                        _nodeDists[other][storage.Index] = distance;

                        if (!nodesToUpdate.Contains(other))
                        {
                            nodesToUpdate.Add(other);
                        }
                    }
                }
            }
        }

        private void Clear()
        {
            _nodeDists = new Dictionary<StreetNode, float[]>();
        }
    }
}