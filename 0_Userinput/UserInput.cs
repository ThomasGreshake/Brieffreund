//Copyright Thomas Greshake 2026

using Spectre.Console;

namespace Brieffreund.Input
{
    internal class UserInput : IUserInput
    {
        //Static -------------------------------------------------------------

        private static readonly char[] SEPERATORS = new char[] { ' ', '\t', ';', ',' };

        //Data -------------------------------------------------------------

        private readonly bool _success = true;

        private readonly string _fileName;
        public string FileName => _fileName;

        private string? _mapFile = null;
        public string? MapFile => _mapFile;

        public bool Success => _success;

        private readonly IList<int> _postCodes = new List<int>();
        public IList<int> PostCodes => _postCodes;

        private readonly IList<string> _cities = new List<string>();
        public IList<string> Cities => _cities;

        private readonly Tuple<Street, string>?[] _startAndEnd = new Tuple<Street, string>[2];
        public Tuple<Street, string>?[] StartAndEnd => _startAndEnd;

        private readonly IDictionary<string, Street> _streets = new Dictionary<string, Street>();
        public IDictionary<string, Street> Streets => _streets;

        private readonly IDictionary<Street, IList<string>> _mailAddresses = new Dictionary<Street, IList<string>>();
        public IDictionary<Street, IList<string>> MailAddresses => _mailAddresses;

        private readonly IList<Tuple<Street, string, bool>> _orderedAddresses = new List<Tuple<Street, string, bool>>();
        public IList<Tuple<Street, string, bool>> OrderedAddresses => _orderedAddresses;

        private readonly IList<Tuple<Street, string, int>> _storages = new List<Tuple<Street, string, int>>();
        public IList<Tuple<Street, string, int>> Storages => _storages;

        private readonly IDictionary<Street, TrafficType> _trafficTypes = new Dictionary<Street, TrafficType>();
        public IDictionary<Street, TrafficType> TrafficTypes => _trafficTypes;

        private int _desiredStorageCount;
        public int DesiredStorageCount => _desiredStorageCount;

        private int _searchDepth = Constants.DEFAULT_SEARCHDEPTH;
        public int SearchDepth => _searchDepth;

        private readonly IList<Tuple<Street, string, Street, string>> _cuts = new List<Tuple<Street, string, Street, string>>();
        public IList<Tuple<Street, string, Street, string>> Cuts => _cuts;

        private bool _doNotCrossAlleys = true;
        public bool DoNotCrossAlleys => _doNotCrossAlleys;

        private bool _compareAnalysis = false;
        public bool CompareAnalysis => _compareAnalysis;

        //Setup -------------------------------------------------------------

        internal static UserInput GetInput(Settings settings)
        {
            bool useSettings = true;

            while (true)
            {
                string? fileName = useSettings ? settings.DistrictFile : null;

                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = AnsiConsole.Ask<string>("Geben Sie den Namen der Datei mit den Bezirks-Daten ein, oder drücken Sie \"Enter\", " +
                        "um den Bezirk aus \"" + Constants.DEFAULT_DISTRICT_FILE + "\" zu laden.", Constants.DEFAULT_DISTRICT_FILE).Trim();
                }

                if (!fileName.Any(c => c == '.'))
                {
                    fileName += ".txt";
                }

                UserInput input = new UserInput(settings, fileName);
                if (input.Success)
                {
                    AnsiConsole.MarkupLine("[green]Die Bezirks-Datei wurde eingelesen (" + input._mailAddresses.Values.Sum(l => l.Count).ToString()
                    + " Adressen, " + input._storages.Count.ToString() + " Ablagen).[/]");

                    input.AskForSearchDepth();

                    return input;
                }
                else
                {
                    useSettings = false;
                }
            }
        }

        internal UserInput(Settings settings, string fileName)
        {
            _fileName = fileName;

            UserInputFile file = new UserInputFile(_fileName);
            if (!file.Success)
            {
                _success = false;
                return;
            }

            ReadFile(file);
            ApplySettings(settings);
            FixInput();

            IInputValidator validator = new Functions.InputValidator(this);
            _success = validator.Validate();
        }

        private void ReadFile(UserInputFile file)
        {
            foreach (Tuple<string, string> d in file.Data)
            {
                string key = d.Item1;
                string value = d.Item2;

                switch (key)
                {
                    case "plz":
                        ReadPostcode(value);
                        break;

                    case "stadt":
                        ReadCities(value);
                        break;

                    case "start":
                        ReadStartEndAddress(value, true);
                        break;

                    case "ende":
                        ReadStartEndAddress(value, false);
                        break;

                    case "adr":
                        ReadAddresses(value);
                        break;

                    case "abl":
                        ReadStorage(value);
                        break;

                    case "anz":
                        ReadDesiredCount(value);
                        break;

                    case "ver":
                        ReadTrafficType(value);
                        break;

                    case "suchtiefe":
                        ReadSearchDepth(value);
                        break;

                    case "rem":
                        ReadCut(value);
                        break;

                    case "karte":
                        ReadMap(value);
                        break;

                    case "gasse":
                        ReadAlley(value);
                        break;

                    case "analyse":
                        ReadAnalysis(value);
                        break;

                    case "adrabl":
                        ReadAddresses(value, true);
                        break;

                    default:
                        continue;
                }
            }
        }

