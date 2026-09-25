//Copyright Thomas Greshake 2026

using Brieffreund.Streetmap.Functions;
using Brieffreund.Input;
using System.Numerics;
using Spectre.Console;

namespace Brieffreund.Streetmap.Osm
{
    internal class OsmMap : IExternalMap
    {
        //Static -------------------------------------------------------------

        private static readonly HashSet<string> FORBIDDEN_STREETS = new HashSet<string>()
        {
            "motorway", "motorway_link", "motorway_junction", "proposed", "construction", "trunk", "trunk_link", "raceway", "platform", "bus_guideway", "corridor"
        };

        private static readonly HashSet<string> FORBIDDEN_SURFACE = new HashSet<string>()
        {
            "stepping_stones", "unpaved", "rock", "ground", "dirt", "earth", "grass", "mud", "sand", "woodchips", "snow", "ice", "salt"
        };

        private static readonly HashSet<string> FORBIDDEN_SMOOTHNESS = new HashSet<string>()
        {
            "impassable", "very_horrible", "horrible", "very_bad"
        };

        private static readonly HashSet<string> FORBIDDEN_BARRIERS = new HashSet<string>()
        {
            "yes", "bump_gate", "bus_trap", "cattle_grid", "full-height_turnsile", "gate", "hampshire_gate", "horse_stile", "kissing_gate", "lift_gate",
            "sliding_beam", "sliding_gate", "stile", "swing_gate", "turnstile", "wicket_gate"
        };

        private static readonly HashSet<string> FORBIDDEN_BUILDINGS = new HashSet<string>()
        {
            "toilets", "barn", "cowshed", "greenhouse", "slurry_tank", "stable", "sty", "livestock", "garage", "garages", "parking", "construction"
        };

        //Data -------------------------------------------------------------

        private readonly bool _success;
        public bool Success => _success;

        private readonly List<StreetNode> _nodes = new(8192), _inactiveNodes = new(32);
        public IList<StreetNode> Nodes => _nodes;
        public IList<StreetNode> InactiveNodes => _inactiveNodes;

        private readonly List<StreetPath> _paths = new(8192), _inactivePaths = new(32);
        public IList<StreetPath> Paths => _paths;
        public IList<StreetPath> InactivePaths => _inactivePaths;

        private readonly List<Building> _buildings = new(512);
        public IList<Building> Buildings => _buildings;

        private readonly IDictionary<string, Street> _streets;

        private readonly List<PolygonCollider> _additonalPathColliders = new();
        public IList<PolygonCollider> AdditionalPathColliders => _additonalPathColliders;

        //Setup -------------------------------------------------------------

        internal static OsmMap GetMap(IUserInput input, Settings settings)
        {
            int count = 0;

            while (true)
            {
                string? fileName = count == 0 ? (settings.OsmFile != null ? settings.OsmFile : input.MapFile) : null;
                string defaultFileName = count == 1 && settings.OsmFile != null && input.MapFile != null ? input.MapFile : Constants.DEFAULT_OSM_FILE;

                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = AnsiConsole.Ask<string>("Geben Sie den Namen der Datei mit der Osm-Karte ein, oder drücken Sie \"Enter\", um die Karte aus \""
                        + defaultFileName + "\" zu laden. " +
                        "Karten können [link=https://www.openstreetmap.org/export]hier[/] heruntergeladen werden und müssen im osm-Format sein.",
                        defaultFileName).Trim();
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = defaultFileName;
                }
                if (!fileName.Any(c => c == '.'))
                {
                    fileName += ".osm";
                }

                using (OsmFile file = new OsmFile(input, fileName))
                {
                    if (!file.Success)
                    {
                        count += 1;
                        continue;
                    }

                    AnsiConsole.MarkupLine("[green]Die Osm-Daten wurden erfolgreich eingelesen ("
                    + file.Nodes.Count.ToString() + " Knoten, " + file.Paths.Count.ToString() + " Pfade, " +
                    "Längengrad: " + file.Longitude.ToString() + ", Breitengrad: " + file.Latitude.ToString() + ").[/]");

                    OsmMap map = new OsmMap(input, file);
                    if (map.Success)
                    {
                        AnsiConsole.MarkupLine("[green]Die Osm-Karte wurde erfolgreich erstellt (" +
                            map.Nodes.Count.ToString() + " Knoten, "
                            + map.Paths.Count.ToString() + " Pfade, "
                            + map.Buildings.Count.ToString() + " Gebäude).[/]");

                        AnsiConsole.WriteLine("\n");

                        return map;
                    }
                    else
                    {
                        count += 1;
                        AnsiConsole.MarkupLine("[red]Die angegebene Datei ist keine valide Karte des Bezirks.[/]");
                    }
                }
            }
        }

