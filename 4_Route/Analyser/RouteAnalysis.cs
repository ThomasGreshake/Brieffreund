//Copyright Thomas Greshake 2026

namespace Brieffreund.Analyser
{
    internal class RouteAnalysis
    {
        //Data -------------------------------------------------------------

        private int _streetLength = 0, _veryLightCrossings = 0, _lightCrossings = 0, _mediumCrossings = 0, _heavyCrossings = 0,
            _veryHeavyCrossings = 0, _crossingScore = 0,
            _wrongSide = 0, _wrongWay = 0, _negDeviation = 0, _posDeviation = 0, _standardDeviation = 0, _storageCount = 0;

        internal int StreetLength => _streetLength;
        internal int VeryLightCrossings => _veryLightCrossings;
        internal int LightCrossings => _lightCrossings;
        internal int MediumCrossings => _mediumCrossings;
        internal int HeavyCrossings => _heavyCrossings;
        internal int VeryHeavyCrossings => _veryHeavyCrossings;
        internal int CrossingScore => _crossingScore;
        internal int LengthScore => _streetLength + _crossingScore;
        internal int WrongSide => _wrongSide;
        internal int WrongWay => _wrongWay;
        internal int StandardDeviation => _standardDeviation;
        internal int PosDeviation => _posDeviation;
        internal int NegDeviation => _negDeviation;
        internal int StorageCount => _storageCount;

        //Setup -------------------------------------------------------------

        internal static RouteAnalysis Create(IUserInput input, AnalyserMap map, IList<Address> route)
        {
            List<Tuple<Address, bool>> list = new List<Tuple<Address, bool>>(route.Count);
            foreach (Address address in route)
            {
                list.Add(Tuple.Create(address, address is Storage));
            }
            return Create(input, map, list);
        }

        internal static RouteAnalysis Create(IUserInput input, AnalyserMap map)
            => Create(input, map, map.ConvertAddresses(input.OrderedAddresses));

        private static RouteAnalysis Create(IUserInput input, AnalyserMap map, IList<Tuple<Address, bool>> addresses)
        {
            List<Tuple<AnalyserNode, bool>> nodes = ConvertList(map, addresses);
            List<AnalyserPathway> ways = new(128);

            for (int i = 0; i < nodes.Count - 1; i++)
            {
                var current = nodes[i];
                var next = nodes[i + 1];

                IPathfinder<AnalyserNode, AnalyserPathway> path =
                    IPathfinder.FindPath<AnalyserNode, AnalyserPathway>(current.Item1, next.Item1, w => w.RightOfWay ? 1f : 1.01f);
                ways.AddRange(path.GetPaths());
            }

            RouteAnalysis analysis = new();

            analysis.AnalyseLength(ways);
            analysis.AnalyseStreetCrossings(ways);
            analysis.AnalyseWrongSide(ways);
            analysis.AnalyseOnewayStreets(ways);
            analysis.AnalyseMailDistribution(addresses, input.DesiredStorageCount);

            return analysis;
        }

        private static List<Tuple<AnalyserNode, bool>> ConvertList(AnalyserMap map, IList<Tuple<Address, bool>> addresses)
        {
            List<Tuple<Address, bool>> addressList = new(addresses.Count);
            AnalyserNode? lastNode = null;

            foreach (Tuple<Address, bool> address in addresses)
            {
                AnalyserNode? node = map.GetNode(address.Item1);
                if (node == null || (lastNode != null && lastNode == node))
                {
                    continue;
                }

                lastNode = node;
                addressList.Add(address);
            }

            while (true)
            {
                bool removedDuplicate = false;

                for (int i = 0; i < addressList.Count; i++)
                {
                    var current = addressList[i];
                    if (current.Item2)
                    {
                        continue;
                    }

                    for (int j = i + 2; j < addressList.Count; j++)
                    {
                        var other = addressList[j];
                        if (other.Item2 || current.Item1 != other.Item1)
                        {
                            continue;
                        }

                        removedDuplicate = true;

                        float currScore = GetDistanceToNeighbours(map, addressList, i);
                        float otherScore = GetDistanceToNeighbours(map, addressList, j);

                        if (currScore < otherScore)
                        {
                            addressList.RemoveAt(j);
                        }
                        else
                        {
                            addressList.RemoveAt(j);
                        }

                        break;
                    }

                    if (removedDuplicate)
                    {
                        break;
                    }
                }

                if (!removedDuplicate)
                {
                    break;
                }
            }

            List<Tuple<AnalyserNode, bool>> nodeList = new(addressList.Count);

            foreach (var address in addressList)
            {
                AnalyserNode? node = map.GetNode(address.Item1);
                if (node == null)
                {
                    continue;
                }

                nodeList.Add(Tuple.Create(node, address.Item2));
            }

            return nodeList;
        }

