//Copyright Thomas Greshake 2026

using Brieffreund.Analyser;
using Brieffreund.Streetmap;
using Brieffreund.Streetmap.Functions;
using Spectre.Console;

namespace Brieffreund
{
    internal class StreetMap : IStreetMap
    {
        //Data -------------------------------------------------------------

        private readonly IUserInput _input;

        private bool _success = true;
        public bool Success => _success;

        private readonly IList<StreetNode> _nodes, _inactiveNodes;
        public IList<StreetNode> Nodes => _nodes;
        public IList<StreetNode> InactiveNodes => _inactiveNodes;

        private readonly IList<StreetPath> _paths, _inactivePaths;
        public IList<StreetPath> Paths => _paths;
        public IList<StreetPath> InactivePaths => _inactivePaths;

        private readonly IList<StreetSegment> _segments = new List<StreetSegment>(), _inactiveSegments = new List<StreetSegment>();
        public IList<StreetSegment> Segments => _segments;
        public IList<StreetSegment> InactiveSegments => _inactiveSegments;

        private readonly IDictionary<StreetNode, Intersection> _intersections = new Dictionary<StreetNode, Intersection>(),
            _inactiveIntersections = new Dictionary<StreetNode, Intersection>();

        public IDictionary<StreetNode, Intersection> Intersections => _intersections;
        public IDictionary<StreetNode, Intersection> InactiveIntersections => _inactiveIntersections;

        private readonly MailAddress?[] _startAndEndAddresses = new MailAddress?[2];
        public MailAddress?[] StartAndEndAddresses => _startAndEndAddresses;

        private readonly Intersection[] _startAndEndIntersections = new Intersection[2];
        public Intersection[] StartAndEndIntersections => _startAndEndIntersections;

        private readonly IList<Storage> _storages = new List<Storage>();
        public IList<Storage> Storages => _storages;

        private int _totalMailAmount = 0;
        public int TotalMailAmount => _totalMailAmount;

        //Setup -------------------------------------------------------------

        #region Setup

        public static StreetMap Create(IUserInput input, IExternalMap externalMap, out AnalyserMap analyserMap)
        {
            StreetMap map = new(input, externalMap);
            if (!map.Success)
            {
                analyserMap = AnalyserMap.Create(map);
                return map;
            }

            IStreetSegmentCreator creator = new StreetSegmentCreator(map);
            IInternalFlagSetter trueDeadEndSetter = new TrueDeadEndSetter(map);
            IAddressRelocator relocator = new AddressRelocator(map);
            IStreetSegmentMerger merger = new StreetSegmentMerger(map, creator, relocator);
            IStreetSegmentTrimmer segmentTrimmer = new StreetSegmentTrimmer(map, merger);
            IBuildingToPathCaster caster = new BuildingToPathCaster(map, externalMap.Buildings, externalMap.AdditionalPathColliders);
            IAddressCreator addressCreator = new AddressCreator(input, map, caster);
            IStreetSegmentSplitter splitter = new StreetSegmentSplitter(map, creator, relocator);
            IStreetPathCutter pathCutter = new StreetPathCutter(input, map, creator, caster, splitter, merger, externalMap.Buildings, externalMap.AdditionalPathColliders);
            IStorageDistanceCalculator storageDistanceCalculator = new StorageDistanceCalculator(map);
            IStorageTrimmer storageTrimmer = new StorageTrimmer(map, storageDistanceCalculator);
            ITrafficTypeIdentifier trafficIdentifier = new TrafficTypeIdentifier(input, map);
            IStreetSegmentDisabler segmentDisabler = new StreetSegmentDisabler(map);
            IInternalFlagSetter onlyConnectionSetter = new OnlyConnectionSetter(map);
            IInternalFlagSetter requiredSegmentSetter = new RequiredSegmentSetter(map);
            ISinglePathCreator singlePathCreator = new SinglePathCreator(map);
            IStartAndEndFinder startEndFinder = new StartAndEndFinder(map);
            IStreetMapValidator validator = new StreetMapValidator(map);

            creator.CreateSegments();
            segmentTrimmer.Trim();
            trueDeadEndSetter.SetFlags();

            AnsiConsole.Status().Start("Den Gebäuden werden Eingänge zugeordnet...", ctx =>
            {
                caster.CastBuildings();
            });

            addressCreator.CreateAddresses();
            PrintFailedAddresses(addressCreator.FailedToCreate);
            trafficIdentifier.IdentifyTrafficType();

            analyserMap = AnalyserMap.Create(map);

            pathCutter.CutRestricted();

            AnsiConsole.Status().Start("Die Straßen werden zusammengeschnitten...", ctx =>
            {
                segmentDisabler.DisableSegments();
            });

            pathCutter.CutPaths();
            PrintFailedCuts(pathCutter.FailedCuts);
            storageTrimmer.TrimStorages();
            onlyConnectionSetter.SetFlags();
            merger.Merge();

            splitter.SplitSegments();
            merger.Merge();
            singlePathCreator.GenerateSinglePaths();
            storageDistanceCalculator.CalculateStorageDistances();
            requiredSegmentSetter.SetFlags();
            if (startEndFinder.FindStartAndEnd())
            {
                AnsiConsole.MarkupLine("[DarkOrange]Die Suche nach einem guten Start- und/oder Endpunkt war erfolgreich.[/]");
            }

            AnsiConsole.MarkupLine("[green]Die Straßenkarte wurde erstellt![/]");
            AnsiConsole.WriteLine();

            map._success = validator.Validate();

            if (map._success)
            {
                map.PrintSummary();
            }

            return map;
        }

