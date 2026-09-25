//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    internal class RoutePathway : IPathway<RouteNode, RoutePathway>
    {
        internal readonly RoutePath RoutePath;

        private readonly RouteNode _towards;
        public RouteNode Towards => _towards;
        public RouteNode Origin => RoutePath.GetOther(_towards);
        public float Length => RoutePath.Length;

        internal EulerPathway? EulerPathway => RoutePath.EulerPathway;
        internal EulerPath? Eulerpath => EulerPathway?.Eulerpath;

        internal RoutePathway(RoutePath path, RouteNode towards)
        {
            RoutePath = path;
            _towards = towards;
        }

        internal List<MailAddress> GetMailAddresses()
        {
            EulerPath? path = EulerPathway?.Eulerpath;
            if (path == null)
            {
                return new();
            }

            List<MailAddress> addresses;

            if (path.LeftRight)
            {
                addresses = path.Segment.SinglePathingOrder.ToList();
            }
            else
            {
                addresses = path.Segment.GetMailAddresses().Where(p => p.LeftOfPath == path.ToLeft).ToList();
            }

            if (_towards.Eulernode.Intersection != path.Segment.To)
            {
                addresses.Reverse();
            }

            return addresses;
        }
    }
}