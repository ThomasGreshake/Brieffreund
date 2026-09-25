//Copyright Thomas Greshake 2026

using Brieffreund.Streetmap;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlTypes;
using System.IO;
using System.Net;
using System.Numerics;

namespace Brieffreund
{
    internal interface IOnPathCreated : IEvent<IOnPathCreated>

    { public StreetPath GetPath(); }

    internal interface IOnPathDeleted : IEvent<IOnPathDeleted>

    { public StreetPath GetPath(); }

    internal interface IOnPathIsActiveChanged : IEvent<IOnPathIsActiveChanged>

    { public StreetPath GetPath(); }

    internal interface IOnPathCut : IEvent<IOnPathCut>
    {
        public StreetNode GetOldNode();

        public StreetPathway GetOldWay();

        public StreetPathway GetNewWay();
    }

    internal enum StreetType : byte
    {
        Primary = 4, Secondary = 3, Tertiary = 2, Minor = 1, Tiny = 0
    }

    internal enum TrafficType : byte
    {
        VeryHeavy = 4, Heavy = 3, Medium = 2, Light = 1, VeryLight = 0
    }

    internal enum DirectionType : sbyte
    {
        OnewayAllR = -2, OnewayCarsR = -1, Bothways = 0, OnewayCars = 1, OnewayAll = 2
    }

    [Flags]
    internal enum ExternalPathFlags : byte
    {
        None = 0,
        NoAccess = 1 << 0,
        HasSteps = 1 << 1,
        TurnLane = 1 << 2
    }

    [Flags]
    internal enum InternalPathFlags : byte
    {
        None = 0,
        RouteDeadEnd = 1 << 0,
        TrueDeadEnd = 1 << 1,
        OnlyConnection = 1 << 2,
        RequiredOnRoute = 1 << 3,
        ForceUpgrade = 1 << 4
    }