        private StreetMap(IUserInput input, IExternalMap externalMap)
        {
            _input = input;
            _nodes = externalMap.Nodes;
            _inactiveNodes = externalMap.InactiveNodes;
            _paths = externalMap.Paths;
            _inactivePaths = externalMap.InactivePaths;
            _success = input.Success && externalMap.Success;
        }

        static StreetMap()
        {
            IOnIntersectionCreated.Register(OnIntersectionCreated);
            IOnIntersectionRemoved.Register(OnIntersectionRemoved);
            IOnIntersectionIsActiveChanged.Register(OnIntersectionIsActiveChanged);
            IOnSegmentCreated.Register(OnSegmentCreated);
            IOnSegmentDeleted.Register(OnSegmentDeleted);
            IOnSegmentRemoved.Register(OnSegmentRemoved);
            IOnSegmentIsActiveChanged.Register(OnSegmentIsActiveChanged);
            IOnAddressCreated.Register(OnAddressCreated);
            IOnAddressAssociated.Register(OnAddressAssociated);
            IAddressRelocator.Register(OnAddressRelocation);
            IStartAndEndFinder.Register(OnStartAndEndFound);
            IStorageTrimmer.Register(OnStoragesRemoved);
        }

        private void PrintSummary()
        {
            int addressCount = _paths.Sum(p => p.MailAddresses.Count);

            AnsiConsole.MarkupLine("Die Straßenkarte wurde erfolgreich erstellt. Hier ein paar Daten:");
            var table = new Table();
            table.AddColumns("Element", "Anzahl", "Element", "Anzahl");
            table.AddRow("Knoten", _nodes.Count.ToString(), "Kreuzungen", _intersections.Count.ToString());
            table.AddRow("Pfade", _paths.Count.ToString(), "Segmente", _segments.Count.ToString());
            table.AddRow("Addressen", addressCount.ToString(), "Ablagen", _storages.Count.ToString());
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();
        }

