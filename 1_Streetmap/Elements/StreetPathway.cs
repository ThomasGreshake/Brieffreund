//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    internal class StreetPathway : IPathway<StreetNode, StreetPathway>
    {
        internal readonly StreetPath Path;
        public float Length => Path.Length;

        private readonly StreetNode _towards;
        public StreetNode Towards => _towards;
        public StreetNode Origin => Path.GetOther(_towards);

        internal Vector2 Direction => _towards.Position - Origin.Position;

        internal StreetPathway(StreetPath path, StreetNode towards)
        {
            Path = path;
            _towards = towards;
        }

        internal float GetPassingWidth() => Path.GetPassingWidth();

        internal StreetPathway GetOpposite() => Path.GetOpposite(this);

        internal bool LeftOfWay(Vector2 position) => MyMath.IsToTheLeft(Origin.Position, _towards.Position, position);
    }
}