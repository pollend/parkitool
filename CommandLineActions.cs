using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace Parkitool
{
    public class CommandLineActions
    {

        static void UpdateProjectHintsAndOutput(String projectPath, String assemblyPath, String output)
        {

            String csprojFile = null;
            foreach (var file in Directory.GetFiles(projectPath))
            {
                if (file.EndsWith(".csproj"))
                {
                    csprojFile = file;
                    break;
                }
            }

            XmlDocument document = new XmlDocument();
            Console.WriteLine("Opening csproj: {0}", Path.Combine(projectPath, csprojFile));
            document.Load(Path.Combine(projectPath, csprojFile));

            var manager = new XmlNamespaceManager(document.NameTable);
            manager.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

            var toReference = document.SelectNodes("//x:Reference", manager)
                .Cast<XmlNode>();

            List<String> parkitectAssemblies = new List<string>();
            foreach (var file in Directory.GetFiles(assemblyPath))
            {
                if (file.EndsWith(".dll"))
                {
                    parkitectAssemblies.Add(Path.GetFileName(file).Replace(".dll", "").Trim());
                }
            }

            foreach (var node in toReference)
            {
                var inc = node.Attributes["Include"];
                String targetAssembly = inc.Value.Split(',').FirstOrDefault();

                if (targetAssembly.StartsWith("System"))
                {
                    continue;
                }

                if (parkitectAssemblies.Contains(targetAssembly))
                {
                    var isPrivate = node.SelectNodes(".//x:Private", manager);
                    if (isPrivate.Count == 0)
                    {
                        var privateNode = document.CreateElement("Private",
                            "http://schemas.microsoft.com/developer/msbuild/2003");
                        privateNode.InnerText = "False";

                        node.AppendChild(privateNode);
                    }
                    else
                    {
                        foreach (var prv in isPrivate.Cast<XmlNode>())
                        {
                            prv.InnerText = "False";
                        }
                    }

                    String localAssemblyPath = Path.Join(assemblyPath, targetAssembly + ".dll");

                    var hints = node.SelectNodes(".//x:HintPath", manager);
                    if (hints.Count > 0)
                    {
                        bool foundAssembly = false;
                        foreach (var hint in hints.Cast<XmlNode>())
                        {
                            foundAssembly = true;
                            Console.WriteLine("Found Existing Assembly {0} -- Updating path: {1} to {2}",
                                targetAssembly, hint.InnerText, localAssemblyPath);
                            hint.InnerText = localAssemblyPath;
                            break;
                        }

                    }
                    else
                    {
                        var hint = document.CreateElement("HintPath",
                            "http://schemas.microsoft.com/developer/msbuild/2003");
                        hint.InnerText = localAssemblyPath;

                        Console.WriteLine("New Hint Assembly {0} -- path: {1}", targetAssembly, hint.InnerText);
                        node.AppendChild(hint);
                    }
                }
            }

            foreach (var node in document.SelectNodes("//x:OutputPath", manager).Cast<XmlNode>())
            {
                node.InnerText = output;
            }

            document.Save(Path.Join(projectPath, csprojFile));
        }

        public static int InstallOptions(InstallOptions options)
        {
            ParkitectConfiguration configuration = JsonConvert.DeserializeObject<ParkitectConfiguration>(
                File.ReadAllText("./" + Constants.PARKITECT_CONFIG_FILE));
            if (String.IsNullOrEmpty(configuration.Name))
            {
                Console.WriteLine("name not set exiting");
                Environment.Exit(0);
            }

            DepotDownloader downloader = new DepotDownloader();
            String gamePath = Path.Join(Constants.HIDDEN_FOLDER, "Game");
            if (!String.IsNullOrEmpty(options.SteamUsername) && !String.IsNullOrEmpty(options.SteamPassword))
            {
                if (downloader.Login(options.SteamUsername, options.SteamPassword))
                {
                    Console.WriteLine("Download Parkitect ...");
                    downloader.DownloadDepot(gamePath, 453090, 453094, "public",
                        s => s.EndsWith(".dll")).Wait();
                }
                else
                {
                    Console.WriteLine("Failed to Login");
                    Environment.Exit(0);
                }
            }
            else if (!String.IsNullOrEmpty(options.ParkitectPath))
            {
                gamePath = options.ParkitectPath;
            }

            Console.WriteLine("Setup Project ...");
            Project project = new Project();

            var assemblyMatcher = new Matcher();
            assemblyMatcher.AddInclude(Path.Join(gamePath, Constants.PARKITECT_ASSEMBLY_PATH) + "/*.dll");
            if (configuration.Include != null ){
                foreach (var inc in configuration.Include)
                {
                    assemblyMatcher.AddInclude(inc);
                }
            }

            HashSet<String> additionalTargets = new HashSet<String>(configuration.AdditionalAssemblies);
            HashSet<String> targets = new HashSet<String>(configuration.Assemblies);
            foreach (var additionalTarget in additionalTargets) {
                targets.Add(additionalTarget);
            }

            foreach (var file in assemblyMatcher.GetResultsInFullPath("./"))
            {
                if(!file.EndsWith(".dll"))
                    continue;

                var asmb = Path.GetFileNameWithoutExtension(file);
                // include netstandard for build deps
                if (asmb.StartsWith("netstandard"))
                {
                    targets.Remove(asmb);
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(file);
                    Assembly asm = Assembly.LoadFrom(file);
                 
                    project.Assemblies.Add(new Project.AssemblyInfo
                    {
                        Name = asmb,
                        HintPath = file,
                        Version = versionInfo.ProductVersion,
                        Culture = "neutral",
                        PublicKeyToken = asm.FullName,
                        IsPrivate = additionalTargets.Contains(asmb)
                    });
                    Console.WriteLine(
                        $"Resolved Assembly: {asmb} -- {file}");
                    continue;
                }
                
                if (targets.Contains(asmb))
                {
                    targets.Remove(asmb);
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(file);
                    if (Constants.SYSTEM_ASSEMBLIES.Contains(asmb) || asmb.StartsWith("System") || asmb.StartsWith("Mono"))
                    {
                        project.Assemblies.Add(new Project.AssemblyInfo
                        {
                            Name = asmb
                        });
                        Console.WriteLine(
                            $"System Assembly {asmb} -- {file}");
                    }
                    else
                    {
                        project.Assemblies.Add(new Project.AssemblyInfo
                        {
                            Name = asmb,
                            HintPath = file,
                            Version = versionInfo.ProductVersion,
                            Culture = "neutral",
                            PublicKeyToken = "null",
                            IsPrivate = additionalTargets.Contains(asmb)
                        });
                        Console.WriteLine(
                            $"Resolved Assembly: {asmb} -- {file}");
                    }
                }
            }

            foreach (var tg in targets)
            {
                if (Constants.SYSTEM_ASSEMBLIES.Contains(tg) || tg.StartsWith("System") || tg.StartsWith("Mono"))
                {
                    project.Assemblies.Add(new Project.AssemblyInfo
                    {
                        Name = tg
                    });
                    Console.WriteLine(
                        $"System Assembly {tg}");
                }
                else
                {
                    Console.WriteLine(
                        $"Unresolved Assembly -- {tg}");
                }
            }

            // String assemblyPath = Path.Join(gamePath, Constants.PARKITECT_ASSEMBLY_PATH);
            // foreach (var asmb in configuration.Assemblies)
            // {
            //     if (Directory.Exists(assemblyPath))
            //     {
            //         String lowerAsmb = asmb.ToLower();
            //         String parkitectAssemblyPath = Path.Join(assemblyPath, $"{asmb}.dll");
            //         FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(parkitectAssemblyPath);
            //         if (File.Exists(parkitectAssemblyPath))
            //         {
            //             if (lowerAsmb.StartsWith("mono") || lowerAsmb.StartsWith("system") ||
            //                 lowerAsmb.StartsWith("microsoft") ||
            //                 Constants.SYSTEM_ASSEMBLIES.Contains(asmb))
            //             {
            //                 if (Constants.SYSTEM_ASSEMBLIES.Contains(asmb))
            //                 {
            //                     Console.WriteLine($"Resolved Known Standard System Assembly -- {asmb}");
            //                 }
            //                 else
            //                 {
            //                     Console.WriteLine(
            //                         $"Warning: Resolved to Unknown System assembly but found in Parkitect Managed -- {asmb}");
            //                 }
            //
            //                 project.Assemblies.Add(new Project.AssemblyInfo
            //                 {
            //                     Name = asmb
            //                 });
            //             }
            //             else if (Constants.PARKITECT_ASSEMBLIES.Contains(asmb))
            //             {
            //                 project.Assemblies.Add(new Project.AssemblyInfo
            //                 {
            //                     Name = asmb,
            //                     HintPath = parkitectAssemblyPath,
            //                     Version = versionInfo.FileVersion,
            //                     Culture = "neutral",
            //                     PublicKeyToken = "null",
            //                     IsPrivate = false
            //                 });
            //                 Console.WriteLine(
            //                     $"Resolved to Known System assembly but found in Parkitect Managed -- {asmb}");
            //             }
            //             else
            //             {
            //                 project.Assemblies.Add(new Project.AssemblyInfo
            //                 {
            //                     Name = asmb,
            //                     HintPath = parkitectAssemblyPath,
            //                     Version = versionInfo.FileVersion,
            //                     Culture = "neutral",
            //                     PublicKeyToken = "null",
            //                     IsPrivate = false
            //                 });
            //                 Console.WriteLine(
            //                     $"Warning: Resolved to Unknown System assembly but found in Parkitect managed: -- {asmb}");
            //             }
            //         }
            //         else
            //         {
            //             project.Assemblies.Add(new Project.AssemblyInfo()
            //             {
            //                 Name = asmb
            //             });
            //             Console.WriteLine($"Warning: Unknown Assembly -- {asmb}");
            //         }
            //     }
            //     else
            //     {
            //         Console.WriteLine("Error: Can't find Parkitect Assemblies");
            //         Environment.Exit(0);
            //     }
            // }

            var matcher = new Matcher();
            foreach (var s in Constants.IGNORE_FILES)
            {
                matcher.AddExclude(s);
            }

            if (configuration.Assets != null)
            {
                foreach (var asset in configuration.Assets)
                {
                    matcher.AddInclude(asset);
                }
            }

            foreach (var file in matcher.GetResultsInFullPath("./"))
            {
                Console.WriteLine($"Asset: {file}");
                project.Content.Add(new Project.ContentGroup
                {
                    Include = file,
                    CopyToOutput = Project.CopyOuputRule.ALWAYS
                });
            }

            matcher = new Matcher();
            foreach (var s in Constants.IGNORE_FILES)
            {
                matcher.AddExclude(s);
            }

            if (configuration.Sources != null)
            {
                foreach (var source in configuration.Sources)
                {
                    matcher.AddInclude(source);
                }
            }
            else
            {
                matcher.AddInclude("*.cs");
                matcher.AddInclude("**/*.cs");
            }

            foreach (var file in matcher.GetResultsInFullPath("./"))
            {
                project.Compile.Add(file);
            }

            if (!String.IsNullOrEmpty(configuration.Workshop))
            {
                File.WriteAllLines("./steam_workshop-id", new[] {configuration.Workshop});
                project.Content.Add(new Project.ContentGroup()
                {
                    Include = "./steam_workshop-id",
                    CopyToOutput = Project.CopyOuputRule.ALWAYS
                });
            }

            if (!String.IsNullOrEmpty(configuration.Preview))
            {
                project.Content.Add(new Project.ContentGroup()
                {
                    Include = configuration.Preview,
                    CopyToOutput = Project.CopyOuputRule.ALWAYS,
                    TargetPath = "preview.png"
                });
            }

            project.None.Add("parkitect.json");
            if(File.Exists("packages.config"))
                project.None.Add("packages.config");

            project.OutputPath = Path.Combine(Constants.GetParkitectPath,
                !String.IsNullOrEmpty(configuration.Folder) ? configuration.Folder : configuration.Name);
            Console.WriteLine($"Output Path: {project.OutputPath}");

            if (!project.Save($"./{configuration.Name}.csproj"))
            {
                Console.WriteLine($"Failed to create project: {configuration.Name}.csproj");
            }

            Console.WriteLine("Completed");
            Environment.Exit(0);
            return 0;
        }

        public static int UploadOptions(UploadOptions options)
        {
            return 1;
        }

        public static int InitOptions(InitOptions options)
        {
            Console.WriteLine(
                @"This utility will walk you through creating a parkitect.json file. It only covers the most common items, and tries to guess sensible defaults.");
            Console.WriteLine();
            Console.WriteLine(@"Press ^C at any time to quit.");

            ParkitectConfiguration configuration = new ParkitectConfiguration();

            configuration.Name = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath("./")));
            if (String.IsNullOrEmpty(options.Name))
            {
                Console.Write($"Mod Name ({configuration.Name}): ");
                configuration.Name = Console.ReadLine();
            }

            Console.Write("Author: ");
            configuration.Author = Console.ReadLine();

            Console.Write("Version: ");
            configuration.Version = Console.ReadLine();

            Console.WriteLine("Path to Preview relative to current path.");

            Console.Write("Preview: ");
            configuration.Version = Console.ReadLine();

            Console.WriteLine("ID for the mod published in the workshop.");
            Console.Write("Workshop: ");
            configuration.Workshop = Console.ReadLine();

            configuration.Assemblies = new List<string>
            {
                "System",
                "System.Core",
                "System.Data",
                "UnityEngine.AssetBundleModule",
                "UnityEngine.CoreModule",
                "Parkitect"
            };
            Console.WriteLine($"Default Assemblies: {String.Join(", ", configuration.Assemblies)}");

            while (true)
            {
                Console.Write("Assembly: ");
                String assembly = Console.ReadLine();
                if (String.IsNullOrEmpty(assembly))
                    break;
                configuration.Assemblies.Add(assembly);
            }

            configuration.Assets = new List<string> {
                "Assets/**/*"
            };
            while (true)
            {
                Console.Write("Assembly: ");
                String asset = Console.ReadLine();
                if (String.IsNullOrEmpty(asset))
                    break;
                configuration.Assets.Add(asset);
            }

            File.WriteAllText("./" + Constants.PARKITECT_CONFIG_FILE, JsonConvert.SerializeObject(configuration, Formatting.Indented));
            return 1;
        }

        public static int SetupWorkspaceOption(WorkspaceOptions workspaceOptions)
        {
            DepotDownloader downloader = new DepotDownloader();
            if (downloader.Login(workspaceOptions.SteamUsername, workspaceOptions.SteamPassword))
            {
                String ParkitectPath = Path.Combine(workspaceOptions.Path, "Game");
                downloader.DownloadDepot(ParkitectPath, 453090, 453094, "public", s => s.EndsWith(".dll")).Wait();
                UpdateProjectHintsAndOutput(workspaceOptions.Path,
                    Path.Combine(ParkitectPath, "Parkitect_Data/Managed"),
                    workspaceOptions.Output);
            }
            else
            {
                Console.WriteLine("Failed to login");
            }

            Console.WriteLine("Completed");
            Environment.Exit(0);
            return 0;
        }
    }
}
