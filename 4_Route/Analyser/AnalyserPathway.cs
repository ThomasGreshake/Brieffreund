//Copyright Thomas Greshake 2026

namespace Brieffreund.Analyser
{
    internal class AnalyserPathway : IPathway<AnalyserNode, AnalyserPathway>
    {
        public readonly AnalyserPath Path;
        public float Length => Path.Length;

        private readonly AnalyserNode _towards;
        public AnalyserNode Towards => _towards;
        public AnalyserNode Origin => Path.GetOther(_towards);

        internal TrafficType Traffic => Path.Traffic;

        internal bool CrossesStreet => Path.PathType == AnalyserPathType.CrossesStreet;

        internal bool RightOfWay => IsOnCorrectSide();

        internal bool RightDirection => HasCorrectDirection();

        internal AnalyserPathway(AnalyserPath path, AnalyserNode towards)
        {
            Path = path;
            _towards = towards;
        }

        private bool HasCorrectDirection()
        {
            if (Path.StreetType == StreetType.Tiny || Path.PathType == AnalyserPathType.CrossesStreet)
            {
                return true;
            }

            switch (Path.DirectionType)
            {
                case DirectionType.OnewayAllR:
                    return Path.Backward == this;

                case DirectionType.OnewayCarsR:
                    return Path.Backward == this || _rightOfWay;

                case DirectionType.Bothways:
                    return true;

                case DirectionType.OnewayCars:
                    return Path.Forward == this || _rightOfWay;

                case DirectionType.OnewayAll:
                    return Path.Forward == this;

                default:
                    return true;
            }
        }

        private bool IsOnCorrectSide()
        {
            if (Path.StreetType == StreetType.Tiny || Path.PathType == AnalyserPathType.CrossesStreet || _rightOfWay)
            {
                return true;
            }

            switch (Path.DirectionType)
            {
                case DirectionType.OnewayAllR:
                    return Path.Backward == this;

                case DirectionType.OnewayCarsR:
                    return Path.Backward == this;

                case DirectionType.Bothways:
                    return false;

                case DirectionType.OnewayCars:
                    return Path.Forward == this;

                case DirectionType.OnewayAll:
                    return Path.Forward == this;

                default:
                    return false;
            }
        }

        private bool _rightOfWay => ((Path.Forward == this) == (Path.PathType == AnalyserPathType.RightOfPath)) || CrossesStreet;
    }
}