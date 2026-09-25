//Copyright Thomas Greshake 2026

using Brieffreund.Analyser;
using Brieffreund.Input;
using Brieffreund.Streetmap;
using Brieffreund.Streetmap.Osm;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Brieffreund
{
    internal class App : Command<Settings>
    {
        public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var panel = new Panel("[yellow1 bold]" + Constants.NAME + "[/]").BorderColor(Color.Yellow1);
            AnsiConsole.Write(panel);
            Rule rule = new Rule("[yellow1]Relevante Daten werden gesammelt[/]").LeftJustified();
            AnsiConsole.Write(rule);

            IUserInput input = UserInput.GetInput(settings);
            if (!input.Success) return -1;
            IExternalMap externalMap = OsmMap.GetMap(input, settings);
            if (!externalMap.Success) return -2;

            rule = new Rule("[yellow1]Die Straßenkarte wird erstellt[/]").LeftJustified();
            AnsiConsole.Write(rule);
            IStreetMap streetMap = StreetMap.Create(input, externalMap, out AnalyserMap analyserMap);
            if (!streetMap.Success) return -3;

            rule = new Rule("[yellow1]Die Routenkarten werden erstellt[/]").LeftJustified();
            AnsiConsole.Write(rule);
            int searchDepth = input.SearchDepth;
            List<IEulerMap> eulerMaps = EulerMap.Generate(streetMap, searchDepth);
            List<RouteMap> routeMaps = RouteMap.Generate(input, eulerMaps);

            rule = new Rule("[yellow1]Eine Route wird bestimmt[/]").LeftJustified();
            AnsiConsole.Write(rule);
            Route route = Route.Generate(input, routeMaps);
            route.AnalyseRoute(input, analyserMap);

            rule = new Rule("[yellow1]Die Route verläuft wie folgt[/]").LeftJustified();
            AnsiConsole.Write(rule);
            string routeString = route.PrintRoute();
            route.WriteRoute(input, routeString);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Drücken Sie die Eingabetaste, um das Programm zu beenden.");
            Console.ReadLine();

            return 0;
        }
    }
}