using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Ghoplin
{
    public static class CliCommands
    {
        private static readonly Option VerbosityOption = new Option(new[] { "--verbose", "-v" }, "Verbose output") { Argument = new Argument<bool>(() => false) };
        private static readonly Option JoplinPortOption = new Option(new[] { "--port", "-p" }, "Joplin's port (found on Web Clipper's settings page)") { Argument = new Argument<int>(() => DefaultPort) };
        private static readonly Option JoplinTokenOption = new Option(new[] { "--token", "-t" }, "Joplin's token (found on Web Clipper's settings page)") { Argument = new Argument<string>(() => "") };
        private const int DefaultPort = 41184;
        private const string ConfigFileName = ".ghoplin";
        private static string ConfigPath = ConfigFileName;

        public static RootCommand CreateCli()
        {
            ConfigPath = Path.Join(
                Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName),
                ConfigFileName);
            return new RootCommand("Ghoplin - a tool for syncing Ghost blog posts into Joplin")
            {
                CreateAddCommand(),
                CreateSyncCommand(),
                CreateConfigCommand(),
            };
        }

        private static Symbol CreateConfigCommand()
        {
            var configCommand = new Command("write-config", "Write your Joplin token and port to a .ghoplin file (so you don't have to enter them on each call)")
            {
                VerbosityOption,
                JoplinPortOption,
                JoplinTokenOption,
            };
            configCommand.Handler = CommandHandler.Create<bool, int, string>(DoConfig);
            return configCommand;
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
                        new Option(new[] {"--url", "-u" }, "URL of the Ghost blog's API") { Argument = new Argument<string>() },
                        new Option(new[] {"--apiKey", "-k" }, "API key of the Ghost blog (get it in the blog's settings)") { Argument = new Argument<string>() },
                        new Option(new[] {"--notebook", "-n" }, "The ID or name of the Joplin's notebook this blog will sync to") { Argument = new Argument<string>() },
                        new Option(new[] {"--auto-tags" }, "A comma-separated list of tags that will be applied to every synced note in Joplin") { Argument = new Argument<string>(() => "") },
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

        private static void DoConfig(bool verbose, int port, string token)
        {
            SetupLogger(verbose);
            if (string.IsNullOrWhiteSpace(token))
            {
                Log.Fatal("You must provide a valid Joplin API token to be written into the config file.");
                Environment.Exit(1);
            }
            if (File.Exists(ConfigPath))
            {
                Log.Warning("The existing .ghoplin file will be overwritten.");
                File.Delete(ConfigPath);
            }
            var config = new StringBuilder(token);
            if (port != DefaultPort)
            {
                config.AppendLine();
                config.Append(port);
            }
            File.WriteAllTextAsync(ConfigPath, config.ToString());
            Log.Information("Config written to .ghoplin");
        }

        private static GhoplinApi ProgramSetup(bool verbose, int port, string token)
        {
            SetupLogger(verbose);

            if (string.IsNullOrWhiteSpace(token))
            {
                // not provided via a parameter - check the setting file

                Log.Verbose("Token not provided as a parameter. Trying to load token and port from file {path}", ConfigPath);
                if (!File.Exists(ConfigPath))
                {
                    Log.Fatal("You must provide a valid Joplin API token - via a parameter or a .ghoplin file.");
                    Environment.Exit(1);
                }
                var joplinConfigFile = File.ReadAllText(ConfigPath).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                token = joplinConfigFile[0];
                port = joplinConfigFile.Length > 1 ? int.Parse(joplinConfigFile[1]) : DefaultPort;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                Log.Fatal("You must provide a valid Joplin API token - via a parameter or a .ghoplin file.");
                Environment.Exit(1);
            }

            var ghoplin = new GhoplinApi(
                $"http://localhost:{port}/",
                token);
            return ghoplin;
        }

        private static void SetupLogger(bool verbose)
        {
            //#if DEBUG
            //            verbose = true;
            //#endif
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Is(verbose ? LogEventLevel.Verbose : LogEventLevel.Information)
                            .WriteTo.Console()
                            .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Error)
                            .CreateLogger();
        }
    }
}