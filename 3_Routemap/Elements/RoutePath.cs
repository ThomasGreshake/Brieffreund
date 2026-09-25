//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    internal interface IOnRoutePathCreated : IEvent<IOnRoutePathCreated>

    { public RoutePath GetPath(); }

    internal enum RoutePathType : byte
    { Path, NodePath, SingleConnection, LoopConnection }

    internal class RoutePath : IOnRoutePathCreated
    {
        //Data -----------------------------------------------------------------------

        internal readonly RouteMap Map;
        internal readonly BigInteger Signature;

        internal readonly EulerPathway? EulerPathway;
        internal bool IsPathOnNode => EulerPathway == null;

        internal readonly RoutePathType Type;
        internal bool GoesBothWays => Type == RoutePathType.LoopConnection;

        internal readonly RoutePathway Forward, Backward;

        internal RouteNode From => Backward.Towards;
        internal RouteNode To => Forward.Towards;

        public readonly float Length;

        public RoutePath GetPath() => this;

        //Setup -----------------------------------------------------------------------

        internal static RoutePath Create(RouteMap map, RouteNode from, RouteNode to, RoutePathType type, int id, EulerPathway? way = null)
        {
            RoutePath path = new(map, from, to, type, id, way);
            IOnRoutePathCreated.Call(path);
            return path;
        }

        private RoutePath(RouteMap map, RouteNode from, RouteNode to, RoutePathType type, int id, EulerPathway? way)
        {
            Signature = BigInteger.One << id;
            Map = map;
            Forward = new(this, to);
            Backward = new(this, from);
            Type = type;
            EulerPathway = way;
            Length = way == null ? 0 : way.Length;
        }

        //Internals -----------------------------------------------------------------------

        internal RouteNode GetOther(RouteNode node)
        {
            if (node == From)
            {
                return To;
            }
            if (node == To)
            {
                return From;
            }
            throw new Exception();
        }

        internal RoutePathway GetOpposite(RoutePathway way)
        {
            if (way == Forward)
            {
                return Backward;
            }
            if (way == Backward)
            {
                return Forward;
            }
            throw new Exception();
        }
    }
}