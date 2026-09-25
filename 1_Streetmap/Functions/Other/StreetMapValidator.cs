//Copyright Thomas Greshake 2026

using Spectre.Console;

namespace Brieffreund.Streetmap
{
    internal interface IStreetMapValidator
    {
        public bool Validate();
    }
}

namespace Brieffreund.Streetmap.Functions
{
    internal class StreetMapValidator : IStreetMapValidator
    {
        private readonly IStreetMap _map;

        internal StreetMapValidator(IStreetMap map)
        {
            _map = map;
        }

        public bool Validate()
        {
            if (!_map.Success)
            {
                return false;
            }

            if (_map.Paths.Any(p => p.Segment == null || p.Segment.IsActive != p.IsActive))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte 1[/]");
                return false;
            }

            if (_map.Segments.All(s => !s.ReceivesMail))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  2[/]");
                return false;
            }

            if (_map.Segments.Any(s => !s.To.Segments.Contains(s) || !s.From.Segments.Contains(s)))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  3[/]");
                return false;
            }

            if (_map.Segments.Any(s => s.Paths.Any(p => p.Segment != s)))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  4[/]");
                return false;
            }

            if (_map.Intersections.Values.Any(i => i.Segments.Any(s => s.From != i && s.To != i)))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  5[/]");
                return false;
            }

            if (_map.Segments.Any(s => !s.IsActive))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  6[/]");
                return false;
            }

            if (_map.Intersections.Values.Any(i => !i.IsActive) || _map.InactiveIntersections.Values.Any(i => i.IsActive))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  7[/]");
                return false;
            }

            if (_map.Intersections.Values.Any(i => i.Segments.All(s => !s.IsActive)) ||
                _map.Nodes.Any(n => n.Paths.All(p => !p.IsActive)) ||
                _map.InactiveIntersections.Values.Any(i => i.Segments.Any(s => s.IsActive)) ||
                _map.InactiveNodes.Any(i => i.Paths.Any(p => p.IsActive)))
            {
                AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  8[/]");
                return false;
            }

            for (int i = 0; i < _map.Storages.Count; i++)
            {
                if (_map.Storages[i].Index != i)
                {
                    AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  9[/]");
                    return false;
                }
            }

            for (int i = 0; i < _map.Segments.Count; i++)
            {
                if (_map.Segments[i].Index != i)
                {
                    AnsiConsole.MarkupLine("[red]ERROR: Straßenkarte  10[/]");
                    return false;
                }
            }

            return true;
        }
    }
}