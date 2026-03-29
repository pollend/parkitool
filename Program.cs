using System;
using System.Collections.Generic;
using CommandLine;

namespace Parkitool
{
    [Verb("workspace", HelpText = "Depreciate! Configure project workspace by downloading Parkitect assemblies and update csproj.")]
    public class WorkspaceOptions
    {
        [Option('u', "username", Required = true, HelpText = "Steam Username")]
        public String SteamUsername { get; set; }

        [Option('p', "password", Required = true, HelpText = "Steam Password")]
        public String SteamPassword { get; set; }

        [Option('c', "project", HelpText = "Directory for Project Path", Default = "./")]
        public String Path { get; set; }

        [Option('o', "output", HelpText = "Directory for output Mod", Default = "./bin")]
        public String Output { get; set; }
    }

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

    [Verb("install", HelpText = "Install mod into local Parkitect.")]
    public class InstallOptions
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
            
            return Parser.Default.ParseArguments<WorkspaceOptions,UploadOptions,InitOptions, InstallOptions>(args).MapResult(
                (WorkspaceOptions opts) => CommandLineActions.SetupWorkspaceOption(opts),
                (UploadOptions ops) => CommandLineActions.UploadOptions(ops),
                (InitOptions ops) => CommandLineActions.InitOptions(ops),
                (InstallOptions opts) => CommandLineActions.InstallOptions(opts), errs => 1);
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            //handle errors
        }

    }
}
