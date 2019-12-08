using Serilog;
using Serilog.Events;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

namespace Ghoplin
{
    public static class CliCommands
    {
        private static readonly Option VerbosityOption = new Option(new[] { "--verbose", "-v" }, "Verbose output") { Argument = new Argument<bool>(() => false) };
        private static readonly Option JoplinPortOption = new Option(new[] { "--port", "-p" }, "Joplin's port (found on Web Clipper's settings page)") { Argument = new Argument<int>(() => DefaultPort) };
        private static readonly Option JoplinTokenOption = new Option(new[] { "--token", "-t" }, "Joplin's token (found on Web Clipper's settings page)") { Argument = new Argument<string>(() => "") };
        private const int DefaultPort = 41184;

        public static RootCommand CreateCli()
        {
            return new RootCommand("Ghoplin - a tool for syncing Ghost blog posts into Joplin")
            {
                CreateAddCommand(),
                CreateSyncCommand(),
            };
        }

        private static Symbol CreateSyncCommand()
        {
            var syncCommand = new Command("sync", "Synchronize all blogs now")
            {
                VerbosityOption,
                JoplinPortOption,
                JoplinTokenOption,
            };
            syncCommand.Handler = CommandHandler.Create<bool, int, string>(DoSync);
            return syncCommand;
        }

        private static Command CreateAddCommand()
        {
            Command addCommand = new Command("add", "Adds a new blog to be synchronized")
                    {
                        new Option("--url", "URL of the Ghost blog's API") { Argument = new Argument<string>() },
                        new Option("--apiKey", "API key of the Ghost blog (get it in the blog's settings)") { Argument = new Argument<string>() },
                        new Option("--notebook", "The ID or name of the Joplin's notebook this blog will sync to") { Argument = new Argument<string>() },
                        new Option("--autoTags", "A comma-separated list of tags that will be applied to every synced note in Joplin") { Argument = new Argument<string>(() => "") },
                        VerbosityOption,
                        JoplinPortOption,
                        JoplinTokenOption,
                    };
            addCommand.Handler = CommandHandler.Create<string, string, string, string, bool, int, string>(DoAdd);
            return addCommand;
        }

        private static void DoAdd(string url, string apiKey, string notebook, string autoTags, bool verbose, int port, string token)
        {
            var ghoplin = ProgramSetup(verbose, port, token);
            ghoplin.AddBlog(
                apiKey,
                url,
                notebook,
                autoTags.Split(",", StringSplitOptions.RemoveEmptyEntries))
                .Wait();
        }

        private static void DoSync(bool verbose, int port, string token)
        {
            var ghoplin = ProgramSetup(verbose, port, token);
            ghoplin.Sync()
                .Wait();
        }

        private static GhoplinApi ProgramSetup(bool verbose, int port, string token)
        {
#if DEBUG
            verbose = true;
#endif
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Is(verbose ? LogEventLevel.Verbose : LogEventLevel.Warning)
                            .WriteTo.Console()
                            .CreateLogger();

            if (string.IsNullOrWhiteSpace(token))
            {
                // not provided via a parameter - check the setting file
                Log.Verbose("token not provided as a parameter. Loading token and port from file.");
                var joplinConfigFile = File.ReadAllText(".ghoplin").Split();
                token = joplinConfigFile[0];
                port = joplinConfigFile.Length > 1 ? int.Parse(joplinConfigFile[1]) : DefaultPort;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new GhoplinException("You must provide a valid Joplin API token");
            }

            var ghoplin = new GhoplinApi(
                $"http://localhost:{port}/",
                token);
            return ghoplin;
        }
    }
}