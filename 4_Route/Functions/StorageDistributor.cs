//Copyright Thomas Greshake 2026

namespace Brieffreund.Routegenerator
{
    internal struct StorageData
    {
        internal int NodeIndex;
        internal int MailAmount;
        internal Tuple<Storage, float> Storage;
        internal int StorageIndex => Storage.Item1.Index;

        internal StorageData(int nodeIndex, int postAmount, Tuple<Storage, float> storage)
        {
            NodeIndex = nodeIndex;
            MailAmount = postAmount;
            Storage = storage;
        }
    }

    internal interface IStorageDistributor
    {
        public IList<StorageData> Data { get; }

        public int FinalMailAmount { get; }
        public float TotalStorageDistance { get; }

        public void DistributeStorages(RoutePathway[] ways);
    }
}

namespace Brieffreund.Routegenerator.Functions
{
    internal class StorageDistributor : IStorageDistributor
    {
        private readonly IUserInput _input;
        private readonly RouteMap _map;

        private readonly List<StorageData> _data = new();
        public IList<StorageData> Data => _data;

        private int _mailAmount = 0;
        public int FinalMailAmount => _mailAmount;

        private float _totalStorageDistance = 0;
        public float TotalStorageDistance => _totalStorageDistance;

        internal StorageDistributor(IUserInput input, RouteMap map)
        {
            _input = input;
            _map = map;
        }

        public void DistributeStorages(RoutePathway[] ways)
        {
            _data.Clear();
            _mailAmount = 0;
            _totalStorageDistance = 0;

            if (_input.DesiredStorageCount == 0)
            {
                return;
            }

            int totalMail = _map.EulerMap.Streetmap.TotalMailAmount;
            float averageMail = (float)totalMail / (_input.DesiredStorageCount + 1);

            PrepareStorageData(ways);
            ReduceToMinima(averageMail);
            RemoveTinySections(averageMail);
            RemoveOverCounts(averageMail);
            MinimiseStorageUsage(averageMail * Constants.VERY_GOOD_MULTIPLIER);
            AnalyseStorageData(averageMail);
        }

        private void PrepareStorageData(RoutePathway[] ways)
        {
            for (int i = 0; i < ways.Length; i++)
            {
                RoutePathway current = ways[i];
                EulerPath? eulerPath = current.Eulerpath;

                if (eulerPath != null)
                {
                    _mailAmount += eulerPath.MailAmount;
                }

                if (i == ways.Length - 1)
                {
                    return;
                }

                RouteNode currentNode = current.Towards;
                Tuple<Storage, float>? storage = currentNode.Storage;
                if (storage == null)
                {
                    continue;
                }

                float nodeDistance = currentNode.Eulernode.Intersection.Streetnode.GetDistance(storage.Item1);

                EulerPathway? ePathway = current.EulerPathway;
                if (ePathway == null)
                {
                    Tuple<Storage, float>? other = current.Origin.Storage;
                    if (other != null && other.Item2 < storage.Item2)
                    {
                        continue;
                    }
                }
                else
                {
                    StreetPathway sPathway = ePathway.GetIncomingWay();
                    float incDist = sPathway.Origin.GetDistance(storage.Item1);

                    if (incDist < nodeDistance)
                    {
                        continue;
                    }
                }

                RoutePathway next = ways[i + 1];
                EulerPathway? nPathway = next.EulerPathway;
                if (nPathway == null)
                {
                    Tuple<Storage, float>? other = next.Towards.Storage;
                    if (other != null && other.Item2 < storage.Item2)
                    {
                        continue;
                    }
                }
                else
                {
                    StreetPathway sPathway = nPathway.GetOutgoingWay();
                    float outDist = sPathway.Towards.GetDistance(storage.Item1);

                    if (outDist < nodeDistance)
                    {
                        continue;
                    }
                }

                Storage st = storage.Item1;
                int sideId = st.GetSidewalkId();

                if ((ePathway != null && sideId == StreetSegment.GetSidewalkId(ePathway.Eulerpath.Segment, ePathway.LeftOfWay(true) == true)) ||
                    (nPathway != null && sideId == StreetSegment.GetSidewalkId(nPathway.Eulerpath.Segment, nPathway.LeftOfWay(false) == true)))
                {
                    storage = Tuple.Create(st, 0f);
                }
                else if (ePathway == null && nPathway == null)
                {
                    RouteNode? lastNode = GetNode(ways, i, false);
                    if (lastNode == null)
                    {
                        continue;
                    }

                    RouteNode? nextNode = GetNode(ways, i, true);
                    if (nextNode == null)
                    {
                        continue;
                    }

                    float diff = Math.Max(lastNode.GetPassingDistance(currentNode) + currentNode.GetPassingDistance(nextNode) -
                        lastNode.GetPassingDistance(nextNode), 0);

                    storage = Tuple.Create(st, diff + storage.Item2);
                }

                StorageData inter = new(i, _mailAmount, storage);
                _data.Add(inter);
                _mailAmount = 0;
            }
        }

        private RouteNode? GetNode(RoutePathway[] ways, int index, bool forwards)
        {
            int i = index;
            while (ways[i].RoutePath.IsPathOnNode)
            {
                i += forwards ? 1 : -1;
                if (i == -1 || i == ways.Length)
                {
                    return null;
                }
            }
            return forwards ? ways[i].Origin : ways[i].Towards;
        }

