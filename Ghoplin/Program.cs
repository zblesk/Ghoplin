using System;
using System.CommandLine.Invocation;

namespace Ghoplin
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var command = CliCommands.CreateCli();
            command.InvokeAsync(args).Wait();
        }
    }
}