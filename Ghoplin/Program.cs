using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using Terminal.Gui;

namespace Ghoplin
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();
            
            var joplinConfig = File.ReadAllText(".ghoplin").Split();
            var token = joplinConfig[0];
            var port = joplinConfig.Length > 1 ? int.Parse(joplinConfig[1]) : 41184;
            var ghoplin = new GhoplinApi(
                $"http://localhost:{port}/",
                token);
            ghoplin.Sync()
                .Wait();
            //Gui();
        }

        private static void Gui()
        {
            Application.Init();
            var top = Application.Top;

            // Creates the top-level window to show
            var win = new Window("MyApp")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu

                // By using Dim.Fill(), it will automatically resize without manual intervention
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            top.Add(win);

            // Creates a menubar, the item "New" has a help menu.
            var menu = new MenuBar(new MenuBarItem[] {
            new MenuBarItem ("_File", new MenuItem [] {
                new MenuItem ("_New", "Creates new file", () => Console.WriteLine("asd")),
                new MenuItem ("_Close", "", () => Console.WriteLine("asd")),
                new MenuItem ("_Quit", "", () => { if (true) top.Running = false; })
            }),
            new MenuBarItem ("_Edit", new MenuItem [] {
                new MenuItem ("_Copy", "", null),
                new MenuItem ("C_ut", "", null),
                new MenuItem ("_Paste", "", null)
            })
        });
            top.Add(menu);

            var login = new Label("Login: ") { X = 3, Y = 2 };
            var password = new Label("Password: ")
            {
                X = Pos.Left(login),
                Y = Pos.Top(login) + 1
            };
            var loginText = new TextField("")
            {
                X = Pos.Right(password),
                Y = Pos.Top(login),
                Width = 40
            };
            var passText = new TextField("")
            {
                Secret = true,
                X = Pos.Left(loginText),
                Y = Pos.Top(password),
                Width = Dim.Width(loginText)
            };

            // Add some controls, 
            win.Add(
                // The ones with my favorite layout system
                login, password, loginText, passText,

                    // The ones laid out like an australopithecus, with absolute positions:
                    new CheckBox(3, 6, "Remember me"),
                    new RadioGroup(3, 8, new[] { "_Personal", "_Company" }),
                    new Button(3, 14, "Ok"),
                    new Button(10, 14, "Cancel"),
                    new Label(3, 18, "Press F9 or ESC plus 9 to activate the menubar"));

            Application.Run();
        }
    }
}
