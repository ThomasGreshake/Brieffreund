//Copyright Thomas Greshake 2026

using System.Collections.ObjectModel;
using System.Numerics;
using Brieffreund.Streetmap;

namespace Brieffreund
{
    internal interface IOnSegmentCreated : IEvent<IOnSegmentCreated>

    { public StreetSegment GetSegment(); }

    internal interface IOnSegmentDeleted : IEvent<IOnSegmentDeleted>

    { public StreetSegment GetSegment(); }

    internal interface IOnSegmentRemoved : IEvent<IOnSegmentRemoved>

    { public StreetSegment GetSegment(); }

    internal interface IOnSegmentIsActiveChanged : IEvent<IOnSegmentIsActiveChanged>

    { public StreetSegment GetSegment(); }

    internal class StreetSegment :
        IOnSegmentCreated, IOnSegmentDeleted, IOnSegmentRemoved, IOnSegmentIsActiveChanged
    {
        internal readonly IStreetMap Map;

        private int _index = -1;
        internal int Index => _index;

        private bool _isActive = false;
        internal bool IsActive => _isActive;

        internal readonly SegmentPathway Forward, Backward;
        internal Intersection From => Backward.Towards;
        internal Intersection To => Forward.Towards;

        internal readonly ReadOnlyCollection<StreetPath> Paths;
        private readonly List<StreetPath> _paths;
        internal bool HasStorages => _paths.Any(p => p.Storages.Count > 0);
        internal bool IsRouteDeadEnd => _paths[0].InternalPathFlags.HasFlag(InternalPathFlags.RouteDeadEnd);
        internal bool IsOnlyConnection => _paths[0].InternalPathFlags.HasFlag(InternalPathFlags.OnlyConnection);
        internal bool IsRequiredOnRoute => _paths[0].InternalPathFlags.HasFlag(InternalPathFlags.RequiredOnRoute);
        internal bool ForceUpgrade => _paths[0].InternalPathFlags.HasFlag(InternalPathFlags.ForceUpgrade);
        internal bool RestrictedAccess => _paths[0].RestrictedAccess;

        internal readonly float Length;

        private TrafficType _traffic;
        internal TrafficType Traffic => _traffic;

        internal readonly StreetType Type;

        private int _leftMailAmount = 0, _rightMailAmount = 0;
        internal int LeftMailAmount => _leftMailAmount;
        internal int RightMailAmount => _rightMailAmount;
        internal int TotalMailAmount => _leftMailAmount + _rightMailAmount;
        internal bool ReceivesMail => TotalMailAmount > 0;

        private float _singlePathingLength = -1f;
        internal float SinglePathingLength => _singlePathingLength;

        private readonly List<MailAddress> _singlePathingOrder = new();
        internal readonly ReadOnlyCollection<MailAddress> SinglePathingOrder;

        //Setup --------------------------------------------------------

        #region Setup

        internal static StreetSegment Create(IStreetMap map, Intersection from, Intersection to, List<StreetPath> paths)
        {
            StreetSegment segment = new(map, from, to, paths);
            IOnSegmentCreated.Call(segment);

            bool isActive = paths[0].IsActive;
            segment.SetIsActive(isActive);

            return segment;
        }

        private StreetSegment(IStreetMap map, Intersection from, Intersection to, List<StreetPath> paths)
        {
            Map = map;

            Type = (StreetType)paths.Max(p => (int)p.Type);
            Length = paths.Sum(p => p.Length);
            Forward = new(this, to);
            Backward = new(this, from);

            _paths = paths;

            foreach (MailAddress post in GetMailAddresses())
            {
                if (post.LeftOfPath)
                {
                    _leftMailAmount += post.MailAmount;
                }
                else
                {
                    _rightMailAmount += post.MailAmount;
                }
            }

            Paths = _paths.AsReadOnly();
            SinglePathingOrder = _singlePathingOrder.AsReadOnly();

            _traffic = (TrafficType)paths.Max(p => (int)p.Traffic);
        }

        private static DirectionType GetDirectionType(List<StreetPath> paths)
        {
            if (paths.Any(p => p.DirectionType == DirectionType.Bothways))
            {
                return DirectionType.Bothways;
            }

            float forwardValue = 0;
            foreach (StreetPath path in paths)
            {
                forwardValue += path.Length * Math.Sign((int)path.DirectionType);
            }

            if (forwardValue < 1 && forwardValue > -1)
            {
                return DirectionType.Bothways;
            }

            int sign = Math.Sign(forwardValue);

            int maxType = paths.Where(p => Math.Sign((int)p.DirectionType) == sign).Max(p => Math.Abs((int)p.DirectionType));
            return (DirectionType)(sign * maxType);
        }

        internal static void SetIsActive(IEnumerable<StreetSegment> list, bool isActive)
        {
            foreach (StreetSegment segment in list)
            {
                segment.SetIsActive(isActive);
            }
        }

        internal static void Delete(IEnumerable<StreetSegment> list)
        {
            foreach (StreetSegment segment in list)
            {
                segment.Delete();
            }
        }

        private void Delete()
        {
            SetIsActive(false);
            IOnSegmentDeleted.Call(this);
        }

        internal static void Remove(IEnumerable<StreetSegment> list)
        {
            foreach (StreetSegment segment in list)
            {
                segment.Remove();
            }
        }

        private void Remove()
        {
            IOnSegmentRemoved.Call(this);
            if (_isActive)
            {
                AdjustIndex();
            }
        }

        static StreetSegment()
        {
            IOnAddressCreated.Register(OnAddressCreated);
            ISinglePathCreator.Register(OnSinglePathCreated);
            ITrafficTypeIdentifier.Register(OnTrafficIdentified);
        }

        #endregion Setup

        //Methods --------------------------------------------------------

        public StreetSegment GetSegment() => this;

        #region Methods

        internal SegmentPathway GetOpposite(SegmentPathway way)
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

        internal Intersection GetOther(Intersection i)
        {
            if (i == From)
            {
                return To;
            }
            if (i == To)
            {
                return From;
            }
            throw new Exception();
        }

        internal IEnumerable<MailAddress> GetMailAddresses()
        {
            foreach (StreetPath path in _paths)
            {
                foreach (MailAddress mail in path.MailAddresses) { yield return mail; }
            }
        }

        internal IEnumerable<Storage> GetStorages()
        {
            foreach (StreetPath path in _paths)
            {
                foreach (Storage storage in path.Storages) { yield return storage; }
            }
        }

        internal IEnumerable<StreetNode> GetNodes()
        {
            yield return From.Streetnode;
            foreach (StreetPath path in _paths)
            {
                yield return path.To;
            }
        }

        internal bool Contains(Street? street)
        {
            return _paths.Any(p => p.Street == street);
        }

        internal Vector2 Lerp(float perc)
        {
            float dist = Length * Math.Clamp(perc, 0f, 1f);
            foreach (var path in _paths)
            {
                float length = path.Length;
                if (dist > length)
                {
                    dist -= length;
                }
                else
                {
                    return path.Lerp(dist / length);
                }
            }
            return To.Position;
        }

        internal float GetAverageWidth() => _paths.Sum(p => p.Width * p.Length) / Length;

        internal float Distance(Vector2 pos, out StreetPath p)
        {
            p = _paths[0];
            float shortest = float.MaxValue;
            foreach (var path in _paths)
            {
                float distance = MyMath.DistanceLinePoint(path.From.Position, path.To.Position, pos);
                if (distance < shortest)
                {
                    shortest = distance;
                    p = path;
                }
            }
            return shortest;
        }

        internal static int GetSidewalkId(StreetSegment segment, bool leftSide) => (leftSide ? 1 : -1) * (segment.Index + 1);

        #endregion Methods

        //Privates ----------------------------------------------------------------------------------

        private void SetIsActive(bool isActive)
        {
            if (isActive == _isActive)
            {
                return;
            }

            if (!isActive && ReceivesMail)
            {
                throw new Exception();
            }

            _isActive = isActive;

            if (isActive)
            {
                _index = Map.Segments.Count;
            }

            IOnSegmentIsActiveChanged.Call(this);

            if (!isActive)
            {
                AdjustIndex();
                _index = -1;
            }
        }

        private void AdjustIndex()
        {
            for (int i = _index; i < Map.Segments.Count; i++)
            {
                Map.Segments[i]._index -= 1;
            }
        }

        #region Listeners

        private static void OnAddressCreated(IOnAddressCreated e)
        {
            Address a = e.GetAddress();
            if (a is MailAddress post)
            {
                StreetSegment segment = a.Segment;

                if (a.LeftOfPath)
                {
                    segment._leftMailAmount += post.MailAmount;
                }
                else
                {
                    segment._rightMailAmount += post.MailAmount;
                }
            }
        }

        private static void OnSinglePathCreated(ISinglePathCreator creator)
        {
            for (int i = 0; i < creator.Map.Segments.Count; i++)
            {
                StreetSegment segment = creator.Map.Segments[i];

                if (i != segment.Index)
                {
                    throw new Exception();
                }

                segment._singlePathingLength = creator.SinglePathingLengths[i];
                IList<MailAddress> list = creator.PathingLists[i];

                if (list.Count == 0)
                {
                    continue;
                }

                segment._singlePathingOrder.Clear();

                foreach (MailAddress address in list)
                {
                    segment._singlePathingOrder.Add(address);
                }
            }
        }

        private static void OnTrafficIdentified(ITrafficTypeIdentifier e)
        {
            foreach (StreetSegment segment in e.Map.GetAllSegments())
            {
                TrafficType type = segment.Paths.Max(p => e.Traffic[p]);
                segment._traffic = type;
            }
        }

        #endregion Listeners
    }
}