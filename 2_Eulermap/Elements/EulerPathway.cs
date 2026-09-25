//Copyright Thomas Greshake 2026

using System.Numerics;

namespace Brieffreund
{
    internal class EulerPathway : IPathway<EulerNode, EulerPathway>
    {
        internal readonly EulerPath Eulerpath;
        public float Length => Eulerpath.Length;
        internal int Index => Eulerpath.Index;

        private readonly EulerNode _towards;
        public EulerNode Towards => _towards;

        public EulerNode Origin => Eulerpath.GetOther(_towards);

        internal Vector2 Direction => Vector2.Normalize(Towards.Position - Origin.Position);

        internal EulerPathway(EulerPath path, EulerNode towards)
        {
            Eulerpath = path;
            _towards = towards;
        }

        internal StreetPathway GetOutgoingWay() =>
            Eulerpath.Forward == this ? Eulerpath.Segment.Paths[0].Forward : Eulerpath.Segment.Paths[Eulerpath.Segment.Paths.Count - 1].Backward;

        internal StreetPathway GetIncomingWay() =>
            Eulerpath.Forward == this ? Eulerpath.Segment.Paths[Eulerpath.Segment.Paths.Count - 1].Forward : Eulerpath.Segment.Paths[0].Backward;

        internal EulerPathway GetOppositeDirection() => Eulerpath.GetOpposite(this);

        internal bool? LeftOfWay(bool atStartOfWay)
        {
            if (_towards == Eulerpath.To)
            {
                return atStartOfWay ? Eulerpath.FromLeft : Eulerpath.ToLeft;
            }
            return atStartOfWay ? !Eulerpath.ToLeft : !Eulerpath.FromLeft;
        }

        internal float GetSideScore() =>
            (0.6f * Eulerpath.Segment.Length + 0.4f * Eulerpath.Segment.SinglePathingOrder.Count * 10)
            * GetDirectionFactor() * GetTrafficFactor();

        private float GetDirectionFactor()
        {
            StreetSegment segment = Eulerpath.Segment;

            float score = 0;

            foreach (StreetPath path in segment.Paths)
            {
                score += path.Length * GetDirectionFactor(path);
            }

            return score / segment.Length;
        }

        private float GetDirectionFactor(StreetPath path)
        {
            bool rightSide = LeftOfWay(true) == false;
            if (path.Type == StreetType.Tiny)
            {
                return rightSide ? 0.001f : -0.001f;
            }

            DirectionType dir = path.DirectionType;

            if (dir == DirectionType.Bothways)
            {
                if (Eulerpath.LeftRight) //Correct side can be chosen freely
                {
                    return rightSide ? 0.001f : -0.001f;
                }

                return rightSide ? 1f : -1f;
            }

            bool rightDir = Towards == Eulerpath.To;
            if (dir == DirectionType.OnewayCarsR || dir == DirectionType.OnewayAllR)
            {
                rightDir = !rightDir;
            }

            if (dir == DirectionType.OnewayCars || dir == DirectionType.OnewayCarsR)
            {
                if (rightDir)
                {
                    return rightSide ? 1f : -0.001f;
                }

                return rightSide ? 0.001f : -1f;
            }

            return rightDir ? 1f : -1f;
        }

        private float GetTrafficFactor()
        {
            float mult = 1f;

            switch (Eulerpath.Segment.Traffic)
            {
                case TrafficType.VeryHeavy:
                    mult = 1.4f;
                    break;

                case TrafficType.Heavy:
                    mult = 1.25f;
                    break;

                case TrafficType.Medium:
                    mult = 1.1f;
                    break;

                case TrafficType.Light:
                    break;

                case TrafficType.VeryLight:
                    break;

                default:
                    throw new NotImplementedException();
            }

            mult *= 1f -
                0.9f * Eulerpath.Segment.Paths.Where(p => p.InternalPathFlags.HasFlag(InternalPathFlags.TrueDeadEnd)).Sum(p => p.Length)
                / Eulerpath.Segment.Length;

            return mult;
        }
    }
}