        private static void PrintFailedAddresses(IList<Tuple<string, AddressFailureReason>> list)
        {
            if (list.Count == 0)
            {
                return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Die folgenden Addressen konnten nicht zugeordnet werden:[/]");
            foreach (var item in list)
            {
                string line = "[red]" + item.Item1 + ", da ";
                switch (item.Item2)
                {
                    case AddressFailureReason.Number:
                        line += "die Nummer der Adresse unleserlich ist (Schreibfehler?).";
                        break;

                    case AddressFailureReason.Street:
                        line += "die Straße der Adresse nicht in der Karte enthalten ist (Schreibfehler?).";
                        break;

                    default:
                        line += "unbekannt.";
                        break;
                }
                line += "[/]";
                AnsiConsole.MarkupLine(line);
            }

            AnsiConsole.WriteLine();
        }

        private static void PrintFailedCuts(IList<Tuple<string, string, CutterFailureReason>> list)
        {
            if (list.Count == 0)
            {
                return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Die folgenden Straßenverbindungen sollten, aber konnten nicht, gelöscht werden:[/]");
            foreach (var item in list)
            {
                string line = "[red]" + item.Item1 + " und " + item.Item2 + ", da ";
                switch (item.Item3)
                {
                    case CutterFailureReason.FirstAddressNumber:
                        line += "die Nummer der ersten Adresse unleserlich ist (Schreibfehler?).";
                        break;

                    case CutterFailureReason.FirstAddressStreet:
                        line += "die Straße der ersten Adresse nicht in der Karte enthalten ist (Schreibfehler?).";
                        break;

                    case CutterFailureReason.SecondAddressNumber:
                        line += "die Nummer der zweiten Adresse unleserlich ist (Schreibfehler?).";
                        break;

                    case CutterFailureReason.SecondAddressStreet:
                        line += "die Straße der zweiten Adresse nicht in der Karte enthalten ist (Schreibfehler?).";
                        break;

                    case CutterFailureReason.Distance:
                        line += "die Adressen zu weit auseinander liegen.";
                        break;

                    case CutterFailureReason.Divide:
                        line += "dies den Bezirk in zwei nicht verbundene Hälften teilen würde.";
                        break;

                    default:
                        line += "unbekannt.";
                        break;
                }
                line += "[/]";

                AnsiConsole.MarkupLine(line);
            }

            AnsiConsole.WriteLine();
        }

        #endregion Setup

        #region Listeners

        private static void OnIntersectionCreated(IOnIntersectionCreated e)
        {
            Intersection inter = e.GetIntersection();

            if (inter.Map is not StreetMap map)
            {
                return;
            }

            if (inter.IsActive)
            {
                throw new Exception();
            }

            map._inactiveIntersections.Add(inter.Streetnode, inter);
        }

        private static void OnIntersectionRemoved(IOnIntersectionRemoved e)
        {
            Intersection inter = e.GetIntersection();

            if (inter.Map is not StreetMap map)
            {
                return;
            }

            if (inter.IsActive ? !map._intersections.Remove(inter.Streetnode) : !map._inactiveIntersections.Remove(inter.Streetnode))
            {
                throw new Exception();
            }
        }

        private static void OnIntersectionIsActiveChanged(IOnIntersectionIsActiveChanged e)
        {
            Intersection inter = e.GetIntersection();
            if (inter.Map is not StreetMap map)
            {
                return;
            }

            if (inter.IsActive)
            {
                if (!map._inactiveIntersections.Remove(inter.Streetnode))
                {
                    throw new Exception();
                }

                map._intersections.Add(inter.Streetnode, inter);
            }
            else
            {
                if (!map._intersections.Remove(inter.Streetnode))
                {
                    throw new Exception();
                }

                map._inactiveIntersections.Add(inter.Streetnode, inter);
            }
        }

        private static void OnSegmentCreated(IOnSegmentCreated e)
        {
            StreetSegment segment = e.GetSegment();
            if (segment.Map is not StreetMap map)
            {
                return;
            }

            if (segment.IsActive)
            {
                throw new Exception();
            }

            map._inactiveSegments.Add(segment);
        }

        private static void OnSegmentDeleted(IOnSegmentDeleted e)
        {
            StreetSegment segment = e.GetSegment();
            OnSegmentRemoved(segment);
        }

        private static void OnSegmentRemoved(IOnSegmentRemoved e)
        {
            StreetSegment segment = e.GetSegment();
            if (segment.Map is not StreetMap map)
            {
                return;
            }

            if (segment.IsActive)
            {
                if (map._segments[segment.Index] != segment)
                {
                    throw new Exception();
                }

                map._segments.RemoveAt(segment.Index);
            }
            else if (!map._inactiveSegments.Remove(segment))
            {
                throw new Exception();
            }
        }

        private static void OnSegmentIsActiveChanged(IOnSegmentIsActiveChanged e)
        {
            StreetSegment segment = e.GetSegment();
            if (segment.Map is not StreetMap map)
            {
                return;
            }

            if (segment.IsActive)
            {
                if (!map._inactiveSegments.Remove(segment))
                {
                    throw new Exception();
                }

                map._segments.Add(segment);
            }
            else
            {
                if (map._segments[segment.Index] != segment)
                {
                    throw new Exception();
                }

                map._segments.RemoveAt(segment.Index);

                map._inactiveSegments.Add(segment);
            }
        }

        private static void OnAddressCreated(IOnAddressCreated e)
        {
            Address address = e.GetAddress();
            IStreetMap map = address.Map;
            if (map is not StreetMap streetMap)
            {
                return;
            }

            if (address is Storage storage)
            {
                streetMap._storages.Add(storage);
            }
            else if (address is MailAddress mail)
            {
                streetMap._totalMailAmount += mail.MailAmount;

                CheckStartAndEnd(streetMap, mail);
            }
        }

        private static void OnAddressAssociated(IOnAddressAssociated e)
        {
            Address address = e.GetAddress();
            IStreetMap map = address.Map;

            if (map is not StreetMap streetMap || address is not MailAddress mail)
            {
                return;
            }

            CheckStartAndEnd(streetMap, mail);
        }

        private static void CheckStartAndEnd(StreetMap streetMap, MailAddress address)
        {
            Tuple<Street, string>? start = streetMap._input.StartAndEnd[0];
            if (start != null && start.Item1 == address.Street && address.Contains(start.Item2))
            {
                streetMap._startAndEndAddresses[0] = address;
            }

            Tuple<Street, string>? end = streetMap._input.StartAndEnd[1];
            if (end != null && end.Item1 == address.Street && address.Contains(end.Item2))
            {
                streetMap._startAndEndAddresses[1] = address;
            }
        }

        private static void OnAddressRelocation(IAddressRelocator relocator)
        {
            if (relocator.Map is not StreetMap map)
            {
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                Intersection? inter = relocator.StartAndEndIntersections[i];
                if (inter != null)
                {
                    map._startAndEndIntersections[i] = inter;
                }
            }
        }

        private static void OnStartAndEndFound(IStartAndEndFinder e)
        {
            if (e.Map is not StreetMap map)
            {
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                map._startAndEndIntersections[i] = e.StartAndEndIntersections[i];
            }
        }

        private static void OnStoragesRemoved(IStorageTrimmer e)
        {
            if (e.Map is not StreetMap map)
            {
                return;
            }

            foreach (Storage s in e.ToRemove)
            {
                map._storages.Remove(s);
            }
        }

        #endregion Listeners
    }
}