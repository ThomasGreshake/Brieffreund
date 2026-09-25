//Copyright Thomas Greshake 2026

using Brieffreund.Eulermap;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Brieffreund
{
    internal interface IOnEulerNodeCreated : IEvent<IOnEulerNodeCreated>

    { public EulerNode GetNode(); }

    internal class EulerNode : IPathnode<EulerNode, EulerPathway>,
        IOnEulerNodeCreated
    {
        //Data -----------------------------------------------------------------------

        internal readonly IEulerMap Map;

        internal readonly int Index;

        internal readonly Intersection Intersection;
        public Vector2 Position => Intersection.Position;

        private readonly float _passingCircumference;
        internal float PassingCircumference => _passingCircumference;

        private List<EulerPathway> _pathways = new();
        internal int Count => _pathways.Count;
        internal IEnumerable<EulerPath> Paths => _pathways.Select(p => p.Eulerpath);
        internal int EulerCount => _pathways.Count + (Intersection.IsStart ? 1 : 0) + (Intersection.IsEnd ? 1 : 0);

        internal readonly ReadOnlyCollection<EulerPathway> Pathways;

        public IList<EulerPathway> GetPaths() => Pathways;

        private readonly List<Tuple<Storage, float>> _storages = new();
        internal readonly ReadOnlyCollection<Tuple<Storage, float>> Storages;
        internal bool HasStorages => _storages.Count > 0;
        internal int StorageUsageCount => _storages.Sum(s => s.Item1.MaxUsageCount);

        public EulerNode GetNode() => this;

        //Setup -----------------------------------------------------------------------
        internal static EulerNode GetOrCreate(IEulerMap map, Intersection intersection)
        {
            if (!map.Nodes.TryGetValue(intersection, out EulerNode? node))
            {
                node = new(map, intersection);
                IOnEulerNodeCreated.Call(node);
            }
            return node;
        }

        private EulerNode(IEulerMap map, Intersection intersection)
        {
            Map = map;
            Index = map.NodeCount;

            Intersection = intersection;

            Pathways = _pathways.AsReadOnly();
            Storages = _storages.AsReadOnly();

            _passingCircumference = intersection.PassingCircumference;
        }

        static EulerNode()
        {
            IOnEulerPathCreated.Register(OnEulerPathCreated);
            IOnEulerPathSideDetermined.Register(OnEulerPathSideDetermined);
            IStorageDistributor.Register(OnStoragesDistributed);
        }

        //Internals -----------------------------------------------------------------------

        internal float GetPassingDistance(EulerPathway from, EulerPathway to)
        {
            StreetPathway fromWay = from.GetIncomingWay();
            StreetPathway toWay = to.GetOutgoingWay();
            return Intersection.Streetnode.GetPassingDistance(fromWay, from.LeftOfWay(false), toWay, to.LeftOfWay(true));
        }

        //Listeners -----------------------------------------------------------------------

        private static void OnEulerPathCreated(IOnEulerPathCreated e)
        {
            EulerPath path = e.GetPath();

            path.From.Add(path.Forward);
            path.To.Add(path.Backward);
        }

        private void Add(EulerPathway way)
        {
            float order = GetOrderScore(way);

            for (int i = 0; i < _pathways.Count; i++)
            {
                EulerPathway current = _pathways[i];
                float currentOrder = GetOrderScore(current);

                if (order < currentOrder)
                {
                    _pathways.Insert(i, way);
                    return;
                }
            }

            _pathways.Add(way);
        }

        private float GetOrderScore(EulerPathway way)
        {
            float score = Intersection.Streetnode.Pathways.IndexOf(way.GetOutgoingWay());
            if (score < 0)
            {
                throw new Exception();
            }

            bool? leftOfWay = way.LeftOfWay(true);
            if (leftOfWay == null)
            {
                score += 0.1f;
            }
            else if (leftOfWay == true)
            {
                score += 0.2f;
            }
            return score;
        }

        private static void OnEulerPathSideDetermined(IOnEulerPathSideDetermined e)
        {
            EulerPath path = e.GetPath();
            path.From.Remove(path);
            path.To.Remove(path);

            OnEulerPathCreated(path);
        }

        private void Remove(EulerPath path)
        {
            _pathways.Remove(path.Backward);
            _pathways.Remove(path.Forward);
        }

        private static void OnStoragesDistributed(IStorageDistributor e)
        {
            foreach (var tuple in e.Storages)
            {
                tuple.Item1._storages.Add(Tuple.Create(tuple.Item2, tuple.Item3));
            }
        }
    }
}