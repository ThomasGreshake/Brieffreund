//Copyright Thomas Greshake 2026

using Brieffreund.AStar;

namespace Brieffreund.Routemap
{
    internal interface IStorageDistributor : IEvent<IStorageDistributor>
    {
        public IDictionary<RouteNode, Tuple<Storage, float>> Storages { get; }

        public void DistributeStorages();
    }
}

namespace Brieffreund.Routemap.Functions
{
    internal class StorageDistributor : IStorageDistributor
    {
        private readonly RouteMap _map;

        private readonly Dictionary<RouteNode, Tuple<Storage, float>> _storages = new();
        public IDictionary<RouteNode, Tuple<Storage, float>> Storages => _storages;

        internal StorageDistributor(RouteMap map)
        {
            _map = map;
        }

        public void DistributeStorages()
        {
            IStreetMap map = _map.EulerMap.Streetmap;
            NodeGraph<StreetNode, StreetPathway> nodeGraph = new(map.GetAllNodes().ToList(), w => 1f);

            foreach (Storage storage in map.Storages)
            {
                foreach (Intersection inter in map.Intersections.Values)
                {
                    if (inter.Streetnode.GetDistance(storage) > Constants.MAX_STORAGE_DISTANCE_METER
                        || !_map.EulerMap.Nodes.TryGetValue(inter, out EulerNode? node))
                    {
                        continue;
                    }

                    AddStorage(nodeGraph, node, storage);
                }
            }

            IStorageDistributor.Call(this);
        }

        private void AddStorage(NodeGraph<StreetNode, StreetPathway> nodeGraph, EulerNode eulerNode, Storage storage)
        {
            //All route nodes of an euler node share the same street node, so one search serves them all
            StreetNode node = eulerNode.Intersection.Streetnode;
            IPathfinder<StreetNode, StreetPathway> path = nodeGraph.FindPath(node, storage.ClosestNode);
            if (!path.Success)
            {
                throw new InvalidOperationException($"No path found from route node at {node.Position} to storage at {storage.ClosestNode.Position}.");
            }

            foreach (RouteNode rn in _map.GetNodes(eulerNode))
            {
                AddStorage(rn, storage, path);
            }
        }

        private void AddStorage(RouteNode rn, Storage storage, IPathfinder<StreetNode, StreetPathway> path)
        {
            StreetNode node = rn.Eulernode.Intersection.Streetnode;

            StreetPathway pointerWay = rn.Pointer.GetIncomingWay();
            bool pointerIsLeft = rn.Pointer.LeftOfWay(false) == true;

            float distance;
            StreetPathway firstWayTowardsStorage;
            bool storageIsLeft;

            if (path.Count == 0)
            {
                if (storage.Path.To == node)
                {
                    firstWayTowardsStorage = storage.Path.Backward;
                    distance = (1 - storage.Path.PercentageOnPath(storage.Position)) * storage.Path.Length;
                }
                else
                {
                    firstWayTowardsStorage = storage.Path.Forward;
                    distance = storage.Path.PercentageOnPath(storage.Position) * storage.Path.Length;
                }
                storageIsLeft = firstWayTowardsStorage.LeftOfWay(storage.Position);
            }
            else
            {
                firstWayTowardsStorage = path.First();

                StreetPathway lastWay = path.Last();
                if (lastWay.Path != storage.Path)
                {
                    if (lastWay.Towards == storage.Path.To)
                    {
                        lastWay = storage.Path.Backward;
                        distance = (1 - storage.Path.PercentageOnPath(storage.Position)) * storage.Path.Length;
                    }
                    else
                    {
                        lastWay = storage.Path.Forward;
                        distance = storage.Path.PercentageOnPath(storage.Position) * storage.Path.Length;
                    }
                }
                else
                {
                    if (lastWay.Towards == storage.Path.To)
                    {
                        distance = (storage.Path.PercentageOnPath(storage.Position) - 1) * storage.Path.Length;
                    }
                    else
                    {
                        distance = -storage.Path.PercentageOnPath(storage.Position) * storage.Path.Length;
                    }
                }
                storageIsLeft = lastWay.LeftOfWay(storage.Position);
            }

            distance += EstimateFullDistance(path, storageIsLeft);
            distance = Math.Max(distance - rn.Eulernode.Intersection.Radius - Constants.STORAGE_FORGIVENESS_DISTANCE, 0.1f * distance);
            distance += node.GetPassingDistance(pointerWay, pointerIsLeft, firstWayTowardsStorage, storageIsLeft);

            if (distance > Constants.MAX_STORAGE_DISTANCE_METER)
            {
                return;
            }

            distance = RescaleDistance(distance);
            distance *= 2;

            if (!_storages.TryGetValue(rn, out Tuple<Storage, float>? value) || value.Item2 > distance)
            {
                _storages[rn] = Tuple.Create(storage, distance);
            }
        }

        private static float EstimateFullDistance(IPathfinder<StreetNode, StreetPathway> path, bool? side)
        {
            float dist = path.GetLength();

            for (int i = 0; i < path.Count - 1; i++)
            {
                StreetPathway current = path[i];
                if (current.Towards.Count <= 2)
                {
                    continue;
                }

                StreetPathway next = path[i + 1];
                dist += current.Towards.GetPassingDistance(current, side, next, side);
            }

            return dist;
        }

        private static float RescaleDistance(float distance)
        {
            if (distance < Constants.STORAGE_RESCALE_DISTANCE)
            {
                return distance;
            }
            return Constants.STORAGE_RESCALE_DISTANCE + Constants.STORAGE_RESCALE_FACTOR * (distance - Constants.STORAGE_RESCALE_DISTANCE);
        }
    }
}