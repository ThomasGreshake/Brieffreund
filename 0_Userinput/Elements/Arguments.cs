//Copyright Thomas Greshake 2026

namespace Brieffreund.Input
{
    [Obsolete]
    internal class Arguments
    {
        internal readonly string? DistrictFile = null, OsmFile = null;
        internal readonly int SearchDepth = -1, DesiredStorageCount = -1;
        internal readonly bool? DoNotCrossAlleys = null;
        internal readonly bool JustHelp = false;
        internal readonly AnalysisMode AnalysisMode = AnalysisMode.Null;

        internal Arguments(string[] args)
        {
            Dictionary<string, string> arguments = ReadArguments(args);

            if (arguments.TryGetValue("d", out string? districtFile) || arguments.TryGetValue("district", out districtFile)
                || arguments.TryGetValue("b", out districtFile) || arguments.TryGetValue("bezirk", out districtFile))
            {
                DistrictFile = districtFile;
            }

            if (arguments.TryGetValue("o", out string? osmFile) || arguments.TryGetValue("osm", out osmFile)
                || arguments.TryGetValue("m", out osmFile) || arguments.TryGetValue("map", out osmFile)
                || arguments.TryGetValue("k", out osmFile) || arguments.TryGetValue("karte", out osmFile))
            {
                OsmFile = osmFile;
            }

            if ((arguments.TryGetValue("depth", out string? depth) || arguments.TryGetValue("suchtiefe", out depth))
                && int.TryParse(depth, out int searchDepth) && searchDepth >= 1)
            {
                SearchDepth = searchDepth;
            }

            if ((arguments.TryGetValue("storages", out string? storageCount) || arguments.TryGetValue("ablagen", out storageCount))
                && int.TryParse(storageCount, out int count) && count >= 0)
            {
                DesiredStorageCount = count;
            }

            if (arguments.TryGetValue("gasse", out string? alley) || arguments.TryGetValue("alley", out alley))
            {
                if (alley == "stopp" || alley == "stop")
                {
                    DoNotCrossAlleys = true;
                }
                else if (alley == "erlaubt" || alley == "allowed")
                {
                    DoNotCrossAlleys = false;
                }
            }

            if ((arguments.TryGetValue("analysis", out string? analysis) || arguments.TryGetValue("analyse", out analysis))
                && (analysis == "compare" || analysis == "vergleich"))
            {
                AnalysisMode = AnalysisMode.AnalyseAndCompare;
            }
        }

        private Dictionary<string, string> ReadArguments(string[] args)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>();

            string? key = "d";

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim().ToLower();

                if (arg.Length == 0)
                {
                    continue;
                }

                if (arg[0] == '-')
                {
                    key = string.Concat(arg.Where(x => x != '-')).ToLower();

                    if (key == "p" || key == "print")
                    {
                        arguments.TryAdd(key, "y");
                    }
                    else if (key == "h" || key == "help" || key == "hilfe")
                    {
                        arguments.TryAdd(key, "y");
                    }
                    continue;
                }

                if (key != null)
                {
                    arguments[key] = arg;
                }

                key = null;
            }

            return arguments;
        }
    }
}