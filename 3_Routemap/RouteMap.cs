//Copyright Thomas Greshake 2026

using Brieffreund.Routemap;
using Brieffreund.Routemap.Functions;
using Spectre.Console;

namespace Brieffreund
{
    internal class RouteMap
    {
        internal readonly IEulerMap EulerMap;

        private List<RoutePath> _paths;
        internal IList<RoutePath> Paths => _paths;

        private readonly Dictionary<EulerNode, List<RouteNode>> _nodes;
        private readonly Dictionary<EulerNode, List<RoutePath>> _bothwaysOnNode;

        private readonly RouteNode[] _startAndEnd;
        internal RouteNode[] StartAndEnd => _startAndEnd;

        internal int Length => EulerMap.Length;
        internal int PassingLength => EulerMap.PassingLength;
        internal int TotalLength => EulerMap.TotalLength;

        private readonly int _nodeCount, _nodeLoopCount, _singleLoopCount, _pathLoopCount;

        //Setup -----------------------------------------------------------------------

        internal static List<RouteMap> Generate(IUserInput input, IList<IEulerMap> eulerMaps)
        {
            List<RouteMap> maps = new List<RouteMap>(eulerMaps.Count);

            foreach (IEulerMap eulerMap in eulerMaps)
            {
                if (!eulerMap.Success)
                {
                    continue;
                }

                RouteMap map = new(input, eulerMap);
                maps.Add(map);
            }

            PrintSummary(maps);

            return maps;
        }

        internal RouteMap(IUserInput input, IEulerMap eulerMap)
        {
            EulerMap = eulerMap;
            _nodes = new Dictionary<EulerNode, List<RouteNode>>(eulerMap.NodeCount);
            _bothwaysOnNode = new Dictionary<EulerNode, List<RoutePath>>(eulerMap.NodeCount);
            _paths = new List<RoutePath>(eulerMap.PathCount);
            _startAndEnd = new RouteNode[2];

            Eulermap.INodeConnectionCreator connectionCreator = new Eulermap.Functions.NodeConnectionCreator();
            IStorageDistributor distributor = new StorageDistributor(this);

            List<PathLoop> pathLoops = PathLoop.CreateLoops(eulerMap, connectionCreator, out PathLoop[] pathToLoop, out List<PathLoop>[] nodeToLoops, out float[] nodeScores);
            INodeLoopCreator nodeLoopCreator = new NodeLoopCreator(input, eulerMap, pathLoops, nodeToLoops, nodeScores);

            nodeLoopCreator.FindNodeLoops();

            PathLoop initialLoop = SeperateInitialLoop(pathLoops, eulerMap);

            foreach (var node in eulerMap.Nodes.Values)
            {
                CreateNodes(node, nodeLoopCreator.NodeLoops, nodeLoopCreator.SingleLoops);
            }
            for (int i = 0; i < pathLoops.Count; i++)
            {
                CreatePaths(pathLoops[i]);
            }
            CreatePaths(initialLoop); //Always run this after all the other paths are created

            if (_startAndEnd[0].Eulernode != eulerMap.StartAndEndNode[0] || _startAndEnd[1].Eulernode != eulerMap.StartAndEndNode[1])
            {
                throw new Exception();
            }

            distributor.DistributeStorages();

            _nodeCount = _nodes.Values.Sum(l => l.Count);
            _nodeLoopCount = nodeLoopCreator.NodeLoops.Count;
            _singleLoopCount = nodeLoopCreator.SingleLoops.Count;
            _pathLoopCount = pathLoops.Count + 1; //+1 for initial loop
        }

        private static PathLoop SeperateInitialLoop(List<PathLoop> pathLoops, IEulerMap eulerMap)
        {
            PathLoop? initialLoop = pathLoops.FirstOrDefault(l => !l.IsClosed);
            if (initialLoop == null)
            {
                if (eulerMap.StartAndEndNode[0] != eulerMap.StartAndEndNode[1])
                {
                    throw new Exception();
                }
                initialLoop = pathLoops[0];
            }

            pathLoops.Remove(initialLoop);
            return initialLoop;
        }

        private void CreateNodes(EulerNode eulerNode, ICollection<EulerNode> nodeLoops, IDictionary<EulerNode, List<EulerPathway>> singleLoops)
        {
            List<RouteNode> routeNodes = new(); //The eulerpathway is a representative way leading TOWARDS the exact routenode

            _nodes.Add(eulerNode, routeNodes);

            foreach (EulerPathway eulerPathway in eulerNode.Pathways)
            {
                if (routeNodes.Any(n => ConnectsDirectly(n.Pointer, eulerPathway)))
                {
                    continue;
                }

                EulerPathway pointer = eulerPathway.GetOppositeDirection();
                RouteNode newNode = RouteNode.Create(this, eulerNode, pointer);

                routeNodes.Add(newNode);
            }

            if (routeNodes.Count < 2)
            {
                return;
            }

            if (nodeLoops.Contains(eulerNode))
            {
                List<RoutePath> nodePathways = new List<RoutePath>();

                for (int i = 0; i < routeNodes.Count; i++)
                {
                    var current = routeNodes[i];
                    var next = routeNodes[(i + 1) % routeNodes.Count];

                    RoutePath newPath = RoutePath.Create(this, current, next, RoutePathType.LoopConnection, _paths.Count, null);
                    _paths.Add(newPath);
                    nodePathways.Add(newPath);
                }

                _bothwaysOnNode.Add(eulerNode, nodePathways);
            }

            if (!singleLoops.TryGetValue(eulerNode, out List<EulerPathway>? ways))
            {
                return;
            }

            for (int i = 0; i < ways.Count; i += 2)
            {
                RouteNode from = routeNodes.First(n => ConnectsDirectly(n.Pointer, ways[i].GetOppositeDirection()));
                RouteNode to = routeNodes.First(n => ConnectsDirectly(n.Pointer, ways[i + 1].GetOppositeDirection()));
                if (from == to)
                {
                    return;
                }

                RoutePath forward = RoutePath.Create(this, from, to, RoutePathType.SingleConnection, _paths.Count, null);
                _paths.Add(forward);
                RoutePath backward = RoutePath.Create(this, to, from, RoutePathType.SingleConnection, _paths.Count, null);
                _paths.Add(backward);
            }
        }

