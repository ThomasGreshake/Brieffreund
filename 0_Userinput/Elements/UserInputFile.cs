//Copyright Thomas Greshake 2026

using Spectre.Console;

namespace Brieffreund.Input
{
    internal class UserInputFile
    {
        //Static -------------------------------------------------------------

        private static readonly char[] SEPERATORS = new char[] { ' ', '\t', ';', ',' };

        //Data -------------------------------------------------------------
        private readonly IList<Tuple<string, string>> _data = new List<Tuple<string, string>>();

        internal IList<Tuple<string, string>> Data => _data;

        private bool _success = true;
        internal bool Success => _success;

        //Data -------------------------------------------------------------
        internal UserInputFile(string districtFileName)
        {
            try
            {
                using (StreamReader reader = new StreamReader(districtFileName))
                {
                    ReadData(reader);
                }

                if (_data.Count == 0)
                {
                    _success = false;

                    AnsiConsole.MarkupLine("[red]Die Datei enthält keine relevanten Daten.[/]");
                }
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    AnsiConsole.MarkupLine("[red]Es wurden keine Bezirks-Daten mit dem Namen " + districtFileName + " gefunden.[/]");
                }
                else
                {
                    AnsiConsole.WriteException(e, ExceptionFormats.NoStackTrace);
                }

                _success = false;
            }
        }

        private void ReadData(StreamReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string key = string.Concat(line.TakeWhile(x => !SEPERATORS.Contains(x))).ToLower();
                key = key.Trim();

                if (key == Constants.COMMENT_KEY)
                {
                    continue;
                }

                string value = line.Substring(key.Length).Trim();
                value = value.Trim();

                _data.Add(Tuple.Create(key, value));
            }
        }
    }
}