        private void ReduceToMinima(float averageMail)
        {
            float inacceptable = averageMail * Constants.INACCEPTABLE_MULTIPLIER;

            while (true)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;

                for (int i = 0; i < _data.Count - 1; i++)
                {
                    StorageData current = _data[i];
                    StorageData next = _data[i + 1];
                    int overNextMailAmount = i == _data.Count - 2 ? _mailAmount : _data[i + 2].MailAmount;

                    if (next.Storage != current.Storage)
                    {
                        continue;
                    }

                    if (current.Storage.Item2 > next.Storage.Item2)
                    {
                        if (current.Storage.Item2 > bestScore && (current.MailAmount + next.MailAmount) < inacceptable)
                        {
                            bestScore = current.Storage.Item2;
                            bestIndex = i;
                        }
                    }
                    else
                    {
                        if (next.Storage.Item2 > bestScore && (next.MailAmount + overNextMailAmount) < inacceptable)
                        {
                            bestScore = next.Storage.Item2;
                            bestIndex = i + 1;
                        }
                    }
                }

                if (bestIndex == -1)
                {
                    break;
                }

                CombineWithNext(bestIndex);
            }
        }

        private void RemoveTinySections(float averageMail)
        {
            float tinyMail = averageMail * Constants.TINY_MAILAMOUNT_MULTIPLIER;

            while (true)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;

                for (int i = 0; i < _data.Count; i++)
                {
                    StorageData current = _data[i];
                    int nextMailAmount = i == _data.Count - 1 ? _mailAmount : _data[i + 1].MailAmount;

                    if (current.Storage.Item2 <= bestScore || (current.MailAmount > tinyMail && nextMailAmount > tinyMail))
                    {
                        continue;
                    }

                    bestScore = current.Storage.Item2;
                    bestIndex = i;
                }

                if (bestIndex == -1)
                {
                    break;
                }

                CombineWithNext(bestIndex);
            }
        }

        private void RemoveOverCounts(float averageMail)
        {
            float acceptable = averageMail * Constants.ACCEPTABLE_MAILAMOUNT_MULTIPLIER;

            int[] overCounts = GetOverusageCounts();

            while (true)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;

                for (int i = 0; i < _data.Count; i++)
                {
                    StorageData current = _data[i];
                    if (overCounts[current.StorageIndex] == 0)
                    {
                        continue;
                    }

                    int nextMailAmount = i == _data.Count - 1 ? _mailAmount : _data[i + 1].MailAmount;
                    float score = current.Storage.Item2 - Math.Max(current.MailAmount + nextMailAmount - acceptable, 0) * 100;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                if (bestIndex == -1)
                {
                    return;
                }

                overCounts[_data[bestIndex].StorageIndex] -= 1;
                CombineWithNext(bestIndex);
            }
        }

        private int[] GetOverusageCounts()
        {
            IList<Storage> storages = _map.EulerMap.Streetmap.Storages;
            int count = storages.Count;
            int[] overUsage = new int[count];
            for (int i = 0; i < count; i++)
            {
                overUsage[i] = -storages[i].MaxUsageCount;
            }
            for (int i = 0; i < _data.Count; i++)
            {
                overUsage[_data[i].StorageIndex] += 1;
            }
            for (int i = 0; i < count; i++)
            {
                if (overUsage[i] < 0)
                {
                    overUsage[i] = 0;
                }
            }
            return overUsage;
        }

        private void CombineWithNext(int index)
        {
            StorageData current = _data[index];
            _data.RemoveAt(index);
            if (index == _data.Count)
            {
                _mailAmount += current.MailAmount;
                return;
            }

            StorageData next = _data[index];
            _data.RemoveAt(index);

            StorageData combined = new(next.NodeIndex, current.MailAmount + next.MailAmount, next.Storage);
            _data.Insert(index, combined);
        }

        private void MinimiseStorageUsage(float cutOff)
        {
            while (true)
            {
                int bestIndex = -1;
                float bestScore = float.MinValue;
                for (int i = 0; i < _data.Count; i++)
                {
                    StorageData current = _data[i];
                    int nextMailAmount = i == _data.Count - 1 ? _mailAmount : _data[i + 1].MailAmount;
                    if (current.MailAmount + nextMailAmount > cutOff)
                    {
                        continue;
                    }

                    if (current.Storage.Item2 > bestScore)
                    {
                        bestIndex = i;
                        bestScore = current.Storage.Item2;
                    }
                }

                if (bestIndex == -1)
                {
                    return;
                }

                CombineWithNext(bestIndex);
            }
        }

        private void AnalyseStorageData(float averageMail)
        {
            float inacceptable = averageMail * Constants.INACCEPTABLE_MULTIPLIER;

            _totalStorageDistance = 0;
            for (int i = 0; i < _data.Count; i++)
            {
                StorageData current = _data[i];
                _totalStorageDistance += current.Storage.Item2;
                if (current.MailAmount > inacceptable)
                {
                    float penalty = RouteLeaf.GetStorageMailPenalty(current.MailAmount, inacceptable);
                    _totalStorageDistance += penalty;
                }
            }

            if (_mailAmount > inacceptable)
            {
                float penalty = RouteLeaf.GetStorageMailPenalty(_mailAmount, inacceptable);
                _totalStorageDistance += penalty;
            }
        }
    }
}