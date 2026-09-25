//Copyright Thomas Greshake 2026

using Brieffreund.Analyser;
using Brieffreund.Printer;
using Brieffreund.Printer.Functions;
using Brieffreund.Routegenerator;
using Brieffreund.Routegenerator.Functions;
using Spectre.Console;
using System.Collections.ObjectModel;

namespace Brieffreund
{
    internal class Route
    {
        private readonly List<Address> _addresses;
        internal readonly ReadOnlyCollection<Address> Addresses;

        internal static Route Generate(IUserInput input, List<RouteMap> maps)
        {
            RouteGeneratorManager manager = new(input, maps);
            AnsiConsole.Status().Start("Eine Route wird gesucht...", ctx =>
            {
                manager.FindBestRoute();
            });
            AnsiConsole.MarkupLine("[green]Eine Route wurde gefunden![/]");
            AnsiConsole.MarkupLine("Anzahl valider Restrouten: " + manager.Count.ToString()
                + ", totale Anzahl an Zweigrouten: " + manager.TreeCount.ToString());
            AnsiConsole.MarkupLine("Die beste Route hatte eine Tiefe von " + manager.FinalLeafIndex.ToString()
                + ", bei einer maximalen Tiefe von " + maps.Count.ToString());
            AnsiConsole.WriteLine();

            RouteLeaf? finalLeaf = manager.FinalLeaf;
            if (finalLeaf == null)
            {
                throw new Exception();
            }

            RouteReader routeReader = new RouteReader(input, finalLeaf);

            IAddressGetter getter = new AddressGetter(routeReader);
            IAddressSorter sorter = new AddressSorter();

            List<Address> addresses = getter.GetAddresses();
            addresses = sorter.Sort(addresses);
            Route route = new Route(addresses);
            return route;
        }

        private Route(List<Address> addresses)
        {
            _addresses = addresses;
            Addresses = _addresses.AsReadOnly();
        }

        internal string PrintRoute()
        {
            IRouteToStringConverter converter = new RouteToStringConverter();
            string routeString = converter.ConvertRouteToString(_addresses);

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(routeString);
            AnsiConsole.WriteLine();

            return routeString;
        }

        internal bool WriteRoute(IUserInput input, string routeString)
        {
            bool success = true;

            string name = AnsiConsole.Ask<string>("Geben Sie den Namen der Datei an, in welcher die Route gespeichert werden soll, " +
                "oder drücken Sie \"Enter\", um die Route zu verwerfen.", "NULL");
            if (name == "NULL")
            {
                return false;
            }

            name += ".txt";

            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            path += "\\" + Constants.NAME;
            Directory.CreateDirectory(path);

            path = Path.Combine(path, name);

            try
            {
                using (StreamWriter writer = new StreamWriter(path))
                {
                    writer.Write(routeString);
                }
            }
            catch (Exception e)
            {
                if (e is UnauthorizedAccessException)
                {
                    AnsiConsole.MarkupLine("[red]Das Programm hat keinen Zugriff auf den Speicherort.[/]");
                }
                else if (e is DirectoryNotFoundException)
                {
                    AnsiConsole.MarkupLine("[red]Das Programm kann den Speicherort nicht finden.[/]");
                }
                else
                {
                    AnsiConsole.WriteException(e, ExceptionFormats.NoStackTrace);
                }

                success = false;
            }

            if (success)
            {
                AnsiConsole.MarkupLine("[green]Eine Kopie der fertigen Route ist in der Text-Datei unter \""
                    + path + "\" zu finden.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Die Route konnte leider nicht gespeichert werden.[/]");
            }

            return success;
        }

        internal void AnalyseRoute(IUserInput input, AnalyserMap map)
        {
            RouteAnalysis analysis = RouteAnalysis.Create(input, map, _addresses);
            if (input.CompareAnalysis)
            {
                RouteAnalysis original = RouteAnalysis.Create(input, map);
                PrintComparison(original, analysis, input.DesiredStorageCount > 0);
            }
            else
            {
                PrintResult(analysis, input.DesiredStorageCount > 0);
            }
        }

        private const string STREETLENGTH_EXPLANATION =
            "\"Straßenlänge\" ist die Länge der Route ohne Straßenüberquerungen.\n" +
            "\"Überquerungsgewicht\" ist eine Kombination aus der Breite und Verkehrsstärke der überquerten Straßen.\n" +
            "\"Gesamt\" ist die Summe der beiden Werte und gibt an, wie schwer die Route zu durchlaufen ist.\n";

