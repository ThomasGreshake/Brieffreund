//Copyright Thomas Greshake 2026

using Brieffreund.Streetmap;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Brieffreund
{
    internal interface IOnNodeCreated : IEvent<IOnNodeCreated>

    { public StreetNode GetNode(); }

    internal interface IOnNodeDeleted : IEvent<IOnNodeDeleted>

    { public StreetNode GetNode(); }

    internal interface IOnNodeIsActiveChanged : IEvent<IOnNodeIsActiveChanged>

    { public StreetNode GetNode(); }

    [Flags]
    internal enum NodeFlags : byte
    {
        None = 0,
        TrafficSignals = 1 << 0,
        Barrier = 1 << 1
    }

    internal class StreetNode : IPathnode<StreetNode, StreetPathway>,
        IOnNodeCreated, IOnNodeDeleted, IOnNodeIsActiveChanged
    {
        //Data -------------------------------------------------------------

        internal readonly IPathMap Map;

        private bool _isActive = false;
        internal bool IsActive => _isActive;

        private readonly Vector2 _position;
        public Vector2 Position => _position;

        internal readonly ReadOnlyCollection<StreetPathway> Pathways;
        private readonly List<StreetPathway> _pathWays = new();
        internal int Count => _pathWays.Count;
        internal IEnumerable<StreetPath> Paths => _pathWays.Select(p => p.Path);
        internal float Radius => _pathWays.Max(p => p.Path.Width);
        internal float PassingCircumference => _pathWays.Sum(w => w.Path.GetPassingWidth());

        internal readonly NodeFlags Flags;
        internal bool HasTrafficSignals => Flags.HasFlag(NodeFlags.TrafficSignals);
        internal bool HasBarrier => Flags.HasFlag(NodeFlags.Barrier);

        private float[] _storageDistances = Array.Empty<float>();

        //Setup ------------------------------------------------------------

        internal static StreetNode Create(IPathMap map, Vector2 position, NodeFlags flags)
        {
            StreetNode node = new(map, position, flags);
            IOnNodeCreated.Call(node);
            return node;
        }

        private StreetNode(IPathMap map, Vector2 position, NodeFlags flags)
        {
            Map = map;
            _position = position;
            Pathways = _pathWays.AsReadOnly();
            Flags = flags;
        }

        static StreetNode()
        {
            IOnPathCreated.Register(OnStreetPathCreated);
            IOnPathDeleted.Register(OnStreetPathDeleted);
            IStorageDistanceCalculator.Register(OnStorageDistancesCalculated);
            IOnPathIsActiveChanged.Register(OnPathIsActiveChanged);
            IOnPathCut.Register(OnPathCut);
        }

        //Publics ------------------------------------------------------------

        public IList<StreetPathway> GetPaths() => Pathways;

        public StreetNode GetNode() => this;

        //Internals ------------------------------------------------------------

        internal static int Clean(IPathMap map)
        {
            List<StreetNode> empty = map.GetAllNodes().Where(n => n.Count == 0).ToList();
            foreach (StreetNode node in empty)
            {
                node.Delete();
            }
            return empty.Count;
        }

        internal float GetPassingDistance(StreetPathway from, bool? fromLeftOfWay, StreetPathway to, bool? toLeftOfWay)
        {
            if (from.Towards != this || to.Origin != this)
            {
                throw new Exception();
            }

            int fromIndex = _pathWays.IndexOf(from.GetOpposite());
            int toIndex = _pathWays.IndexOf(to);

            if (fromIndex < 0 || toIndex < 0)
            {
                throw new Exception();
            }

            float counterClockwiseDistance = 0f;

            while (true)
            {
                fromIndex = (fromIndex + 1) % _pathWays.Count;
                if (fromIndex == toIndex)
                {
                    break;
                }

                StreetPathway current = _pathWays[fromIndex];
                counterClockwiseDistance += current.GetPassingWidth();
            }

            float increment = from.GetPassingWidth();
            if (fromLeftOfWay == true)
            {
                counterClockwiseDistance += increment;
            }

            increment = to.GetPassingWidth();
            if (toLeftOfWay == true)
            {
                counterClockwiseDistance += increment;
            }

            float passingCircumference = PassingCircumference;
            if (passingCircumference < counterClockwiseDistance)
            {
                counterClockwiseDistance -= passingCircumference;
            }

            return Math.Min(counterClockwiseDistance, passingCircumference - counterClockwiseDistance);
        }

        internal float GetDistance(Storage storage) => _storageDistances[storage.Index];

        //Listeners ------------------------------------------------------------

        private static void OnStreetPathCreated(IOnPathCreated e)
        {
            StreetPath path = e.GetPath();

            path.From.Add(path);
            path.To.Add(path);
        }

        private void Add(StreetPath path)
        {
            StreetPathway outgoing = path.To == this ? path.Backward : path.Forward;

            if (_pathWays.Contains(outgoing))
            {
                throw new Exception();
            }

            float angle = GetAngle(outgoing);

            for (int i = 0; i < _pathWays.Count; i++)
            {
                StreetPathway current = _pathWays[i];
                float currAngle = GetAngle(current);
                if (angle < currAngle)
                {
                    _pathWays.Insert(i, outgoing);
                    return;
                }
            }

            _pathWays.Add(outgoing);
        }

        private float GetAngle(StreetPathway way) => MyMath.FullAngle(new Vector2(1, 0), way.Direction);

        private static void OnStreetPathDeleted(IOnPathDeleted e)
        {
            StreetPath path = e.GetPath();

            if (!path.From._pathWays.Remove(path.Forward) || !path.To._pathWays.Remove(path.Backward))
            {
                throw new Exception();
            }

            if (path.From.Count == 0)
            {
                path.From.Delete();
            }

            if (path.To.Count == 0)
            {
                path.To.Delete();
            }
        }

        private void Delete()
        {
            if (_isActive)
            {
                throw new Exception();
            }

            IOnNodeDeleted.Call(this);
        }

        private static void OnPathIsActiveChanged(IOnPathIsActiveChanged e)
        {
            StreetPath path = e.GetPath();
            path.From.SetIsActive();
            path.To.SetIsActive();
        }

        private void SetIsActive()
        {
            bool isActive = Paths.Any(p => p.IsActive);

            if (_isActive == isActive)
            {
                return;
            }

            _isActive = isActive;
            IOnNodeIsActiveChanged.Call(this);
        }

        private static void OnStorageDistancesCalculated(IStorageDistanceCalculator e)
        {
            foreach (var calc in e.NodeDistances)
            {
                StreetNode node = calc.Key;
                float[] dists = calc.Value;
                node._storageDistances = dists;
            }
        }

        private static void OnPathCut(IOnPathCut e)
        {
            StreetPathway oldWay = e.GetOldWay();
            StreetPathway newWay = e.GetNewWay();
            StreetPath path = oldWay.Path;
            StreetPathway otherWay = path.GetOpposite(newWay);

            if (!e.GetOldNode()._pathWays.Remove(otherWay) || !otherWay.Towards._pathWays.Remove(oldWay))
            {
                throw new Exception();
            }

            OnStreetPathCreated(path);
        }
    }
}