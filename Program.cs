//Copyright Thomas Greshake 2026

using Brieffreund;
using Spectre.Console.Cli;

CommandApp app = new();
app.SetDefaultCommand<App>();
app.Configure(config => config.SetApplicationName(Constants.NAME));
#if DEBUG
app.Configure(config => config.PropagateExceptions());
# endif
app.Run(args);