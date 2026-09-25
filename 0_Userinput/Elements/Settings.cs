//Copyright Thomas Greshake 2026

using Spectre.Console.Cli;
using System.ComponentModel;

namespace Brieffreund.Input
{
    internal class Settings : CommandSettings
    {
        [CommandArgument(0, "[bezirk]")]
        [Description("Der Name der Textdatei, in welcher der zu sortierende Bezirk entahlten ist.")]
        public string? DistrictFile { get; init; } = null;

        [CommandOption("-k|--karte")]
        [Description("Der Name der osm-Datei, in welcher eine Karte des Bezirks entahlten ist: https://www.openstreetmap.org/export.")]
        public string? OsmFile { get; init; } = null;

        [CommandOption("-s|--suchtiefe")]
        [Description("Die Suchtiefe (Menge der zu generierenden Routenkarten). " +
            "Ein guter Start ist 16, erhöhbar falls die Ablagen nicht gut verteilt werden, senkbar falls die Rechenzeit zu lang ist.")]
        public int SearchDepth { get; init; } = -1;

        [CommandOption("-a|--ablagen")]
        [Description("Die Menge an Ablagen, über die die Postmenge verteilt werden soll.")]
        public int DesiredStorageCount { get; init; } = -1;

        [CommandOption("-g|--gasse")]
        [Description("Hier kann angegeben werden, ob enge Gassen durchgangen werden sollen (erlaubt/stopp). " +
            "Standardmäßig wird dies verhindert (stopp), \"erlaubt\" verhindert diese sehr experimentelle Funktion also." +
            "Sollte die Funktion deaktiviert werden, so können enge Gassen in der Bezirksdatei mit \"Rem\" ebenfalls beseitigt werden.")]
        public string? DoNotCrossAlleys { get; init; } = null;

        [CommandOption("-v|--vergleich")]
        [Description("Hiermit kann angegeben werden, " +
            "dass die ermittelte Gangfolge mit der in der Bezirksdatei angegebenen Gangfolge verglichen werden soll." +
            "Hierzu müssen die Adressen in der Bezirks-Datei in Gangfolge angegeben sein.")]
        public bool CompareAnalysis { get; init; } = false;
    }
}