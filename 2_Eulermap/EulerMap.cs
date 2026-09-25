//Copyright Thomas Greshake 2026

using Brieffreund.Eulermap;
using Brieffreund.Eulermap.Functions;
using Spectre.Console;

namespace Brieffreund
{
    internal class EulerMap : IEulerMap
    {
        //Data -----------------------------------------------------------------------

        private readonly bool _success;
        public bool Success => _success;

        private readonly IStreetMap _map;
        public IStreetMap Streetmap => _map;

        private readonly Dictionary<Intersection, EulerNode> _nodes;
        public IDictionary<Intersection, EulerNode> Nodes => _nodes;

        private readonly List<EulerPath>[] _paths;

        private readonly EulerNode[] _startAndEndNode = new EulerNode[2];
        public EulerNode[] StartAndEndNode => _startAndEndNode;

        public IList<EulerPath> GetPaths(StreetSegment seg)
        {
            return _paths[seg.Index];
        }

        private int _pathCount = 0;
        public int PathCount => _pathCount;

        private readonly int _length, _passingLength;
        public int Length => _length;
        public int PassingLength => _passingLength;

        //Setup -----------------------------------------------------------------------
        internal static List<IEulerMap> Generate(IStreetMap streetMap, int count)
        {
            IPathingCountGenerator generator = new PathingCountGenerator(streetMap);
            List<IEulerMap> maps = new(count);

            AnsiConsole.Status().Start("Generiere Eulerkarten...", ctx =>
            {
                generator.Generate(count);
                Convert(streetMap, generator.Result, maps);
            });
            AnsiConsole.MarkupLine("[green]Die Eulerkarten wurden generiert![/]");
            AnsiConsole.WriteLine();

            PrintSummary(maps);

            return maps;
        }

        private static List<IEulerMap> Convert(IStreetMap streetMap, IList<PathingCount[]> pathingMaps, List<IEulerMap> maps)
        {
            foreach (PathingCount[] pathingMap in pathingMaps)
            {
                EulerMap map = new(streetMap, pathingMap);
                if (!map.Success) //This never happens
                {
                    continue;
                }

                maps.Add(map);
            }
            return maps;
        }

        private EulerMap(IStreetMap streetMap, PathingCount[] pathingMap)
        {
            _map = streetMap;
            _success = streetMap.Success;
            _nodes = new(streetMap.Intersections.Count);
            _paths = new List<EulerPath>[streetMap.Segments.Count];
            for (int i = 0; i < streetMap.Segments.Count; i++)
            {
                _paths[i] = new List<EulerPath>((int)pathingMap[i]);
            }
            for (int i = 0; i < 2; i++)
            {
                _startAndEndNode[i] = EulerNode.GetOrCreate(this, streetMap.StartAndEndIntersections[i]);
            }

            if (!_success)
            {
                return;
            }

            IEulerpathCreator creator = new EulerpathCreator(this, pathingMap);
            IEulerDeadendFinder deadendFinder = new EulerDeadendFinder(this);
            INodeConnectionCreator nodeConnectionCreator = new NodeConnectionCreator();
            IEulerpathSideDeterminator sideDeterminator = new EulerpathSideDeterminator(this, nodeConnectionCreator);
            IStorageDistributor distributor = new StorageDistributor(this);
            IEulerMapValidator validator = new EulerMapValidator(this);

            creator.CreatePaths();
            deadendFinder.FindDeadEnds();
            sideDeterminator.DetermineSides();
            EulerPath.SetMailAmounts(this);
            distributor.DistributeStorages();
            _success = validator.Validate();
            _length = (int)_paths.Sum(l => l.Sum(p => p.Length));
            _passingLength = (int)_nodes.Values.Sum(nodeConnectionCreator.EvaluateOptionScore);
        }

        static EulerMap()
        {
            IOnEulerNodeCreated.Register(OnEulerNodeCreated);
            IOnEulerPathCreated.Register(OnEulerpathCreated);
        }

        //Privates -----------------------------------------------------------------------

        private static void PrintSummary(IList<IEulerMap> maps)
        {
            AnsiConsole.MarkupLine("Es wurden " + maps.Count.ToString() + " Euler-Karten erstellt. Hier ein paar Daten:");

            if (maps.Count == 0)
            {
                return;
            }

            var table = new Table();
            table.AddColumns("Element", "Länge (m)", "Bestes Ü-Gewicht", "Gesamt", "Knoten", "Pfade");
            table.AddRow("Beste Karte", maps[0].Length.ToString(), maps[0].PassingLength.ToString(), maps[0].TotalLength.ToString(),
                maps[0].NodeCount.ToString(), maps[0].PathCount.ToString());

            if (maps.Count > 1)
            {
                IEulerMap last = maps[maps.Count - 1];
                table.AddRow("Schlechteste Karte", last.Length.ToString(), last.PassingLength.ToString(), last.TotalLength.ToString(),
                    last.NodeCount.ToString(), last.PathCount.ToString());

                table.AddRow("Minimalwerte",
                    maps.Min(m => m.Length).ToString(),
                    maps.Min(m => m.PassingLength).ToString(),
                    maps.Min(m => m.TotalLength).ToString(),
                    maps.Min(m => m.NodeCount).ToString(),
                    maps.Min(m => m.PathCount).ToString());

                table.AddRow("Maximalwerte",
                    maps.Max(m => m.Length).ToString(),
                    maps.Max(m => m.PassingLength).ToString(),
                    maps.Max(m => m.TotalLength).ToString(),
                    maps.Max(m => m.NodeCount).ToString(),
                    maps.Max(m => m.PathCount).ToString());
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        //Listeners -----------------------------------------------------------------------

        private static void OnEulerNodeCreated(IOnEulerNodeCreated e)
        {
            EulerNode node = e.GetNode();
            if (node.Map is not EulerMap map)
            {
                return;
            }

            map._nodes.Add(node.Intersection, node);
        }

        private static void OnEulerpathCreated(IOnEulerPathCreated e)
        {
            EulerPath path = e.GetPath();
            if (path.Map is not EulerMap map)
            {
                return;
            }

            map._paths[path.Segment.Index].Add(path);
            map._pathCount += 1;
        }
    }
}