        private void CreatePaths(PathLoop loop)
        {
            int wayCount = loop.Ways.Count;
            RouteNode? from = null;

            EulerPathway way = loop.Ways[0];
            EulerNode node = way.Origin;
            List<RouteNode> routeNodes = _nodes[node];
            from = routeNodes.First(t => ConnectsDirectly(t.Pointer, way));
            _startAndEnd[0] = from;

            for (int i = 0; i < wayCount; i++)
            {
                EulerPathway currentWay = loop.Ways[i];

                EulerNode nextNode = currentWay.Towards;
                routeNodes = _nodes[nextNode];
                RouteNode to = routeNodes.First(t => ConnectsDirectly(t.Pointer, currentWay.GetOppositeDirection()));
                _startAndEnd[1] = to;

                //Create normal path
                RoutePath path = RoutePath.Create(this, from, to, RoutePathType.Path, _paths.Count, currentWay);
                _paths.Add(path);

                if (i == wayCount - 1 && !loop.IsClosed)
                {
                    return;
                }

                //Create path on node
                EulerPathway nextWay = loop.Ways[(i + 1) % wayCount];
                from = routeNodes.First(t => ConnectsDirectly(t.Pointer, nextWay));
                if (from != to)
                {
                    RoutePath pathOnNode = RoutePath.Create(this, to, from, RoutePathType.NodePath, _paths.Count);
                    _paths.Add(pathOnNode);
                }
            }
        }

        private static bool ConnectsDirectly(EulerPathway from, EulerPathway to) =>
            from.Towards.GetPassingDistance(from, to) <= Constants.CONNECTS_DIRECTLY_PASSING_DISTANCE;

        //Internals -----------------------------------------------------------------------

        public IEnumerable<RouteNode> GetNodes()
        {
            foreach (var kvp in _nodes)
            {
                foreach (var n in kvp.Value)
                {
                    yield return n;
                }
            }
        }

        public IEnumerable<RouteNode> GetNodes(EulerNode node)
        {
            foreach (var n in _nodes[node])
            {
                yield return n;
            }
        }

        public IEnumerable<RoutePath> GetBothwayPathsOnNode(EulerNode node)
        {
            if (_bothwaysOnNode.TryGetValue(node, out var list))
            {
                foreach (RoutePath p in list)
                {
                    yield return p;
                }
            }
        }

        //Privates -----------------------------------------------------------------------

        private static void PrintSummary(IList<RouteMap> maps)
        {
            AnsiConsole.MarkupLine("Aus den Eulerkarten wurden " + maps.Count.ToString() + " Routenkarten erstellt. Hier ein paar Daten:");

            if (maps.Count == 0)
            {
                return;
            }

            var table = new Table();
            table.AddColumns("Element", "Länge (m)", "Knoten", "Pfade", "VK-S", "EK-S", "K-S", "P-S");
            table.AddRow("Beste Karte",
                ((int)maps[0].Length).ToString(),
                maps[0]._nodeCount.ToString(), maps[0]._paths.Count.ToString(),
                maps[0]._nodeLoopCount.ToString(), maps[0]._singleLoopCount.ToString(),
                (maps[0]._nodeLoopCount + maps[0]._singleLoopCount).ToString(),
                maps[0]._pathLoopCount.ToString());

            if (maps.Count > 1)
            {
                RouteMap last = maps[maps.Count - 1];
                table.AddRow("Schlechteste Karte",
                ((int)last.Length).ToString(),
                last._nodeCount.ToString(), last._paths.Count.ToString(),
                last._nodeLoopCount.ToString(), last._singleLoopCount.ToString(),
                (last._nodeLoopCount + last._singleLoopCount).ToString(),
                last._pathLoopCount.ToString());

                table.AddRow("Minimalwerte",
                ((int)maps.Min(m => m.Length)).ToString(),
                maps.Min(m => m._nodeCount).ToString(), maps.Min(m => m._paths.Count).ToString(),
                maps.Min(m => m._nodeLoopCount).ToString(), maps.Min(m => m._singleLoopCount).ToString(),
                maps.Min(m => m._nodeLoopCount + m._singleLoopCount).ToString(),
                maps.Min(m => m._pathLoopCount).ToString());

                table.AddRow("Maximalwerte",
                ((int)maps.Max(m => m.Length)).ToString(),
                maps.Max(m => m._nodeCount).ToString(), maps.Max(m => m._paths.Count).ToString(),
                maps.Max(m => m._nodeLoopCount).ToString(), maps.Max(m => m._singleLoopCount).ToString(),
                maps.Max(m => m._nodeLoopCount + m._singleLoopCount).ToString(),
                maps.Max(m => m._pathLoopCount).ToString());
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();
        }
    }
}