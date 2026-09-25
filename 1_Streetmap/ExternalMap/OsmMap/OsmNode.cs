//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund.Streetmap.Osm
{
    internal class OsmNode : IFlagPole
    {
        //Data -------------------------------------------------------------
        internal readonly Vector2 Position;

        private readonly Dictionary<string, string> _flags;
        public IDictionary<string, string> Flags => _flags;

        //Setup ------------------------------------------------------------

        internal OsmNode(Vector2 position, Dictionary<string, string> flags)
        {
            Position = position;
            _flags = flags;
        }
    }
}