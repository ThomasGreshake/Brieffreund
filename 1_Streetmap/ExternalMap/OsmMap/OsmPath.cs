//Copyright Thomas Greshake 2026

using System.Collections.ObjectModel;

namespace Brieffreund.Streetmap.Osm
{
    internal class OsmPath : IFlagPole
    {
        //Data -------------------------------------------------------------

        public readonly ReadOnlyCollection<long> Nodes;

        private readonly Dictionary<string, string> _flags;
        public IDictionary<string, string> Flags => _flags;

        //Setup ------------------------------------------------------------
        internal OsmPath(List<long> nodes, Dictionary<string, string> flags)
        {
            Nodes = nodes.AsReadOnly();
            _flags = flags;
        }
    }
}