        internal OsmMap(IUserInput input, OsmFile fileMap)
        {
            _streets = input.Streets;

            Dictionary<long, StreetNode> nodes = new(8192);
            Dictionary<Street, List<Building>> buildings = new();

            for (int i = 0; i < fileMap.Paths.Count; i++)
            {
                OsmPath osmPath = fileMap.Paths[i];
                if (!CreatePaths(fileMap, nodes, osmPath))
                {
                    CreateBuilding(fileMap, osmPath, osmPath.Nodes, buildings, input);
                }
            }

            List<long> id = new List<long>() { 0 };
            foreach (var kvp in fileMap.Nodes)
            {
                if (kvp.Value.Flags.Count < 2)
                {
                    continue;
                }

                id[0] = kvp.Key;
                CreateBuilding(fileMap, kvp.Value, id, buildings, input);
            }

            StreetNode.Clean(this);

            IStreetPathTrimmer pathTrimmer = new StreetPathTrimmer(this);
            IUniqueSetEnsurer uniqueEnsurer = new UniqueSetEnsurer(this);

            pathTrimmer.TrimPaths();
            _success = uniqueEnsurer.EnsureUniqueConnectedSet();
        }

        static OsmMap()
        {
            IOnNodeCreated.Register(OnNodeCreated);
            IOnNodeDeleted.Register(OnNodeDeleted);
            IOnNodeIsActiveChanged.Register(OnNodeIsActiveChanged);
            IOnPathCreated.Register(OnPathCreated);
            IOnPathDeleted.Register(OnPathDeleted);
            IOnPathIsActiveChanged.Register(OnPathIsEnabledChanged);
        }

        #region Paths