        private const string WRONGWAY_EXPLANATION =
            "\"Falsche Straßenseite\" ist die Länge der Strecke, bei der auf der falschen Straßenseite zugestellt wird.\n" +
            "\"Gegen Einbahnstraße\" ist die Länge der Strecke, die entgegen einer Einbahnstraße zugestellt wird.\n" +
            "Ob diese Werte ein Problem darstellen, hängt stark von den lokalen Begebenheiten ab.\n";

        private const string STANDARD_DEVIATION_EXPLANATION =
            "\"Standardabweichung\" ist die durchschnittliche Abweichung der Postmenge je Ablage vom Zielwert, dividiert durch den Zielwert. " +
            "Dieser Wert misst wie gut die Ablagen verteilt sind, kleinere Werte sind besser.\n" +
            "\"Positive Abweichung\" ist die maximale Abweichung nach oben (Je höher, desto voller ist die größte Ablage).\n" +
            "\"Negative Abweichung\" ist die maximale Abweichung nach unten (Je höher, desto leerer ist die kleinste Ablage).";

        private const string POS_DEVIATION_EXPLANATION = "[red]Eine hohe positive Abweichung kann ein Hinweis darauf sein, dass die Suchtiefe zu klein ist. " +
            "Sollte hingegen die Ablagenlage im Bezirk sehr schlecht sein, so kann das Programm möglicherweise auch keine gute Route finden.[/]";

        private const string CROSSINGS_EXPLANATION =
            "Dies ist eine Aufzählung, wie oft Straßen mit den jeweiligen Verkehrsstärken überquert werden müssen. Das obige Überquerungsgewicht " +
            "setzt sich zum Teil aus diesen Werten zusammen.";

        private void PrintComparison(RouteAnalysis original, RouteAnalysis analysis, bool printStorageData)
        {
            AnsiConsole.MarkupLine("Die erstellte Route wird mit der alten, eingelesenen Route verglichen:");
            var table = new Table();
            table.AddColumns("Streckenlänge", "Alte Route", "Erstellte Route", "Differenz");
            table.AddRow("Straßenlänge (m)", original.StreetLength.ToString(), analysis.StreetLength.ToString(),
                DiffToString(original.StreetLength, analysis.StreetLength, true));
            table.AddRow("Überquerungsgewicht", original.CrossingScore.ToString(), analysis.CrossingScore.ToString(),
                DiffToString(original.CrossingScore, analysis.CrossingScore, true));
            table.AddRow("Gesamt", original.LengthScore.ToString(), analysis.LengthScore.ToString(),
                DiffToString(original.LengthScore, analysis.LengthScore, true));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(STREETLENGTH_EXPLANATION);

            table = new Table();
            table.AddColumns("Sonstige Streckenlängen", "Alte Route", "Erstellte Route", "Differenz");
            table.AddRow("Falsche Straßenseite (m)", original.WrongSide.ToString(), analysis.WrongSide.ToString(),
                DiffToString(original.WrongSide, analysis.WrongSide, true));
            table.AddRow("Gegen Einbahnstraße (m)", original.WrongWay.ToString(), analysis.WrongWay.ToString(),
                DiffToString(original.WrongWay, analysis.WrongWay, true));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(WRONGWAY_EXPLANATION);

            if (printStorageData)
            {
                table = new Table();
                table.AddColumns("Postverteilung", "Alte Route", "Erstellte Route", "Differenz");
                table.AddRow("Ablagenanzahl", original.StorageCount.ToString(), analysis.StorageCount.ToString(), "-");
                table.AddRow("Standardabweichung (%)", original.StandardDeviation.ToString(), analysis.StandardDeviation.ToString(),
                    DiffToString(original.StandardDeviation, analysis.StandardDeviation));
                table.AddRow("Max. positive Abweichung (%)", original.PosDeviation.ToString(), analysis.PosDeviation.ToString(),
                    DiffToString(original.PosDeviation, analysis.PosDeviation));
                table.AddRow("Max. negative Abweichung (%)", original.NegDeviation.ToString(), analysis.NegDeviation.ToString(),
                    DiffToString(original.NegDeviation, analysis.NegDeviation));
                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine(STANDARD_DEVIATION_EXPLANATION);
                if (HasBadPosVariation(analysis.PosDeviation))
                {
                    AnsiConsole.MarkupLine(POS_DEVIATION_EXPLANATION);
                }
                AnsiConsole.WriteLine();
            }

            table = new Table();
            table.AddColumns("Überquerungen / Verkehrsstärke", "Alte Route", "Erstellte Route", "Differenz");
            table.AddRow("Sehr schwer", original.VeryHeavyCrossings.ToString(), analysis.VeryHeavyCrossings.ToString(),
                DiffToString(original.VeryHeavyCrossings, analysis.VeryHeavyCrossings));
            table.AddRow("Schwer", original.HeavyCrossings.ToString(), analysis.HeavyCrossings.ToString(),
                DiffToString(original.HeavyCrossings, analysis.HeavyCrossings));
            table.AddRow("Normal", original.MediumCrossings.ToString(), analysis.MediumCrossings.ToString(),
                DiffToString(original.MediumCrossings, analysis.MediumCrossings));
            table.AddRow("Leicht", original.LightCrossings.ToString(), analysis.LightCrossings.ToString(),
                DiffToString(original.LightCrossings, analysis.LightCrossings));
            table.AddRow("Sehr leicht", original.VeryLightCrossings.ToString(), analysis.VeryLightCrossings.ToString(),
                DiffToString(original.VeryLightCrossings, analysis.VeryLightCrossings));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(CROSSINGS_EXPLANATION);

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();
        }

