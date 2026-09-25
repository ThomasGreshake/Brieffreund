//Copyright Thomas Greshake 2026

namespace Brieffreund.Streetmap
{
    internal interface IFlagPole
    {
        public IDictionary<string, string> Flags { get; }
    }

    internal interface IExternalMap : IPathMap
    {
        public bool Success { get; }

        public IList<Building> Buildings { get; }

        public IList<PolygonCollider> AdditionalPathColliders { get; }
    }
}