        private bool CreatePaths(OsmFile fileMap, Dictionary<long, StreetNode> streetNodes, OsmPath osmPath)
        {
            //Not a street
            if (osmPath.Nodes.Count < 2 || !osmPath.Flags.TryGetValue("highway", out string? type) || string.IsNullOrWhiteSpace(type))
            {
                return false;
            }

            //Not allowed (Mainly motorways)
            if (FORBIDDEN_STREETS.Contains(type)) { return false; }

            //Bad surface
            if ((type == "track" && (!osmPath.Flags.TryGetValue("tracktype", out string? tracktype) || tracktype != "grade1"))
                || (osmPath.Flags.TryGetValue("surface", out string? surface) && FORBIDDEN_SURFACE.Contains(surface))
                || (osmPath.Flags.TryGetValue("smoothness", out string? smoothness) && FORBIDDEN_SMOOTHNESS.Contains(smoothness)))
            {
                return false;
            }

            //No parking aisles
            if (osmPath.Flags.TryGetValue("service", out string? parking) && parking == "parking_aisle")
            {
                return false;
            }

            IList<long> nodes = osmPath.Nodes;
            StreetType sType = IdentifyType(type);
            if (osmPath.Flags.TryGetValue("maxspeed", out string? speedValue) && ReadMaxspeed(speedValue, out int maxSpeed))
            {
                StreetType speedType = MaxSpeedToType(maxSpeed);
                sType = (StreetType)Math.Max((int)sType, (int)speedType);
            }

            Street? street = null;
            if (osmPath.Flags.TryGetValue("name", out string? name) && !string.IsNullOrWhiteSpace(name))
            {
                street = Street.GetOrCreate(_streets, name);
            }

            float width;
            if ((!osmPath.Flags.TryGetValue("width", out string? widthValue) && !osmPath.Flags.TryGetValue("est_width", out widthValue)) || !ReadWidth(widthValue, out width))
            {
                width = EstimateWidth(sType);
            }

            int oneway = 0;
            if (sType != StreetType.Tiny && osmPath.Flags.TryGetValue("oneway", out string? onewayValue))
            {
                if (onewayValue == "yes" || onewayValue == "true" || onewayValue == "1")
                {
                    oneway = 1;
                }
                else if (onewayValue == "-1" || onewayValue == "reverse")
                {
                    oneway = -1;
                }

                if (osmPath.Flags.TryGetValue("oneway:bicycle", out string? onewayBicycleValue) && onewayBicycleValue != "no" && onewayBicycleValue != "false")
                {
                    oneway *= 2;
                }
            }
            DirectionType directionType = (DirectionType)oneway;

            ExternalPathFlags flags = ExternalPathFlags.None;
            if (type == "steps" && (!osmPath.Flags.TryGetValue("ramp", out string? ramp) || ramp == "no"))
            {
                flags |= ExternalPathFlags.HasSteps;
            }
            if (osmPath.Flags.TryGetValue("access", out string? access) && (access == "no" || access == "private"))
            {
                flags |= ExternalPathFlags.NoAccess;
            }
            if (directionType != DirectionType.Bothways && osmPath.Flags.ContainsKey("turn:lanes"))
            {
                flags |= ExternalPathFlags.TurnLane;
                directionType = DirectionType.Bothways;
            }

            List<StreetNode> sNodes = new();
            for (int i = 0; i < nodes.Count; i++)
            {
                long id = nodes[i];
                OsmNode osmNode = fileMap.GetNode(id);

                CreateInterNodes(sNodes, osmNode.Position);

                StreetNode sNode = GetOrCreateNode(id, osmNode, streetNodes);
                sNodes.Add(sNode);
            }

            for (int i = 1; i < sNodes.Count; i++)
            {
                StreetNode prev = sNodes[i - 1];
                StreetNode curr = sNodes[i];

                StreetPath.Create(this, prev, curr, sType, street, width, directionType, flags);
            }

            return true;
        }

        private void CreateInterNodes(List<StreetNode> sNodes, Vector2 end)
        {
            if (sNodes.Count == 0)
            {
                return;
            }

            StreetNode node = sNodes[sNodes.Count - 1];

            float dist = Vector2.Distance(node.Position, end);
            if (dist < Constants.MAX_STREETPATH_LENGTH)
            {
                return;
            }

            int parts = 1 + (int)(dist / Constants.MAX_STREETPATH_LENGTH);

            for (int i = 1; i < parts; i++)
            {
                Vector2 pos = Vector2.Lerp(node.Position, end, (float)i / parts);
                StreetNode interNode = StreetNode.Create(this, pos, NodeFlags.None);
                sNodes.Add(interNode);
            }
        }

        private static StreetType IdentifyType(string type)
        {
            switch (type)
            {
                case "primary":
                    return StreetType.Secondary;

                case "secondary":
                    return StreetType.Secondary;

                case "tertiary":
                    return StreetType.Minor;

                case "primary_link":
                    return StreetType.Secondary;

                case "secondary_link":
                    return StreetType.Secondary;

                case "tertiary_link":
                    return StreetType.Minor;

                case "residential":
                    return StreetType.Minor;

                case "living_street":
                    return StreetType.Minor;

                case "service":
                    return StreetType.Tiny;

                case "footway":
                    return StreetType.Tiny;

                case "path":
                    return StreetType.Tiny;

                case "cycleway":
                    return StreetType.Tiny;

                case "pedestrian":
                    return StreetType.Tiny;

                case "unclassified":
                    return StreetType.Minor;

                case "track":
                    return StreetType.Tiny;

                case "road":
                    return StreetType.Minor;

                case "steps":
                    return StreetType.Tiny;

                default:
                    Console.WriteLine("Fehlender Straßentyp: " + type.ToString());
                    return StreetType.Minor;
            }
        }

