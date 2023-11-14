using System;
using System.CommandLine;
using System.Threading.Tasks;

using NuGetUpdater.Cli.Commands;

namespace NuGetUpdater.Cli;

internal sealed class Program
{
    internal static async Task<int> Main(string[] args)
    {
        var exitCode = 0;
        Action<int> setExitCode = (int code) => exitCode = code;

        var command = new RootCommand()
        {
            FrameworkCheckCommand.GetCommand(setExitCode),
            UpdateCommand.GetCommand(setExitCode),
        };
        command.TreatUnmatchedTokensAsErrors = true;

        var result = await command.InvokeAsync(args);

        return exitCode;
    }
}