    internal class StreetPath :
        IOnPathCreated, IOnPathDeleted, IOnPathIsActiveChanged
    {
        //Data -------------------------------------------------------------

        internal readonly IPathMap Map;

        private int _index = -1;
        internal int Index => _index;

        private bool _isActive = false;
        internal bool IsActive => _isActive;

        internal readonly StreetType Type;

        private TrafficType _traffic = TrafficType.VeryLight;
        internal TrafficType Traffic => _traffic;

        private StreetPathway _forward, _backward;
        internal StreetPathway Forward => _forward;
        internal StreetPathway Backward => _backward;
        internal StreetNode From => _backward.Towards;
        internal StreetNode To => _forward.Towards;

        private readonly float _length;
        public float Length => _length;

        internal Vector2 Direction => Vector2.Normalize(To.Position - From.Position);

        internal readonly float Width;

        private DirectionType _directionType;
        internal DirectionType DirectionType => _directionType;

        private Street? _street;
        internal Street? Street => _street;

        private StreetSegment? _segment;
        internal StreetSegment? Segment => _segment;

        private InternalPathFlags _internalFlags = InternalPathFlags.None;
        internal InternalPathFlags InternalPathFlags => _internalFlags;

        private readonly List<Storage> _storageAddresses = new();
        internal readonly ReadOnlyCollection<Storage> Storages;

        private readonly List<MailAddress> _mailAddresses = new();
        internal readonly ReadOnlyCollection<MailAddress> MailAddresses;

        internal readonly ExternalPathFlags Flags;
        internal bool NoAccess => Flags.HasFlag(ExternalPathFlags.NoAccess);
        internal bool HasSteps => Flags.HasFlag(ExternalPathFlags.HasSteps);
        internal bool IsTurnLane => Flags.HasFlag(ExternalPathFlags.TurnLane);
        internal bool RestrictedAccess => NoAccess || HasSteps || From.HasBarrier || To.HasBarrier;

        public StreetPath GetPath() => this;

        //Setup ------------------------------------------------------------

        internal static StreetPath Create(IPathMap map, StreetNode from, StreetNode to, StreetType type, Street? street, float width, DirectionType directionType,
            ExternalPathFlags flags, bool isActive = true)
        {
            StreetPath path = new(map, from, to, type, street, width, directionType, flags);
            IOnPathCreated.Call(path);
            path.SetIsActive(isActive);
            return path;
        }

        private StreetPath(IPathMap map, StreetNode from, StreetNode to, StreetType type, Street? street, float width, DirectionType directionType, ExternalPathFlags flags)
        {
            Map = map;
            _index = map.Paths.Count;

            Type = type;
            Width = width;
            _directionType = directionType;
            Flags = flags;

            _forward = new(this, to);
            _backward = new(this, from);
            _street = street;
            _length = Vector2.Distance(from.Position, to.Position);

            int typeToInt = (int)type;
            if (typeToInt > 1)
            {
                _traffic = (TrafficType)typeToInt;
            }

            Storages = _storageAddresses.AsReadOnly();
            MailAddresses = _mailAddresses.AsReadOnly();
        }

        static StreetPath()
        {
            IOnAddressCreated.Register(OnAddressCreated);
            IOnSegmentCreated.Register(OnSegmentCreated);
            IOnSegmentRemoved.Register(OnSegmentRemoved);
            IOnSegmentDeleted.Register(OnSegmentDeleted);
            IInternalFlagSetter.Register(OnInternalFlagsSet);
            ITrafficTypeIdentifier.Register(OnTrafficIdentified);
            IStreetIdentifier.Register(OnStreetsIdentified);
            IOnSegmentIsActiveChanged.Register(OnSegmentIsActiveChanged);
            IStorageTrimmer.Register(OnStoragesRemoved);
        }

        internal static void Delete(IEnumerable<StreetPath> list)
        {
            foreach (var path in list)
            {
                path.Delete();
            }
        }

        private void Delete()
        {
            if (_segment != null || _mailAddresses.Count != 0 || _storageAddresses.Count != 0)
            {
                throw new Exception();
            }

            SetIsActive(false);

            IOnPathDeleted.Call(this);
        }

        //Internals ---------------------------------------------------------

        internal StreetNode GetOther(StreetNode node)
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

        internal Vector2 Lerp(float perc) =>
            From.Position + Math.Clamp(perc, 0, 1) * (To.Position - From.Position);

        internal float PercentageOnPath(Vector2 pos) =>
            MyMath.PercentageOnLine(From.Position, To.Position, pos);

        internal Vector2 CastPosition(Vector2 pos) =>
            Lerp(PercentageOnPath(pos));

        internal float GetPassingWidth()
        {
            float width;

            switch (_traffic)
            {
                case TrafficType.VeryHeavy:
                    width = Constants.PASSING_VERY_HEAVY;
                    break;

                case TrafficType.Heavy:
                    width = Constants.PASSING_HEAVY;
                    break;

                case TrafficType.Medium:
                    width = Constants.PASSING_MEDIUM;
                    break;

                case TrafficType.Light:
                    width = Constants.PASSING_LIGHT;
                    break;

                case TrafficType.VeryLight:
                    width = Type == StreetType.Tiny ? Constants.PASSING_VERY_LIGHT_AND_TINY : Constants.PASSING_VERY_LIGHT;
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (_internalFlags.HasFlag(InternalPathFlags.TrueDeadEnd))
            {
                width *= Constants.DEADEND_MULT;
            }

            width += Width;

            if (!_isActive)
            {
                width *= Constants.INACTIVE_PASSING_MULT;
            }

            return width;
        }

        internal bool IsLeftOfPath(Vector2 pos) => MyMath.IsToTheLeft(From.Position, To.Position, pos);

        internal float DistanceTo(Vector2 pos) => MyMath.DistanceLinePoint(From.Position, To.Position, pos);

        internal StreetNode GetClosestNode(Vector2 position) => PercentageOnPath(position) <= 0.5f ? From : To;

        internal void Reverse()
        {
            StreetPathway old = _forward;
            _forward = _backward;
            _backward = old;

            _mailAddresses.Reverse();
            _directionType = (DirectionType)(-1 * (int)_directionType);
        }

        internal StreetPathway GetOpposite(StreetPathway way)
        {
            if (way == _forward)
            {
                return _backward;
            }
            if (way == _backward)
            {
                return _forward;
            }
            throw new Exception();
        }

        internal void Cut(bool atStart)
        {
            if (_segment != null)
            {
                throw new Exception();
            }

            StreetNode oldNode = atStart ? From : To;
            if (oldNode.Count == 1)
            {
                return;
            }

            bool isActive = _isActive;
            SetIsActive(false);

            StreetNode newNode = StreetNode.Create(Map, oldNode.Position, oldNode.Flags);
            StreetPathway newWay = new StreetPathway(this, newNode);
            StreetPathway oldWay;
            if (atStart)
            {
                oldWay = _backward;
                _backward = newWay;
            }
            else
            {
                oldWay = _forward;
                _forward = newWay;
            }

            PathCutEvent.OnPathCut(oldNode, oldWay, newWay);
            SetIsActive(isActive);
        }

        //Listeners ---------------------------------------------------------

        private static void OnSegmentCreated(IOnSegmentCreated e)
        {
            StreetSegment segment = e.GetSegment();
            foreach (StreetPath path in segment.Paths)
            {
                path._segment = segment;
            }
        }

        private static void OnSegmentRemoved(IOnSegmentRemoved e)
        {
            StreetSegment segment = e.GetSegment();
            foreach (StreetPath path in segment.Paths)
            {
                path._segment = null;
            }
        }

        private static void OnSegmentDeleted(IOnSegmentDeleted e)
        {
            StreetSegment segment = e.GetSegment();
            foreach (StreetPath path in segment.Paths)
            {
                path._segment = null;
                path.Delete();
            }
        }

        private static void OnSegmentIsActiveChanged(IOnSegmentIsActiveChanged e)
        {
            StreetSegment segment = e.GetSegment();
            bool isActive = segment.IsActive;

            foreach (StreetPath path in segment.Paths)
            {
                path.SetIsActive(isActive);
            }
        }

        private void SetIsActive(bool isActive)
        {
            if (_isActive == isActive)
            {
                return;
            }

            _isActive = isActive;

            if (isActive)
            {
                _index = Map.Paths.Count;
            }

            IOnPathIsActiveChanged.Call(this);

            if (!isActive)
            {
                AdjustIndex();
                _index = -1;
            }
        }

        private void AdjustIndex()
        {
            for (int i = _index; i < Map.Paths.Count; i++)
            {
                Map.Paths[i]._index -= 1;
            }
        }

        private static void OnInternalFlagsSet(IInternalFlagSetter e)
        {
            foreach (StreetSegment seg in e.Map.Segments)
            {
                InternalPathFlags flags = e.Flags[seg.Index];

                foreach (StreetPath path in seg.Paths)
                {
                    path._internalFlags = flags;

                    if (flags.HasFlag(InternalPathFlags.TrueDeadEnd) || flags.HasFlag(InternalPathFlags.RouteDeadEnd))
                    {
                        path._directionType = DirectionType.Bothways;
                    }
                }
            }
        }

        private static void OnAddressCreated(IOnAddressCreated e)
        {
            Address address = e.GetAddress();
            if (address is MailAddress post)
            {
                address.Path.Add(post);
            }
            else if (address is Storage storage)
            {
                address.Path._storageAddresses.Add(storage);
            }
        }

        private void Add(MailAddress post)
        {
            float perc = GetUnclampedPercentageOnPath(post);
            for (int i = 0; i < _mailAddresses.Count; i++)
            {
                if (GetUnclampedPercentageOnPath(_mailAddresses[i]) > perc)
                {
                    _mailAddresses.Insert(i, post);
                    return;
                }
            }
            _mailAddresses.Add(post);
        }

        private float GetUnclampedPercentageOnPath(Address address)
            => MyMath.PercentageOnLineUnclamped(address.Path.From.Position, address.Path.To.Position, address.Position);

        private static void OnTrafficIdentified(ITrafficTypeIdentifier e)
        {
            foreach (var pathTraffic in e.Traffic)
            {
                pathTraffic.Key._traffic = pathTraffic.Value;
            }
        }

        private static void OnStreetsIdentified(IStreetIdentifier e)
        {
            foreach (var path in e.IdentifiedStreets)
            {
                path.Key._street = path.Value;
            }
        }

        private static void OnStoragesRemoved(IStorageTrimmer e)
        {
            foreach (Storage s in e.ToRemove)
            {
                s.Path._storageAddresses.Remove(s);
            }
        }

        private struct PathCutEvent : IOnPathCut
        {
            private readonly StreetNode _oldNode;
            private readonly StreetPathway _oldWay, _newWay;

            internal static PathCutEvent OnPathCut(StreetNode oldNode, StreetPathway oldWay, StreetPathway newWay)
            {
                PathCutEvent e = new PathCutEvent(oldNode, oldWay, newWay);
                IOnPathCut.Call(e);
                return e;
            }

            private PathCutEvent(StreetNode oldNode, StreetPathway oldWay, StreetPathway newWay)
            {
                _oldNode = oldNode;
                _oldWay = oldWay;
                _newWay = newWay;
            }

            public StreetNode GetOldNode() => _oldNode;

            public StreetPathway GetOldWay() => _oldWay;

            public StreetPathway GetNewWay() => _newWay;
        }
    }
}