        private static bool ReadWidth(string? width, out float val)
        {
            if (string.IsNullOrWhiteSpace(width))
            {
                val = 0f;
                return false;
            }

            width.Replace(".", ",");
            string value = string.Concat(width.TakeWhile(x => Char.IsDigit(x) || x == ','));
            string unit = width.Substring(value.Length).Trim();

            if (!string.IsNullOrEmpty(unit) && unit != "m")
            {
                val = 0f;
                return false;
            }

            return float.TryParse(value, out val);
        }

        private static float EstimateWidth(StreetType type)
        {
            switch (type)
            {
                case StreetType.Primary:
                    return Constants.WIDTH_PRIMARY;

                case StreetType.Secondary:
                    return Constants.WIDTH_SECONDARY;

                case StreetType.Tertiary:
                    return Constants.WIDTH_TERTIARY;

                case StreetType.Minor:
                    return Constants.WIDTH_MINOR;

                case StreetType.Tiny:
                    return Constants.WIDTH_TINY;

                default:
                    throw new NotImplementedException();
            }
        }

        private static bool ReadMaxspeed(string? maxspeed, out int val)
        {
            if (string.IsNullOrWhiteSpace(maxspeed))
            {
                val = 0;
                return false;
            }

            string value = string.Concat(maxspeed.TakeWhile(x => Char.IsDigit(x)));
            string unit = maxspeed.Substring(value.Length).Trim();

            if (!string.IsNullOrEmpty(unit) && unit != "km/h")
            {
                val = 0;
                return false;
            }

            return int.TryParse(value, out val);
        }

        private static StreetType MaxSpeedToType(int maxSpeed)
        {
            if (maxSpeed <= 35)
            {
                return StreetType.Tiny;
            }
            if (maxSpeed <= 50)
            {
                return StreetType.Tertiary;
            }
            return StreetType.Secondary;
        }

        #endregion Paths

        #region Nodes

        private StreetNode GetOrCreateNode(long id, OsmNode osmNode, Dictionary<long, StreetNode> nodes)
        {
            if (!nodes.TryGetValue(id, out StreetNode? node))
            {
                node = CreateNode(osmNode);
                nodes.Add(id, node);
            }

            return node;
        }

        private StreetNode CreateNode(OsmNode osmNode)
        {
            NodeFlags flags = NodeFlags.None;

            if ((osmNode.Flags.TryGetValue("crossing", out string? traffic_signals) || osmNode.Flags.TryGetValue("highway", out traffic_signals))
                && traffic_signals == "traffic_signals")
            {
                flags |= NodeFlags.TrafficSignals;
            }

            if (osmNode.Flags.TryGetValue("barrier", out string? barrier) && FORBIDDEN_BARRIERS.Contains(barrier))
            {
                flags |= NodeFlags.Barrier;
            }

            StreetNode node = StreetNode.Create(this, osmNode.Position, flags);
            return node;
        }

        #endregion Nodes

        #region Buildings