        private string DiffToString(int a, int b, bool displayPerc = false, bool lesserIsBetter = true)
        {
            int diff = b - a;
            if (diff == 0)
            {
                return diff.ToString();
            }

            bool green = diff < 0 == lesserIsBetter;
            string line = (green ? "[green]" : "[red]") + (diff < 0 ? "-" : "+") + Math.Abs(diff).ToString() + "[/]";

            if (displayPerc)
            {
                int perc = (int)Math.Round(100 * (float)diff / a);
                line += " (" + (green ? "[green]" : "[red]") + (diff < 0 ? "-" : "+") + Math.Abs(perc).ToString() + "%[/])";
            }

            return line;
        }

        private void PrintResult(RouteAnalysis analysis, bool printStorageData)
        {
            AnsiConsole.MarkupLine("Ein paar Daten zu der erstellten Route:");
            var table = new Table();
            table.AddColumns("Streckenlänge", "Wert");
            table.AddRow("Straßenlänge (m)", analysis.StreetLength.ToString());
            table.AddRow("Überquerungsgewicht", analysis.CrossingScore.ToString());
            table.AddRow("Gesamt", analysis.LengthScore.ToString());
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(STREETLENGTH_EXPLANATION);

            table = new Table();
            table.AddColumns("Sonstige Streckenlängen", "Wert");
            table.AddRow("Falsche Straßenseite (m)", analysis.WrongSide.ToString());
            table.AddRow("Gegen Einbahnstraße (m)", analysis.WrongWay.ToString());
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(WRONGWAY_EXPLANATION);

            if (printStorageData)
            {
                table = new Table();
                table.AddColumns("Postverteilung", "Wert");
                table.AddRow("Ablagenanzahl", analysis.StorageCount.ToString());
                table.AddRow("Standardabweichung (%)", analysis.StandardDeviation.ToString());
                table.AddRow("Max. positive Abweichung (%)", analysis.PosDeviation.ToString());
                table.AddRow("Max. negative Abweichung (%)", analysis.NegDeviation.ToString());
                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine(STANDARD_DEVIATION_EXPLANATION);
                if (HasBadPosVariation(analysis.PosDeviation))
                {
                    AnsiConsole.MarkupLine(POS_DEVIATION_EXPLANATION);
                }
                AnsiConsole.WriteLine();
            }

            table = new Table();
            table.AddColumns("Überquerungen / Verkehrsstärke", "Anzahl");
            table.AddRow("Sehr schwer", analysis.VeryHeavyCrossings.ToString());
            table.AddRow("Schwer", analysis.HeavyCrossings.ToString());
            table.AddRow("Normal", analysis.MediumCrossings.ToString());
            table.AddRow("Leicht", analysis.LightCrossings.ToString());
            table.AddRow("Sehr leicht", analysis.VeryLightCrossings.ToString());
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(CROSSINGS_EXPLANATION);

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();
        }

        private bool HasBadPosVariation(int posVariation) => 1f + posVariation / 100f > Constants.INACCEPTABLE_MULTIPLIER + 0.1f;
    }
}