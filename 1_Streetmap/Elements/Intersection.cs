//Copyright Thomas Greshake 2026

using Brieffreund.Streetmap;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Brieffreund
{
    internal interface IOnIntersectionCreated : IEvent<IOnIntersectionCreated>

    { public Intersection GetIntersection(); }

    internal interface IOnIntersectionRemoved : IEvent<IOnIntersectionRemoved>

    { public Intersection GetIntersection(); }

    internal interface IOnIntersectionIsActiveChanged : IEvent<IOnIntersectionIsActiveChanged>

    { public Intersection GetIntersection(); }

    internal class Intersection : IPathnode<Intersection, SegmentPathway>,
        IOnIntersectionCreated, IOnIntersectionRemoved, IOnIntersectionIsActiveChanged
    {
        //Data -------------------------------------------------------------

        private bool _isActive = false;
        internal bool IsActive => _isActive;

        internal readonly IStreetMap Map;

        internal readonly StreetNode Streetnode;
        internal float Radius => Streetnode.Radius;
        public Vector2 Position => Streetnode.Position;
        internal float PassingCircumference => Streetnode.PassingCircumference;

        internal bool IsStart => this == Map.StartAndEndIntersections[0];
        internal bool IsEnd => this == Map.StartAndEndIntersections[1];

        private readonly List<SegmentPathway> _pathways;
        internal int Count => _pathways.Count;
        internal IEnumerable<StreetSegment> Segments => _pathways.Select(s => s.Segment);

        internal readonly ReadOnlyCollection<SegmentPathway> Pathways;

        public IList<SegmentPathway> GetPaths() => Pathways;

        private readonly List<Storage> _storages = new();
        internal readonly ReadOnlyCollection<Storage> Storages;

        public Intersection GetIntersection() => this;

        //Setup ------------------------------------------------------------

        internal static Intersection GetOrCreate(IStreetMap map, StreetNode node)
        {
            if (!map.Intersections.TryGetValue(node, out Intersection? inter) && !map.InactiveIntersections.TryGetValue(node, out inter))
            {
                inter = new(map, node);
                IOnIntersectionCreated.Call(inter);
            }

            return inter;
        }

        private Intersection(IStreetMap map, StreetNode node)
        {
            Map = map;

            Streetnode = node;
            _pathways = new(node.Count);

            Pathways = _pathways.AsReadOnly();
            Storages = _storages.AsReadOnly();
        }

        static Intersection()
        {
            IOnSegmentCreated.Register(OnSegmentCreated);
            IOnSegmentDeleted.Register(OnSegmentDeleted);
            IOnSegmentRemoved.Register(OnSegmentRemoved);
            IOnAddressCreated.Register(OnAddressCreated);
            IAddressRelocator.Register(OnAddressRelocation);
            IOnSegmentIsActiveChanged.Register(OnSegmentIsActiveChanged);
            IStorageTrimmer.Register(OnStoragesTrimmed);
        }

        internal static void Remove(IEnumerable<Intersection> list)
        {
            foreach (Intersection inter in list)
            {
                inter.Remove();
            }
        }

        private void Remove()
        {
            IOnIntersectionRemoved.Call(this);
        }

        //Internals --------------------------------------------------------

        internal float GetPassingDistance(SegmentPathway from, bool? fromLeftOfWay, SegmentPathway to, bool? toLeftOfWay)
        {
            StreetPathway fromPath = from.GetIncomingway();
            StreetPathway toPath = to.GetOutgoingway();
            return Streetnode.GetPassingDistance(fromPath, fromLeftOfWay, toPath, toLeftOfWay);
        }

        //Listeners --------------------------------------------------------
        private static void OnSegmentCreated(IOnSegmentCreated e)
        {
            StreetSegment s = e.GetSegment();
            s.From.Add(s.Forward);
            s.To.Add(s.Backward);
        }

        private void Add(SegmentPathway way)
        {
            int score = GetOrderScore(way);
            for (int i = 0; i < _pathways.Count; i++)
            {
                SegmentPathway current = _pathways[i];
                int currentScore = GetOrderScore(current);
                if (score < currentScore)
                {
                    _pathways.Insert(i, way);
                    return;
                }
            }

            _pathways.Add(way);
        }

        private int GetOrderScore(SegmentPathway way)
        {
            StreetPathway pathway = way.GetOutgoingway();
            int score = Streetnode.Pathways.IndexOf(pathway);
            if (score < 0)
            {
                throw new Exception();
            }
            return score;
        }

        private static void OnSegmentDeleted(IOnSegmentDeleted e)
        {
            StreetSegment s = e.GetSegment();
            OnSegmentRemoved(s);

            if (s.From.Count == 0)
            {
                s.From.Remove();
            }
            if (s.To.Count == 0 && s.From != s.To)
            {
                s.To.Remove();
            }
        }

        private static void OnSegmentRemoved(IOnSegmentRemoved e)
        {
            StreetSegment s = e.GetSegment();
            if (!s.From._pathways.Remove(s.Forward) || !s.To._pathways.Remove(s.Backward))
            {
                throw new Exception();
            }
        }

        private static void OnAddressRelocation(IAddressRelocator e)
        {
            IStreetMap map = e.Map;
            foreach (Intersection inter in map.GetAllIntersections())
            {
                inter._storages.Clear();
            }

            foreach (Tuple<Intersection, Storage> pair in e.Storages)
            {
                pair.Item1._storages.Add(pair.Item2);
            }
        }

        private static void OnAddressCreated(IOnAddressCreated e)
        {
            Address address = e.GetAddress();
            if (address is Storage storage)
            {
                storage.GetClosestIntersection()._storages.Add(storage);
            }
        }

        private static void OnSegmentIsActiveChanged(IOnSegmentIsActiveChanged e)
        {
            StreetSegment s = e.GetSegment();
            s.From.SetIsActive();
            s.To.SetIsActive();
        }

        private static void OnStoragesTrimmed(IStorageTrimmer trimmer)
        {
            IStreetMap map = trimmer.Map;
            foreach (Intersection inter in map.GetAllIntersections())
            {
                inter._storages.Clear();
            }

            foreach (Storage storage in map.Storages.Where(s => !trimmer.ToRemove.Contains(s)))
            {
                storage.GetClosestIntersection()._storages.Add(storage);
            }
        }

        private void SetIsActive()
        {
            bool isActive = GetIsActive();
            if (_isActive == isActive)
            {
                return;
            }

            _isActive = isActive;

            IOnIntersectionIsActiveChanged.Call(this);
        }

        private bool GetIsActive()
        {
            foreach (StreetPath p in Streetnode.Paths)
            {
                StreetSegment? seg = p.Segment;
                if (seg == null)
                {
                    if (p.IsActive)
                    {
                        return true;
                    }
                }
                else
                {
                    if (seg.IsActive)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}