        private void CreateBuilding(OsmFile file, IFlagPole flags, IList<long> longNodes, Dictionary<Street, List<Building>> buildings, IUserInput input)
        {
            List<Vector2> nodes = ConvertNodesToPositions(file, longNodes);
            if (nodes.Count == 0) { return; }

            if ((!flags.Flags.TryGetValue("building", out string? bType) && !flags.Flags.TryGetValue("amenity", out bType))
                || string.IsNullOrWhiteSpace(bType))
            {
                bType = "residential";
            }
            else if (FORBIDDEN_BUILDINGS.Contains(bType))
            {
                CreateCollider(file, flags, nodes);
                return;
            }

            if ((!flags.Flags.TryGetValue("addr:street", out string? streetName) && !flags.Flags.TryGetValue("addr:place", out streetName))
                || string.IsNullOrWhiteSpace(streetName) ||
                !flags.Flags.TryGetValue("addr:housenumber", out string? fullNumber) || string.IsNullOrWhiteSpace(fullNumber))
            {
                return;
            }

            if (!Street.FullnumberToNumberId(fullNumber, out int id))
            {
                CreateCollider(file, flags, nodes);
                return;
            }

            if (!flags.Flags.TryGetValue("building:levels", out string? levels) || !int.TryParse(levels, out int level))
            {
                level = 2;
            }

            BuildingType type = IdentifyBuildingType(bType);

            Street street = Street.GetOrCreate(_streets, streetName);

            Vector2 entrance = GetEntrancePosition(file, longNodes, nodes);

            Building building = new(street, id, nodes, entrance, type, level);
            if (!buildings.TryGetValue(street, out List<Building>? buildingsList))
            {
                buildingsList = new List<Building>() { building };
                buildings.Add(street, buildingsList);
            }
            else if (buildingsList.Any(b => b.Merge(building)))
            {
                return;
            }
            else
            {
                buildingsList.Add(building);
            }

            _buildings.Add(building);
        }

        private void CreateCollider(OsmFile file, IFlagPole flags, List<Vector2> nodes)
        {
            if (nodes.Count < 3 || (!flags.Flags.ContainsKey("building") && !flags.Flags.ContainsKey("amenity")))
            {
                return;
            }

            PolygonCollider collider = new(nodes);
            _additonalPathColliders.Add(collider);
        }

        private static BuildingType IdentifyBuildingType(string type)
        {
            switch (type)
            {
                case "apartments":
                    return BuildingType.Apartments;

                case "barracks":
                    return BuildingType.Apartments;

                case "bungalow":
                    return BuildingType.Home;

                case "cabin":
                    return BuildingType.Secondary;

                case "annexe":
                    return BuildingType.Secondary;

                case "detached":
                    return BuildingType.Secondary;

                case "dormitory":
                    return BuildingType.Apartments;

                case "farm":
                    return BuildingType.Home;

                case "hotel":
                    return BuildingType.Address;

                case "house":
                    return BuildingType.Home;

                case "residential":
                    return BuildingType.Residential;

                case "semidetached_house":
                    return BuildingType.Home;

                case "stilt_house":
                    return BuildingType.Home;

                case "terrace":
                    return BuildingType.Residential;

                case "commercial":
                    return BuildingType.Address;

                case "conservatory":
                    return BuildingType.Address;

                case "industrial":
                    return BuildingType.Address;

                case "kiosk":
                    return BuildingType.Address;

                case "office":
                    return BuildingType.Address;

                case "retail":
                    return BuildingType.Address;

                case "supermarket":
                    return BuildingType.Address;

                case "warehouse":
                    return BuildingType.Address;

                case "college":
                    return BuildingType.Address;

                case "hospital":
                    return BuildingType.Primary;

                case "government":
                    return BuildingType.Primary;

                case "kindergarten":
                    return BuildingType.Address;

                case "museum":
                    return BuildingType.Address;

                case "train_station":
                    return BuildingType.Address;

                case "university":
                    return BuildingType.Primary;

                case "stadium":
                    return BuildingType.Primary;

                case "hut":
                    return BuildingType.Secondary;

                case "shed":
                    return BuildingType.Secondary;

                case "riding_hall":
                    return BuildingType.Address;

                case "sports_hall":
                    return BuildingType.Address;

                case "sports_centre":
                    return BuildingType.Address;

                default:
                    return BuildingType.Residential;
            }
        }

        private static List<Vector2> ConvertNodesToPositions(OsmFile file, IList<long> nodes)
        {
            List<Vector2> positions = new List<Vector2>(nodes.Count);
            int end = nodes.Count > 1 && nodes[0] == nodes[nodes.Count - 1] ? nodes.Count - 1 : nodes.Count;
            for (int i = 0; i < end; i++)
            {
                long node = nodes[i];
                if (file.TryGetNode(node, out OsmNode? osmNode) && osmNode != null)
                {
                    positions.Add(osmNode.Position);
                }
            }

            return positions;
        }

