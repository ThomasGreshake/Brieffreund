//Copyright Thomas Greshake 2026

using Brieffreund.Routegenerator;
using Brieffreund.Routegenerator.Functions;
using System.Collections.ObjectModel;

namespace Brieffreund
{
    internal class RouteReader
    {
        //Data --------------------------------------------------------------------

        internal readonly RouteMap Map;

        private readonly RoutePathway[] _ways;

        internal int Count => _ways.Length;
        internal RoutePathway CurrentWay => _ways[currentIndex];
        internal RoutePath CurrentPath => CurrentWay.RoutePath;
        internal RouteNode CurrentNode => CurrentWay.Origin;
        internal RouteNode NextNode => CurrentWay.Towards;
        internal bool Skipped => _skipDuplicate[currentIndex];

        private int currentIndex = 0;

        private bool[] _skipDuplicate;

        private bool _forcedSinglePathing = false;

        private Storage?[] _storages;
        internal Storage? LastStorage => currentIndex == 0 ? null : _storages[currentIndex - 1];
        internal Storage? UpcomingStorage => _storages[currentIndex];

        //Setup --------------------------------------------------------------------

        internal RouteReader(IUserInput input, RouteLeaf route)
        {
            RouteMap map = route.Map;
            _ways = route.ToPathwayArray();

            IStorageDistributor storageDistributor = new StorageDistributor(input, map);
            IDuplicateRemover duplicateRemover = new DuplicateRemover();

            Map = map;

            storageDistributor.DistributeStorages(_ways);
            _skipDuplicate = duplicateRemover.FindDuplicatesToSkip(_ways, storageDistributor.Data, storageDistributor.FinalMailAmount);

            _storages = new Storage?[_ways.Length];
            foreach (StorageData sD in storageDistributor.Data)
            {
                _storages[sD.NodeIndex] = sD.Storage.Item1;
            }
        }

        //Internals --------------------------------------------------------------------
        internal bool GoNext()
        {
            if (currentIndex < Count - 1)
            {
                currentIndex += 1;
                return true;
            }

            currentIndex = 0;
            _forcedSinglePathing = false;
            return false;
        }

        internal void ReturnToStart() => currentIndex = 0;

        internal List<Address> GetCurrentAddresses()
        {
            List<MailAddress> post = GetCurrentMailAddresses();
            List<Address> addresses = new List<Address>(post.Count);
            foreach (var p in post)
            {
                addresses.Add(p);
            }
            return addresses;
        }

        private List<MailAddress> GetCurrentMailAddresses()
        {
            EulerPath? path = CurrentWay.Eulerpath;
            if (path == null)
            {
                return new();
            }
            if (_forcedSinglePathing)
            {
                _forcedSinglePathing = false;
                return new();
            }

            if (path.Segment.Type == StreetType.Tiny)
            {
                EulerPath? next = GetNextValidPath();
                if (next != null && next.Segment == path.Segment)
                {
                    _forcedSinglePathing = true;
                }
            }

            if (_skipDuplicate[currentIndex] && !_forcedSinglePathing)
            {
                return new();
            }

            EulerNode fromNode = CurrentNode.Eulernode;

            List<MailAddress> addresses;

            bool singlePathing = path.LeftRight || _forcedSinglePathing;
            if (singlePathing)
            {
                addresses = path.Segment.SinglePathingOrder.ToList();
            }
            else
            {
                addresses = path.Segment.GetMailAddresses().Where(p => p.LeftOfPath == path.FromLeft).ToList();
            }

            if (path.To == fromNode)
            {
                addresses.Reverse();
            }

            if (addresses.Count == 0)
            {
                return new();
            }

            return addresses;
        }

        private EulerPath? GetNextValidPath()
        {
            for (int i = currentIndex + 1; i < Count; i++)
            {
                if (_ways[i].Eulerpath != null && !_skipDuplicate[i])
                {
                    return _ways[i].Eulerpath;
                }
            }
            return null;
        }
    }
}