        private static float GetDistanceToNeighbours(AnalyserMap map, List<Tuple<Address, bool>> list, int index)
        {
            AnalyserNode? currNode = map.GetNode(list[index].Item1);
            if (currNode == null)
            {
                return 0;
            }

            float distance = 0;

            if (index > 0)
            {
                AnalyserNode? prevNode = map.GetNode(list[index - 1].Item1);
                if (prevNode != null)
                {
                    IPathfinder<AnalyserNode, AnalyserPathway> path = IPathfinder.FindPath<AnalyserNode, AnalyserPathway>(currNode, prevNode);
                    distance += path.GetLength();
                }
            }

            if (index < list.Count - 1)
            {
                AnalyserNode? nextNode = map.GetNode(list[index + 1].Item1);
                if (nextNode != null)
                {
                    IPathfinder<AnalyserNode, AnalyserPathway> path = IPathfinder.FindPath<AnalyserNode, AnalyserPathway>(currNode, nextNode);
                    distance += path.GetLength();
                }
            }

            return distance;
        }

        private void AnalyseLength(List<AnalyserPathway> ways)
        {
            _streetLength = (int)ways.Where(w => !w.CrossesStreet).Sum(w => w.Length);
        }

        private void AnalyseStreetCrossings(List<AnalyserPathway> ways)
        {
            _veryLightCrossings = 0;
            _lightCrossings = 0;
            _mediumCrossings = 0;
            _heavyCrossings = 0;
            _veryHeavyCrossings = 0;

            float total = 0;

            foreach (AnalyserPathway way in ways)
            {
                if (!way.CrossesStreet || way.Path.StreetType == StreetType.Tiny)
                {
                    continue;
                }

                total += way.Length;

                switch (way.Traffic)
                {
                    case TrafficType.VeryHeavy:
                        _veryHeavyCrossings++;
                        break;

                    case TrafficType.Heavy:
                        _heavyCrossings++;
                        break;

                    case TrafficType.Medium:
                        _mediumCrossings++;
                        break;

                    case TrafficType.Light:
                        _lightCrossings++;
                        break;

                    case TrafficType.VeryLight:
                        _veryLightCrossings++;
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            _crossingScore = (int)total;
        }

        private void AnalyseWrongSide(List<AnalyserPathway> ways)
        {
            float wrongSide = 0;

            foreach (AnalyserPathway way in ways)
            {
                if (way.RightOfWay)
                {
                    continue;
                }

                wrongSide += way.Length;
            }

            _wrongSide = (int)wrongSide;
        }

        private void AnalyseOnewayStreets(List<AnalyserPathway> ways)
        {
            float wrongWay = 0;

            foreach (AnalyserPathway way in ways)
            {
                if (way.RightDirection)
                {
                    continue;
                }

                wrongWay += way.Length;
            }

            _wrongWay = (int)wrongWay;
        }

        private void AnalyseMailDistribution(IList<Tuple<Address, bool>> addresses, int desiredCount)
        {
            if (desiredCount == 0)
            {
                return;
            }

            List<int> mailCounts = new(desiredCount + 4);

            int totalMail = 0;
            int currentMail = 0;
            foreach (Tuple<Address, bool> address in addresses)
            {
                if (address.Item2)
                {
                    mailCounts.Add(currentMail);
                    currentMail = 0;
                    _storageCount++;
                }
                else if (address.Item1 is MailAddress mail)
                {
                    totalMail += mail.MailAmount;
                    currentMail += mail.MailAmount;
                }
            }

            mailCounts.Add(currentMail);
            float average = (float)totalMail / (desiredCount + 1);

            float min = desiredCount;
            float max = -desiredCount;
            float deviation = 0;

            foreach (int c in mailCounts)
            {
                float diff = c / average - 1;

                if (diff < min)
                {
                    min = diff;
                }
                if (diff > max)
                {
                    max = diff;
                }

                deviation += diff * diff;
            }

            deviation /= mailCounts.Count;
            deviation = (float)Math.Sqrt(deviation);

            if (min > 0)
            {
                min = 0;
            }
            if (max < 0)
            {
                max = 0;
            }

            _negDeviation = (int)Math.Round(-min * 100);
            _posDeviation = (int)Math.Round(max * 100);
            _standardDeviation = (int)Math.Round(deviation * 100);
        }

        private RouteAnalysis()
        {
        }
    }
}