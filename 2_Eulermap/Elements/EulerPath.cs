//Copyright Thomas Greshake 2026

using Brieffreund.Eulermap;

namespace Brieffreund
{
    internal interface IOnEulerPathCreated : IEvent<IOnEulerPathCreated>

    { public EulerPath GetPath(); }

    internal interface IOnEulerPathSideDetermined : IEvent<IOnEulerPathSideDetermined>

    { public EulerPath GetPath(); }

    internal class EulerPath : IOnEulerPathCreated, IOnEulerPathSideDetermined
    {
        //Data -----------------------------------------------------------------------

        internal readonly IEulerMap Map;

        internal readonly int Index;

        internal readonly StreetSegment Segment;
        internal float Length => _leftRight ? Segment.SinglePathingLength : Segment.Length;

        internal readonly EulerPathway Forward, Backward;
        internal EulerNode From => Backward.Towards;
        internal EulerNode To => Forward.Towards;

        private bool? _fromLeft, _toLeft;
        internal bool? FromLeft => _fromLeft;
        internal bool? ToLeft => _toLeft;

        private int _mailAmount;
        internal int MailAmount => _mailAmount;

        private bool _leftRight = false, _isOnDeadEnd = false;
        internal bool LeftRight => _leftRight;
        internal bool IsRouteDeadEnd => _isOnDeadEnd;

        public EulerPath GetPath() => this;

        //Setup -----------------------------------------------------------------------
        internal static EulerPath Create(IEulerMap map, StreetSegment segment, EulerNode from, EulerNode to, bool? fromLeft, bool? toLeft)
        {
            EulerPath path = new(map, segment, from, to, fromLeft, toLeft);
            IOnEulerPathCreated.Call(path);
            return path;
        }

        private EulerPath(IEulerMap map, StreetSegment segment, EulerNode from, EulerNode to, bool? fromLeft, bool? toLeft)
        {
            Map = map;
            Index = map.PathCount;

            Segment = segment;
            Forward = new(this, to);
            Backward = new(this, from);

            _fromLeft = fromLeft;
            _toLeft = toLeft;
        }

        static EulerPath()
        {
            IEulerDeadendFinder.Register(OnDeadEndsFound);
        }

        internal void SetSide(bool leftSide)
        {
            if (_fromLeft == leftSide && _toLeft == leftSide)
            {
                return;
            }

            _fromLeft = leftSide;
            _toLeft = leftSide;
            IOnEulerPathSideDetermined.Call(this);
        }

        internal static void SetMailAmounts(IEulerMap map)
        {
            foreach (StreetSegment segment in map.Streetmap.Segments)
            {
                int count = map.GetPathCount(segment);
                if (count == 0)
                {
                    continue;
                }

                if (count == 1)
                {
                    EulerPath path = map.GetPaths(segment).First();
                    path._mailAmount = segment.TotalMailAmount;
                    path._leftRight = segment.LeftMailAmount > 0 && segment.RightMailAmount > 0;
                    continue;
                }

                List<EulerPath> paths = map.GetPaths(segment).ToList();
                foreach (EulerPath path in paths)
                {
                    int amount = path._fromLeft == true ? segment.LeftMailAmount : segment.RightMailAmount;
                    int divider = paths.Count(p => p._fromLeft == path._fromLeft);
                    path._mailAmount = amount / divider;
                }
            }
        }

        //Internals -----------------------------------------------------------------------

        internal EulerPathway GetOpposite(EulerPathway way)
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

        internal EulerNode GetOther(EulerNode node)
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

        //Listeners -----------------------------------------------------------------------

        private static void OnDeadEndsFound(IEulerDeadendFinder e)
        {
            foreach (EulerPath path in e.Map.Paths)
            {
                path._isOnDeadEnd = false;
            }

            foreach (EulerPath path in e.DeadEnds)
            {
                path._isOnDeadEnd = true;
            }
        }
    }
}