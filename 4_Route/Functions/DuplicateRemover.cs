//Copyright Thomas Greshake 2026

namespace Brieffreund.Routegenerator
{
    internal interface IDuplicateRemover
    {
        public int FinalMailAmount { get; }

        public bool[] FindDuplicatesToSkip(RoutePathway[] ways, IList<StorageData> data, int finalPostAmount);
    }
}

namespace Brieffreund.Routegenerator.Functions
{
    internal class DuplicateRemover : IDuplicateRemover
    {
        private bool[] _skipDuplicates;

        private int _finalMailAmount = 0;
        public int FinalMailAmount => _finalMailAmount;

        private IList<StorageData> _data;

        internal DuplicateRemover()
        {
            _skipDuplicates = Array.Empty<bool>();
            _data = new List<StorageData>();
        }

        public bool[] FindDuplicatesToSkip(RoutePathway[] ways, IList<StorageData> data, int finalMailAmount)
        {
            _skipDuplicates = new bool[ways.Length];
            _finalMailAmount = finalMailAmount;
            _data = data;

            Dictionary<int, int> sideWalkIndexToWayIndex = new(ways.Length);
            List<Tuple<int, int>> duplicates = new();
            bool[] isDuplicate = new bool[ways.Length];

            for (int i = 0; i < ways.Length; i++)
            {
                EulerPath? path = ways[i].Eulerpath;
                if (path == null)
                {
                    continue;
                }

                int sidewalkId = StreetSegment.GetSidewalkId(path.Segment, path.ToLeft == true);

                if (sideWalkIndexToWayIndex.ContainsKey(sidewalkId))
                {
                    int j = sideWalkIndexToWayIndex[sidewalkId];
                    duplicates.Add(Tuple.Create(i, j));
                    isDuplicate[i] = true;
                    isDuplicate[j] = true;
                    continue;
                }

                sideWalkIndexToWayIndex.Add(sidewalkId, i);
            }

            foreach (Tuple<int, int> duplicate in duplicates)
            {
                float firstDist = GetPassingDistance(ways, duplicate.Item1);
                float secondDist = GetPassingDistance(ways, duplicate.Item2);

                if (firstDist * 1.2f < secondDist)
                {
                    SkipDuplicate(ways, duplicate.Item2, duplicate.Item1);
                    continue;
                }
                if (secondDist * 1.2f < firstDist)
                {
                    SkipDuplicate(ways, duplicate.Item1, duplicate.Item2);
                    continue;
                }

                int firstMail = GetMailAmount(duplicate.Item1);
                int secondMail = GetMailAmount(duplicate.Item2);

                if (firstMail > secondMail * 2)
                {
                    SkipDuplicate(ways, duplicate.Item1, duplicate.Item2);
                    continue;
                }
                if (secondMail > firstMail * 2)
                {
                    SkipDuplicate(ways, duplicate.Item2, duplicate.Item1);
                    continue;
                }

                float firstScore = GetDuplicateRemainScore(ways, duplicate.Item1, isDuplicate);
                float secondScore = GetDuplicateRemainScore(ways, duplicate.Item2, isDuplicate);

                if (firstScore >= secondScore)
                {
                    SkipDuplicate(ways, duplicate.Item2, duplicate.Item1);
                }
                else
                {
                    SkipDuplicate(ways, duplicate.Item1, duplicate.Item2);
                }
            }

            return _skipDuplicates;
        }

        private float GetDuplicateRemainScore(RoutePathway[] ways, int index, bool[] isDuplicate)
        {
            RoutePathway way = ways[index];
            List<MailAddress> addresses = way.GetMailAddresses();
            if (addresses.Count == 0)
            {
                return 0;
            }

            float score = 0;
            MailAddress address = addresses[0];
            score += GetPathwayScore(ways, address, isDuplicate, index, false);
            address = addresses[addresses.Count - 1];
            score += GetPathwayScore(ways, address, isDuplicate, index, true);

            return score;
        }

