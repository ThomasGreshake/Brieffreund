//Copyright Thomas Greshake 2026

namespace Brieffreund
{
    internal interface IEulerMap
    {
        public bool Success { get; }

        public IStreetMap Streetmap { get; }

        public IDictionary<Intersection, EulerNode> Nodes { get; }
        public int NodeCount => Nodes.Count;

        public EulerNode[] StartAndEndNode { get; }

        public IList<EulerPath> GetPaths(StreetSegment segment);

        public int GetPathCount(StreetSegment segment) => GetPaths(segment).Count;

        public int Length { get; }

        public int PassingLength { get; }

        public int TotalLength => Length + PassingLength;

        public IEnumerable<EulerPath> Paths
        {
            get
            {
                foreach (StreetSegment segment in Streetmap.Segments)
                {
                    IList<EulerPath> list = GetPaths(segment);
                    foreach (EulerPath path in list)
                    {
                        yield return path;
                    }
                }
            }
        }

        public int PathCount { get; }

        public float GetLength() => Paths.Sum(p => p.Length);
    }
}