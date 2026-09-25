//Copyright Thomas Greshake 2026

using Brieffreund.Routemap;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace Brieffreund
{
    internal interface IOnRouteNodeCreated : IEvent<IOnRouteNodeCreated>

    { public RouteNode GetNode(); }

    internal class RouteNode : IPathnode<RouteNode, RoutePathway>,
        IOnRouteNodeCreated
    {
        //Data -----------------------------------------------------------------------

        internal readonly RouteMap Map;

        internal readonly EulerNode Eulernode;
        public Vector2 Position => Eulernode.Position;

        private readonly List<RoutePathway> _pathways = new();
        internal readonly ReadOnlyCollection<RoutePathway> Pathways;
        internal int Count => _pathways.Count;
        internal IEnumerable<RoutePath> Paths => _pathways.Select(w => w.RoutePath);

        public IList<RoutePathway> GetPaths() => Pathways;

        private Tuple<Storage, float>? _storage = null;
        internal Tuple<Storage, float>? Storage => _storage;

        internal readonly EulerPathway Pointer;

        public RouteNode GetNode() => this;

        //Setup -----------------------------------------------------------------------
        internal static RouteNode Create(RouteMap map, EulerNode eulerNode, EulerPathway pointer)
        {
            RouteNode node = new RouteNode(map, eulerNode, pointer);
            IOnRouteNodeCreated.Call(node);
            return node;
        }

        private RouteNode(RouteMap map, EulerNode node, EulerPathway pointer)
        {
            Debug.Assert(pointer.Towards == node);

            Map = map;
            Eulernode = node;
            Pointer = pointer;

            Pathways = _pathways.AsReadOnly();
        }

        static RouteNode()
        {
            IOnRoutePathCreated.Register(OnRoutePathCreated);
            IStorageDistributor.Register(OnStoragesDistributed);
        }

        //Internals -----------------------------------------------------------------------
        internal float GetPassingDistance(RoutePathway from, RoutePathway to)
        {
            EulerPathway? fromWay = from.RoutePath.EulerPathway;
            EulerPathway? toWay = to.RoutePath.EulerPathway;

            if (fromWay == null || toWay == null)
            {
                return 0f;
            }

            return Eulernode.GetPassingDistance(fromWay, toWay);
        }

        internal float GetPassingDistance(RouteNode to) => Eulernode.GetPassingDistance(Pointer, to.Pointer.GetOppositeDirection());

        internal Vector2 EstimateTruePosition()
        {
            Vector2 pointerDir = -Vector2.Normalize(Pointer.GetIncomingWay().Direction);

            Vector2 side = (Pointer.LeftOfWay(false) == true ? -1 : 1) * MyMath.Perpendicular(pointerDir);

            Vector2 offset = (pointerDir + side) * Eulernode.Intersection.Radius / 1.41f;

            return Position + offset;
        }

        //Listeners -----------------------------------------------------------------------
        private static void OnRoutePathCreated(IOnRoutePathCreated e)
        {
            RoutePath path = e.GetPath();

            path.From._pathways.Add(path.Forward);
            if (path.GoesBothWays)
            {
                path.To._pathways.Add(path.Backward);
            }
        }

        private static void OnStoragesDistributed(IStorageDistributor e)
        {
            foreach (var nSto in e.Storages)
            {
                RouteNode node = nSto.Key;
                Tuple<Storage, float> storage = nSto.Value;
                node._storage = storage;
            }
        }
    }
}