        private static Vector2 GetEntrancePosition(OsmFile file, IList<long> nodes, IList<Vector2> positions)
        {
            List<Tuple<Vector2, int>> entrances = new();
            foreach (long node in nodes)
            {
                if (!file.TryGetNode(node, out OsmNode? osmNode) || osmNode == null || !osmNode.Flags.TryGetValue("entrance", out string? entrance) || entrance == "no")
                {
                    continue;
                }

                int value = 2;
                if (entrance == "home" || entrance == "main")
                {
                    value = 3;
                }
                else if (entrance == "secondary" || entrance == "staircase")
                {
                    value = 1;
                }
                else if (entrance == "garage")
                {
                    value = 0;
                }

                entrances.Add(Tuple.Create(osmNode.Position, value));
            }

            if (entrances.Count == 0)
            {
                return new Vector2(positions.Select(v => v.X).Min() + positions.Select(v => v.X).Max(),
                    positions.Select(v => v.Y).Min() + positions.Select(v => v.Y).Max()) * 0.5f;
            }

            int max = entrances.Max(t => t.Item2);
            float x = entrances.Where(t => t.Item2 == max).Select(t => t.Item1.X).Average();
            float y = entrances.Where(t => t.Item2 == max).Select(t => t.Item1.Y).Average();
            return new Vector2(x, y);
        }

        #endregion Buildings

        #region Listeners

        private static void OnNodeCreated(IOnNodeCreated e)
        {
            StreetNode node = e.GetNode();
            if (node.Map is not OsmMap map) { return; }

            if (node.IsActive)
            {
                throw new Exception();
            }

            map._inactiveNodes.Add(node);
        }

        private static void OnNodeDeleted(IOnNodeDeleted e)
        {
            StreetNode node = e.GetNode();
            if (node.Map is not OsmMap map) { return; }

            if (node.IsActive)
            {
                throw new Exception();
            }

            if (!map._inactiveNodes.Remove(node))
            {
                throw new Exception();
            }
        }

        private static void OnNodeIsActiveChanged(IOnNodeIsActiveChanged e)
        {
            StreetNode node = e.GetNode();
            if (node.Map is not OsmMap map) { return; }

            if (node.IsActive)
            {
                if (!map._inactiveNodes.Remove(node))
                {
                    throw new Exception();
                }

                map._nodes.Add(node);
            }
            else
            {
                if (!map._nodes.Remove(node))
                {
                    throw new Exception();
                }

                map._inactiveNodes.Add(node);
            }
        }

        private static void OnPathCreated(IOnPathCreated e)
        {
            StreetPath path = e.GetPath();
            if (path.Map is not OsmMap map) { return; }

            if (path.IsActive)
            {
                throw new Exception();
            }

            map._inactivePaths.Add(path);
        }

        private static void OnPathDeleted(IOnPathDeleted e)
        {
            StreetPath path = e.GetPath();
            if (path.Map is not OsmMap map) { return; }

            if (path.IsActive)
            {
                throw new Exception();
            }

            if (!map._inactivePaths.Remove(path))
            {
                throw new Exception();
            }
        }

        private static void OnPathIsEnabledChanged(IOnPathIsActiveChanged e)
        {
            StreetPath path = e.GetPath();
            if (path.Map is not OsmMap map) { return; }

            if (path.IsActive)
            {
                if (!map._inactivePaths.Remove(path))
                {
                    throw new Exception();
                }

                map._paths.Add(path);
            }
            else
            {
                if (map._paths[path.Index] != path)
                {
                    throw new Exception();
                }

                map._paths.RemoveAt(path.Index);

                map._inactivePaths.Add(path);
            }
        }

        #endregion Listeners
    }
}