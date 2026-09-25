//Copyright Thomas Greshake 2026

using Priority_Queue;

namespace Brieffreund.Streetmap
{
    internal interface ITrafficTypeIdentifier : IEvent<ITrafficTypeIdentifier>
    {
        public IStreetMap Map { get; }

        public IDictionary<StreetPath, TrafficType> Traffic { get; }

        public void IdentifyTrafficType();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class TrafficTypeIdentifier : ITrafficTypeIdentifier
    {
        private readonly IUserInput _input;
        private readonly IStreetMap _map;
        public IStreetMap Map => _map;

        private readonly Dictionary<StreetPath, TrafficType> _traffic;
        public IDictionary<StreetPath, TrafficType> Traffic => _traffic;

        internal TrafficTypeIdentifier(IUserInput input, IStreetMap map)
        {
            _input = input;
            _map = map;
            _traffic = new Dictionary<StreetPath, TrafficType>(map.Paths.Count + map.InactivePaths.Count);
        }

        public void IdentifyTrafficType()
        {
            GetTypeByStreetType();
            UpgradeTraffic();
            OverwriteWithInput();
            Print();
            ITrafficTypeIdentifier.Call(this);
        }

        private void GetTypeByStreetType()
        {
            foreach (StreetPath path in _map.GetAllPaths())
            {
                int typeInt = (int)path.Type;
                TrafficType traffic = typeInt > 1 ? (TrafficType)typeInt : TrafficType.VeryLight;
                _traffic.Add(path, traffic);
            }
        }

        private void UpgradeTraffic()
        {
            List<StreetNode> signalNodes = _map.GetAllNodes().Where(n => n.HasTrafficSignals).ToList();

            foreach (StreetNode signal in signalNodes)
            {
                List<Street> done = new();
                foreach (StreetPath path in signal.Paths.Where(p => p.Type == StreetType.Minor))
                {
                    Street? street = path.Street;
                    if (street == null || done.Contains(street))
                    {
                        continue;
                    }

                    done.Add(street);
                    UpgradeFromSignalNode(signal, street);
                }
            }
        }

        private void UpgradeFromSignalNode(StreetNode node, Street street)
        {
            SimplePriorityQueue<StreetNode> front = new();
            Dictionary<StreetNode, float> scores = new();

            front.Enqueue(node, 0);
            scores.Add(node, 0);

            while (front.Count > 0)
            {
                StreetNode current = front.Dequeue();
                float currentScore = scores[current];

                foreach (StreetPath path in current.Paths)
                {
                    if (path.Type != StreetType.Minor || path.Street != street || path.InternalPathFlags.HasFlag(InternalPathFlags.TrueDeadEnd))
                    {
                        continue;
                    }

                    List<StreetPath> paths = ExpandPath(path, current, out StreetNode other);
                    if (other.HasBarrier)
                    {
                        continue;
                    }

                    float score = currentScore + paths.Sum(p => p.Length);
                    if (score > Constants.TRAFFIC_SIGNAL_DISTANCE)
                    {
                        continue;
                    }

                    foreach (StreetPath upgradePath in paths.Where(p => p.Type == StreetType.Minor))
                    {
                        _traffic[upgradePath] = TrafficType.Light;
                    }

                    if (other.HasTrafficSignals)
                    {
                        continue;
                    }

                    if (scores.TryGetValue(other, out float otherScore))
                    {
                        if (otherScore < score)
                        {
                            continue;
                        }

                        front.UpdatePriority(other, score);
                    }
                    else
                    {
                        front.Enqueue(other, score);
                    }

                    scores[other] = score;
                }
            }
        }

        private List<StreetPath> ExpandPath(StreetPath start, StreetNode current, out StreetNode end)
        {
            List<StreetPath> options = new(1) { start };
            List<StreetPath> paths = new(8);
            end = current;

            while (options.Count == 1)
            {
                StreetPath path = options[0];
                paths.Add(path);
                end = path.GetOther(end);
                if (end.HasBarrier || end.HasTrafficSignals)
                {
                    break;
                }

                options = end.Paths.Where(p => p.Type != StreetType.Tiny && p != path && !p.InternalPathFlags.HasFlag(InternalPathFlags.TrueDeadEnd)).ToList();
            }

            return paths;
        }

        private void OverwriteWithInput()
        {
            foreach (StreetPath path in _map.GetAllPaths())
            {
                if (path.Type == StreetType.Tiny)
                {
                    continue;
                }

                Street? street = path.Street;
                if (street == null)
                {
                    continue;
                }

                if (_input.TrafficTypes.TryGetValue(street, out TrafficType inputType))
                {
                    _traffic[path] = inputType;
                }
            }
        }

        private void Print()
        {
            Dictionary<Street, TrafficType> streetTraffic = GetStreetTraffic();
            List<Street> toPrint = streetTraffic.Keys.Where(s => _input.MailAddresses.ContainsKey(s) && !_input.TrafficTypes.ContainsKey(s)).ToList();
            if (toPrint.Count > 0)
            {
                Console.WriteLine("Die Verkehrstärke der folgenden Straßen wurden wie folgt bestimmt:");

                for (int i = 0; i < toPrint.Count; i++)
                {
                    Street street = toPrint[i];

                    TrafficType type = streetTraffic[street];

                    if (type == TrafficType.VeryLight)
                    {
                        continue;
                    }

                    Console.Write(street.Name + ": ");

                    switch (type)
                    {
                        case TrafficType.VeryHeavy:
                            Console.Write("Sehr schwer");
                            break;

                        case TrafficType.Heavy:
                            Console.Write("Schwer");
                            break;

                        case TrafficType.Medium:
                            Console.Write("Normal");
                            break;

                        case TrafficType.Light:
                            Console.Write("Leicht");
                            break;
                    }

                    Console.Write(", ");
                }

                Console.WriteLine("Rest - Sehr Leicht");
                Console.WriteLine("Die Verkehrsstärken können auch in den Bezirks-Daten manuell angegeben werden.");
            }
        }

        private Dictionary<Street, TrafficType> GetStreetTraffic()
        {
            Dictionary<Street, TrafficType> traffic = new Dictionary<Street, TrafficType>(16);

            foreach (var kvp in _traffic)
            {
                Street? street = kvp.Key.Street;
                if (street == null)
                {
                    continue;
                }

                TrafficType type = kvp.Value;

                if (!traffic.TryGetValue(street, out TrafficType current))
                {
                    traffic.Add(street, type);
                    continue;
                }

                if ((int)type > (int)current)
                {
                    traffic[street] = type;
                }
            }

            return traffic;
        }
    }
}