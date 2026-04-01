using System;
using System.Collections.Generic;
using CommandLine;

namespace Parkitool
{

    [Verb("upload", HelpText = "Upload a parkitect mod to steam. TODO: not working ")]
    public class UploadOptions
    {

        [Option('u', "username", HelpText = "Steam Username")]
        public String SteamUsername { get; set; }

        [Option('p', "password", HelpText = "Steam Password")]
        public String SteamPassword { get; set; }

    }

    [Verb("init", HelpText = "Configure folder ")]
    public class InitOptions
    {
        [Option('n', "name", HelpText = "Project Name")]
        public String Name { get; set; }
    }

    [Verb("workspace", HelpText = "Install mod into local Parkitect.")]
    public class WorkspaceOptions
    {
        [Option('u', "username", HelpText = "Steam Username")]
        public String SteamUsername { get; set; }

        [Option('p', "password", HelpText = "Steam Password")]
        public String SteamPassword { get; set; }

        [Option("path", HelpText = "Parkitect Path")]
        public String ParkitectPath { get; set; }
    }

    class Program
    {
        static int Main(string[] args)
        {
            AccountSettingsStore.LoadFromFile("account.config");
            
            return Parser.Default.ParseArguments<UploadOptions,InitOptions, WorkspaceOptions>(args).MapResult(
                (UploadOptions ops) => CommandLineActions.UploadOptions(ops),
                (InitOptions ops) => CommandLineActions.InitOptions(ops),
                (WorkspaceOptions opts) => CommandLineActions.Workspace(opts), errs => 1);
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            //handle errors
        }

    }
}
