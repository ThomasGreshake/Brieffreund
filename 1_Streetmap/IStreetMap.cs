//Copyright Thomas Greshake 2026

using System.Diagnostics.CodeAnalysis;

namespace Brieffreund
{
    internal interface IPathMap
    {
        public IList<StreetNode> Nodes { get; }
        public IList<StreetNode> InactiveNodes { get; }
        public IList<StreetPath> Paths { get; }
        public IList<StreetPath> InactivePaths { get; }

        public IEnumerable<StreetNode> GetAllNodes()
        {
            foreach (StreetNode n in Nodes)
            {
                yield return n;
            }
            foreach (StreetNode n in InactiveNodes)
            {
                yield return n;
            }
        }

        public IEnumerable<StreetPath> GetAllPaths()
        {
            foreach (StreetPath p in Paths)
            {
                yield return p;
            }
            foreach (StreetPath p in InactivePaths)
            {
                yield return p;
            }
        }
    }

    internal interface IStreetMap : IPathMap
    {
        public bool Success { get; }

        public IList<StreetSegment> Segments { get; }

        public IList<StreetSegment> InactiveSegments { get; }

        public IDictionary<StreetNode, Intersection> Intersections { get; }

        public IDictionary<StreetNode, Intersection> InactiveIntersections { get; }

        public MailAddress?[] StartAndEndAddresses { get; }

        public Intersection[] StartAndEndIntersections { get; }

        public IList<Storage> Storages { get; }

        public int TotalMailAmount { get; }

        public IEnumerable<Intersection> GetAllIntersections()
        {
            foreach (Intersection i in Intersections.Values)
            {
                yield return i;
            }
            foreach (Intersection i in InactiveIntersections.Values)
            {
                yield return i;
            }
        }

        public IEnumerable<StreetSegment> GetAllSegments()
        {
            foreach (StreetSegment s in Segments)
            {
                yield return s;
            }
            foreach (StreetSegment s in InactiveSegments)
            {
                yield return s;
            }
        }

        public bool IsIntersection(StreetNode node) => Intersections.ContainsKey(node) || InactiveIntersections.ContainsKey(node);

        public bool TryGetIntersection(StreetNode node, [MaybeNullWhen(false)] out Intersection intersection)
            => Intersections.TryGetValue(node, out intersection) || InactiveIntersections.TryGetValue(node, out intersection);
    }
}