        private void AskForSearchDepth()
        {
            while (true)
            {
                int depth = AnsiConsole.Ask<int>("Geben Sie die Suchtiefe an, oder drücken Sie \"Enter\", um die Suchtiefe \""
                    + _searchDepth.ToString() + "\" zu nutzen.", _searchDepth);

                if (depth <= 0)
                {
                    AnsiConsole.MarkupLine("[red]Die Suchtiefe muss größer als 1 sein.[/]");
                    continue;
                }

                _searchDepth = depth;
                return;
            }
        }

        private void ApplySettings(Settings settings)
        {
            if (settings.SearchDepth > 0)
            {
                _searchDepth = settings.SearchDepth;
            }
            if (settings.DesiredStorageCount >= 0)
            {
                _desiredStorageCount = settings.DesiredStorageCount;
            }
            if (settings.DoNotCrossAlleys != null)
            {
                _doNotCrossAlleys = settings.DoNotCrossAlleys == "j";
            }
            if (settings.CompareAnalysis)
            {
                _compareAnalysis = true;
            }
        }

        private void ReadPostcode(string line)
        {
            string[] parts = line.Split(SEPERATORS, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var plz) && !_postCodes.Contains(plz))
                {
                    _postCodes.Add(plz);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Die folgende Postleitzahl konnte nicht gelesen werden: " + part + "[/]");
                }
            }
        }

        private void ReadCities(string line)
        {
            string[] parts = line.Split(SEPERATORS, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string c = part.ToLower();
                if (!_cities.Contains(c))
                {
                    _cities.Add(c);
                }
            }
        }

        private void ReadAddresses(string line, bool onlyStorageData = false)
        {
            string streetName = string.Concat(line.TakeWhile(x => !Char.IsDigit(x)));
            string numbers = line.Substring(streetName.Length);

            streetName = streetName.Trim();
            if (string.IsNullOrWhiteSpace(streetName))
            {
                AnsiConsole.MarkupLine("[red]Eine Zeile mit Zustell-Adressen konnte nicht gelesen werden.[/]");
                return;
            }

            string[] numbersArray = numbers.Split(SEPERATORS, StringSplitOptions.RemoveEmptyEntries);

            if (numbersArray.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]Hausnummern der Straße " + streetName + " konnten nicht gelesen werden.[/]");
                return;
            }

            Street street = Street.GetOrCreate(_streets, streetName);

            if (onlyStorageData)
            {
                _orderedAddresses.Add(Tuple.Create(street, numbersArray[0], true));
                return;
            }

            if (!_mailAddresses.TryGetValue(street, out IList<string>? streetNumbers))
            {
                streetNumbers = new List<string>();
                _mailAddresses.Add(street, streetNumbers);
            }

            foreach (string num in numbersArray)
            {
                if (!Char.IsDigit(num[0]) || !Street.FullnumberToNumberId(num, out int numberId))
                {
                    AnsiConsole.MarkupLine("[red]Die Hausnummer " + num + " der Straße " + streetName + " konnten nicht gelesen werden.[/]");
                    continue;
                }

                streetNumbers.Add(num);
                _orderedAddresses.Add(Tuple.Create(street, num, false));
            }
        }

        private void ReadStorage(string line)
        {
            string countString = string.Concat(line.TakeWhile(x => !SEPERATORS.Contains(x)));
            string address = line.Substring(countString.Length).Trim();
            if (string.IsNullOrWhiteSpace(address) || !int.TryParse(string.Concat(countString.TakeWhile(Char.IsDigit)), out int count) ||
                 !Street.SplitStreetAddress(address, out string streetName, out string number))
            {
                AnsiConsole.MarkupLine("[red]Die Zeile" + line + " mit Ablagen konnte nicht gelesen werden.[/]");
                return;
            }

            Street street = Street.GetOrCreate(_streets, streetName);
            Tuple<Street, string, int> storage = Tuple.Create(street, number, count);
            _storages.Add(storage);
        }

        private void ReadDesiredCount(string line)
        {
            if (!int.TryParse(line, out int count))
            {
                AnsiConsole.MarkupLine("[red]Die ideale Ablagenzahl konnte nicht gelesen werden.[/]");
                _desiredStorageCount = 0;
                return;
            }

            _desiredStorageCount = count;
        }

        private void ReadStartEndAddress(string line, bool isStart)
        {
            if (!Street.SplitStreetAddress(line, out string streetName, out string fullNumber))
            {
                AnsiConsole.MarkupLine("[red]Die " + (isStart ? "Startadresse" : "Endadresse") + " konnte nicht gelesen werden (" + line + ").[/]");
                return;
            }

            Street street = Street.GetOrCreate(_streets, streetName);
            Tuple<Street, string> address = Tuple.Create(street, fullNumber);
            _startAndEnd[isStart ? 0 : 1] = address;
        }

        private void ReadTrafficType(string line)
        {
            string street = string.Concat(line.TakeWhile(c => !Char.IsDigit(c)));
            string value = line.Substring(street.Length);

            street = street.Trim();
            value = value.Trim();

            if (!int.TryParse(value, out int v))
            {
                AnsiConsole.MarkupLine("[DarkOrange]Die Verkehrsstärke " + line + " konnte nicht gelesen werden.[/]");
                return;
            }

            if (v < 0 || v > 4)
            {
                AnsiConsole.MarkupLine("[DarkOrange]Die Verkehrsstärke " + line + " liegt nicht zwischen 0 (sehr leicht) und 4 (sehr schwer).[/]");
                v = Math.Clamp(v, 0, 4);
            }

            Street s = Street.GetOrCreate(_streets, street);
            TrafficType type = (TrafficType)v;

            if (!_trafficTypes.TryAdd(s, type))
            {
                AnsiConsole.MarkupLine("[DarkOrange]Die Verkehsstärke der Straße "
                    + street + " wurde mehrmals angegeben. Alle Werte abseits des ersten werden ignoriert.[/]");
            }
        }

        private void ReadSearchDepth(string line)
        {
            if (!int.TryParse(line, out int d))
            {
                AnsiConsole.MarkupLine("[DarkOrange]Die Suchtiefe konnte nicht gelesen werden.[/]");
                return;
            }

            if (d < 1)
            {
                AnsiConsole.MarkupLine("[red]Die Suchtiefe muss größer als 0 sein.[/]");
                return;
            }

            _searchDepth = d;
        }

        private void ReadAlley(string line)
        {
            line = line.Trim().ToLower();
            if (line == "stopp" || line == "stop")
            {
                _doNotCrossAlleys = true;
            }
            else if (line == "erlaubt" || line == "allowed")
            {
                _doNotCrossAlleys = false;
            }
        }

        private void ReadCut(string line)
        {
            string[] parts = line.Split(":", StringSplitOptions.None);
            if (parts.Length != 2)
            {
                AnsiConsole.MarkupLine("[red]Die Verbindungs-Löschung " + line + " konnte nicht gelesen werden.[/]");
                return;
            }

            string first = parts[0];
            if (!Street.SplitStreetAddress(first, out string firstName, out string firstNumber))
            {
                AnsiConsole.MarkupLine("[red]Die erste Adresse der Verbindungs-Löschung " + line + " konnte nicht gelesen werden.[/]");
                return;
            }

            string second = parts[1];
            if (!Street.SplitStreetAddress(second, out string secondName, out string secondNumber))
            {
                AnsiConsole.MarkupLine("[red]Die zweite Adresse der Verbindungs-Löschung " + line + " konnte nicht gelesen werden[/]");
                return;
            }

            Street firstStreet = Street.GetOrCreate(_streets, firstName);
            Street secondStreet = Street.GetOrCreate(_streets, secondName);

            Tuple<Street, string, Street, string> cut = Tuple.Create(firstStreet, firstNumber, secondStreet, secondNumber);
            _cuts.Add(cut);
        }

        private void ReadAnalysis(string analysis)
        {
            analysis = analysis.Trim().ToLower();

            if (analysis == "compare" || analysis == "vergleich")
            {
                _compareAnalysis = true;
            }
            else
            {
                AnsiConsole.MarkupLine("[DarkOrange]Der Analysewunsch konnte nicht gelesen werden.[/]");
                _compareAnalysis = true;
            }
        }

        private void ReadMap(string line)
        {
            if (line.Length > 0)
            {
                _mapFile = line;
            }
        }

        private void FixInput()
        {
            foreach (var kvp in _mailAddresses.ToList())
            {
                if (kvp.Value.Count == 0)
                {
                    _mailAddresses.Remove(kvp.Key);
                    AnsiConsole.MarkupLine("[DarkOrange]Die Straße " + kvp.Key.Name +
                        " wurde als Postadresse angegeben, es wurden aber keine gültigen Hausnummern angegeben.[/]");
                }
            }

            for (int i = 0; i < 2; i++)
            {
                Tuple<Street, string>? address = _startAndEnd[i];
                if (address == null)
                {
                    continue;
                }

                if (!_mailAddresses.TryGetValue(address.Item1, out IList<string>? nums))
                {
                    nums = new List<string>();
                    _mailAddresses.Add(address.Item1, nums);
                }

                if (!nums.Contains(address.Item2))
                {
                    nums.Add(address.Item2);
                    AnsiConsole.MarkupLine("[DarkOrange]Die " + (i == 0 ? "Startadresse" : "Endadresse")
                        + " war nicht unter den zuzustellenden Postadressen und wurde dort hinzugefügt.[/]");
                }
            }

            int storageSum = _storages.Sum(s => s.Item3);
            if (_desiredStorageCount > storageSum)
            {
                _desiredStorageCount = storageSum;

                AnsiConsole.MarkupLine("[DarkOrange]Die angebende gewünschte Anzahl an zu nutzenden Ablagen" +
                    " ist größer als die Menge der angebenen Ablagen.[/]");
            }

            if (_desiredStorageCount == 0)
            {
                _storages.Clear();
            }
        }
    }
}