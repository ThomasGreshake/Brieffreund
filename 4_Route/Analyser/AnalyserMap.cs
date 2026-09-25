//Copyright Thomas Greshake 2026

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Brieffreund.Analyser
{
    internal class AnalyserMap
    {
        private const float ANALYSIS_DETAIL = 1f;

        //Data -------------------------------------------------------------

        private readonly Dictionary<Address, AnalyserNode> _addressNodes;

        private readonly List<AnalyserNode> _nodes = new(256);
        internal readonly ReadOnlyCollection<AnalyserNode> Nodes;

        private readonly List<AnalyserPath> _paths = new(256);
        internal readonly ReadOnlyCollection<AnalyserPath> Paths;

        //Setup -------------------------------------------------------------

        internal static AnalyserMap Create(IStreetMap baseMap)
        {
            Dictionary<Address, AnalyserNode> addressNodes = new Dictionary<Address, AnalyserNode>(256);
            AnalyserMap map = new(addressNodes);

            Dictionary<StreetNode, List<AnalyserNode>> streetnodeNodes = new(baseMap.Nodes.Count);
            foreach (StreetNode sNode in baseMap.Nodes)
            {
                if (sNode.Count == 0)
                {
                    continue;
                }

                List<AnalyserNode> nodeList = new List<AnalyserNode>();

                if (sNode.Count == 1)
                {
                    StreetPathway way = sNode.Pathways[0];
                    Vector2 offset = Vector2.Normalize(way.Direction.Perpendicular()) * way.Path.Width * .5f;
                    AnalyserNode rightNode = AnalyserNode.Create(map, sNode.Position - offset);
                    nodeList.Add(rightNode);
                    AnalyserNode leftNode = AnalyserNode.Create(map, sNode.Position + offset);
                    nodeList.Add(leftNode);
                }
                else
                {
                    for (int i = 0; i < sNode.Count; i++)
                    {
                        StreetPathway way = sNode.Pathways[i];
                        StreetPathway prev = sNode.Pathways[(i + sNode.Count - 1) % sNode.Count];

                        Vector2 offset = -Vector2.Normalize(way.Direction.Perpendicular()) * way.Path.Width * .5f;

                        AnalyserNode node = AnalyserNode.Create(map, sNode.Position + offset);
                        nodeList.Add(node);
                    }
                }

                for (int i = 0; i < sNode.Pathways.Count; i++)
                {
                    StreetPathway way = sNode.Pathways[i];
                    float length = way.GetPassingWidth();
                    AnalyserNode node = nodeList[i];
                    AnalyserNode nextNode = nodeList[(i + 1) % nodeList.Count];

                    AnalyserPath.Create(node, nextNode, way.Path, length, AnalyserPathType.CrossesStreet);
                }

                streetnodeNodes.Add(sNode, nodeList);
            }

            foreach (StreetPath path in baseMap.Paths)
            {
                List<AnalyserNode> nodeList = streetnodeNodes[path.From];
                int index = GetIndex(path.From.Paths, path);

                AnalyserNode rightNode = nodeList[index];
                AnalyserNode leftNode = nodeList[(index + 1) % nodeList.Count];
                float distance = 0;

                foreach (MailAddress m in path.MailAddresses)
                {
                    float dist = path.PercentageOnPath(m.Position) * path.Length;
                    float diff = dist - distance;

                    if (diff > ANALYSIS_DETAIL)
                    {
                        distance = dist;

                        Vector2 cast = path.CastPosition(m.Position);
                        Vector2 offset = Vector2.Normalize(path.Forward.Direction.Perpendicular()) * path.Width * .5f;

                        AnalyserNode newRightNode = AnalyserNode.Create(map, cast - offset);
                        AnalyserNode newLeftNode = AnalyserNode.Create(map, cast + offset);

                        AnalyserPath.Create(newRightNode, newLeftNode, path, path.GetPassingWidth(), AnalyserPathType.CrossesStreet);
                        AnalyserPath.Create(rightNode, newRightNode, path, diff, AnalyserPathType.RightOfPath);
                        AnalyserPath.Create(leftNode, newLeftNode, path, diff, AnalyserPathType.LeftOfPath);

                        rightNode = newRightNode;
                        leftNode = newLeftNode;
                    }

                    addressNodes.Add(m, m.LeftOfPath ? leftNode : rightNode);
                }

                nodeList = streetnodeNodes[path.To];
                index = GetIndex(path.To.Paths, path);
                AnalyserNode finalRightNode = nodeList[(index + 1) % nodeList.Count];
                AnalyserNode finalLeftNode = nodeList[index];

                AnalyserPath.Create(rightNode, finalRightNode, path, path.Length - distance, AnalyserPathType.RightOfPath);
                AnalyserPath.Create(leftNode, finalLeftNode, path, path.Length - distance, AnalyserPathType.LeftOfPath);
            }

            return map;
        }

        private static int GetIndex<T>(IEnumerable<T> e, T item) where T : class
        {
            int index = 0;
            foreach (T t in e)
            {
                if (t == item)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        private AnalyserMap(Dictionary<Address, AnalyserNode> addressNodes)
        {
            _addressNodes = addressNodes;

            Nodes = _nodes.AsReadOnly();
            Paths = _paths.AsReadOnly();
        }

        static AnalyserMap()
        {
            IOnAnalyserNodeCreated.Register(OnAnalyserNodeCreated);
            IOnAnalyserPathCreated.Register(OnAnalyserPathCreated);
        }

        //Internals -------------------------------------------------------------

        internal bool TryGetNode(Address address, [MaybeNullWhen(false)] out AnalyserNode? node) => _addressNodes.TryGetValue(address, out node);

        internal AnalyserNode? GetNode(Address address)
        {
            if (_addressNodes.TryGetValue(address, out var node))
            {
                return node;
            }
            return null;
        }

        internal bool Contains(Address address) => _addressNodes.ContainsKey(address);

        internal Address? FindAddress(Street street, string num) => _addressNodes.Keys.FirstOrDefault(a => a.Street == street && a.Contains(num));

        internal List<Tuple<Address, bool>> ConvertAddresses(IList<Tuple<Street, string, bool>> route)
        {
            List<Tuple<Address, bool>> addresses = new List<Tuple<Address, bool>>(route.Count);
            foreach (var pair in route)
            {
                Address? address = FindAddress(pair.Item1, pair.Item2);
                if (address == null)
                {
                    if (pair.Item3 && addresses.Count > 0)
                    {
                        address = addresses[addresses.Count - 1].Item1;
                    }
                    else
                    {
                        continue;
                    }
                }
                addresses.Add(Tuple.Create(address, pair.Item3));
            }
            return addresses;
        }

        //Listeners -------------------------------------------------------------

        private static void OnAnalyserNodeCreated(IOnAnalyserNodeCreated e)
        {
            AnalyserNode node = e.GetNode();
            node.Map._nodes.Add(node);
        }

        private static void OnAnalyserPathCreated(IOnAnalyserPathCreated e)
        {
            AnalyserPath path = e.GetPath();
            path.Map._paths.Add(path);
        }
    }
}