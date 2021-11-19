using System;
using System.CommandLine;

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