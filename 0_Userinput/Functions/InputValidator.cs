//Copyright Thomas Greshake 2026

using Spectre.Console;

namespace Brieffreund.Input
{
    internal interface IInputValidator
    {
        public bool Validate();
    }
}

namespace Brieffreund.Input.Functions
{
    internal class InputValidator : IInputValidator
    {
        private readonly IUserInput _input;

        internal InputValidator(IUserInput input)
        {
            _input = input;
        }

        public bool Validate()
        {
            if (!_input.Success)
            {
                return false;
            }

            if (_input.PostCodes.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Es wurden keine Postleitzahlen angegeben.[/]");
                return false;
            }

            if (_input.PostCodes.Contains(68159) || _input.PostCodes.Contains(68161))
            {
                AnsiConsole.MarkupLine("[red]Die Mannheimer Quadrate werden nicht unterstützt![/]");
                return false;
            }

            if (_input.MailAddresses.Sum(s => s.Value.Count) == 0)
            {
                AnsiConsole.MarkupLine("[red]Es wurden keine zuzustellende Adressen angegeben.[/]");
                return false;
            }

            if (_input.Cities.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Es wurden keine Städte angegeben.[/]");
                return false;
            }

            for (int i = 0; i < 2; i++)
            {
                string addressName = i == 0 ? "Startadresse" : "Endadresse";
                Tuple<Street, string>? address = _input.StartAndEnd[i];
                if (address == null)
                {
                    AnsiConsole.MarkupLine("[DarkOrange]Es wurde keine " + addressName + " angegeben. " +
                        "Das Programm wird nach einer " + addressName + " suchen, dieses Feature ist allerdings experimentell.[/]");
                }
            }

            return true;
        }
    }
}