        private float GetPathwayScore(RoutePathway[] ways, MailAddress compare, bool[] isDuplicate, int index, bool forward)
        {
            int inc = forward ? 1 : -1;
            float score = 0f;
            float distTravelled = 0;

            for (int i = index + inc; i >= 0 && i < ways.Length; i += inc)
            {
                RoutePathway current = ways[i];
                StreetSegment? currentSeg = current.Eulerpath?.Segment;
                if (currentSeg == null)
                {
                    continue;
                }

                List<MailAddress> currentA = current.GetMailAddresses();
                bool reversed = current.Towards.Eulernode.Intersection != currentSeg.To;

                foreach (MailAddress m in currentA)
                {
                    if (m.Street != compare.Street)
                    {
                        continue;
                    }

                    float dist = m.GetDistanceOnSegment();
                    if (reversed)
                    {
                        dist = currentSeg.Length - dist;
                    }

                    dist += distTravelled;
                    float numberDiff = (float)Math.Abs(compare.NumberId - m.NumberId) / Constants.ADDRESS_ADDON_RANGE;
                    score += 10f * (isDuplicate[i] ? 0.5f : 1f) * (m.IsEven == compare.IsEven ? 1f : 0.25f) / (5f + (float)Math.Pow(dist * numberDiff, 0.25f));
                }
            }

            return score;
        }

        private int GetMailAmount(int wayIndex)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                StorageData current = _data[i];
                if (current.NodeIndex >= wayIndex)
                {
                    return current.MailAmount;
                }
            }
            return _finalMailAmount;
        }

        private float GetPassingDistance(RoutePathway[] ways, int index)
        {
            RoutePathway way = ways[index];

            RoutePathway? from = null;
            int i = index;
            while (i > 0)
            {
                i--;

                RoutePathway w = ways[i];
                if (w.RoutePath.IsPathOnNode)
                {
                    continue;
                }

                from = w;
                break;
            }

            RoutePathway? to = null;
            i = index;
            while (i < ways.Length - 1)
            {
                i++;

                RoutePathway w = ways[i];
                if (w.RoutePath.IsPathOnNode)
                {
                    continue;
                }

                to = w;
                break;
            }

            float distance = 0;

            if (from != null)
            {
                distance += from.Towards.GetPassingDistance(from, way);
            }
            if (to != null)
            {
                distance += way.Towards.GetPassingDistance(way, to);
            }

            return distance;
        }

        private void SkipDuplicate(RoutePathway[] ways, int skip, int remain)
        {
            _skipDuplicates[skip] = true;

            EulerPath? path = ways[skip].Eulerpath;
            if (path == null)
            {
                return;
            }

            int postAmount = path.MailAmount / 2;
            if (postAmount == 0)
            {
                return;
            }

            int dataSkipId = -1;
            int dataRemainId = -1;

            for (int i = 0; i < _data.Count; i++)
            {
                int nodeIndex = _data[i].NodeIndex;
                if (nodeIndex >= skip && dataSkipId == -1)
                {
                    dataSkipId = i;
                }
                if (nodeIndex >= remain && dataRemainId == -1)
                {
                    dataRemainId = i;
                }
            }

            if (dataSkipId == dataRemainId)
            {
                return;
            }

            if (dataSkipId == -1)
            {
                _finalMailAmount -= postAmount;
            }
            else
            {
                StorageData skipData = _data[dataSkipId];
                _data.RemoveAt(dataSkipId);
                skipData = new StorageData(skipData.NodeIndex, skipData.MailAmount - postAmount, skipData.Storage);
                _data.Insert(dataSkipId, skipData);
            }

            if (dataRemainId == -1)
            {
                _finalMailAmount += postAmount;
            }
            else
            {
                StorageData remainData = _data[dataRemainId];
                _data.RemoveAt(dataRemainId);
                remainData = new StorageData(remainData.NodeIndex, remainData.MailAmount + postAmount, remainData.Storage);
                _data.Insert(dataRemainId, remainData);
            }
        }
    }
}