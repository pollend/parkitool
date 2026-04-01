using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace Parkitool
{
    public class Project
    {
        public enum CopyOuputRule
        {
            NULL,
            ALWAYS,
            NEVER,
            PRESERVE_NEWEST
        }

        public class ContentGroup {
            public String Include { get; set; }
            public CopyOuputRule CopyToOutput { get; set; }
            public String TargetPath { get; set; }
        }

        public class AssemblyInfo
        {
            public bool? IsPrivate { get; set; } = null;
            public String HintPath { get; set; } = null;
            public String Name { get; set; } = null;
            public String Version { get; set; } = null;
            public String Culture { get; set; } = null;
            public String PublicKeyToken { get; set; } = null;
        }

        public List<AssemblyInfo> Assemblies { get; } = new List<AssemblyInfo>();
        public List<ContentGroup> Content { get; } = new List<ContentGroup>();
        public List<String> Compile { get; } = new List<String>();

        public List<String> None { get; } = new List<string>();
        public String OutputPath { get; set; } = "./bin";

        private XmlAttribute CreateAttribute(XmlDocument document, String name, String value) {
            var attr = document.CreateAttribute(name);
            attr.InnerText = value;
            return attr;
        }

        public bool Save(String path)
        {
            XmlDocument document = new XmlDocument();

            XmlNode docNode = document.CreateXmlDeclaration("1.0", "UTF-8", null);
            document.AppendChild(docNode);

            var project = document.CreateElement("Project");
            project.Attributes.Append(CreateAttribute(document, "Sdk", "Microsoft.NET.Sdk"));
            project.Attributes.Append(CreateAttribute(document, "xmlns", "http://schemas.microsoft.com/developer/msbuild/2003"));

            document.AppendChild(project);
            // ---------------------------------------------------------------------------------------------------------------------
            {
                var propertyGroup = document.CreateElement("PropertyGroup");

                var configuration = document.CreateElement("Configuration");
                configuration.InnerText = "Debug";
                configuration.Attributes.Append(CreateAttribute(document, "Condition"," '$(Configuration)' == '' "));
                propertyGroup.AppendChild(configuration);

                var platform = document.CreateElement("Platform");
                platform.InnerText = "AnyCPU";
                platform.Attributes.Append(CreateAttribute(document, "Condition"," '$(Platform)' == '' "));
                propertyGroup.AppendChild(platform);

                var debugType = document.CreateElement("DebugType");
                debugType.InnerText = "pdbonly";
                propertyGroup.AppendChild(debugType);
                
                var targetFramework = document.CreateElement("TargetFramework");
                targetFramework.InnerText = "net8.0";
                propertyGroup.AppendChild(targetFramework);
                
                var optimization = document.CreateElement("Optimize");
                optimization.InnerText = "true";
                propertyGroup.AppendChild(optimization);

                var outputPath = document.CreateElement("OutputPath");
                outputPath.InnerText = OutputPath;
                propertyGroup.AppendChild(outputPath);

                var defineConstants = document.CreateElement("DefineConstants");
                defineConstants.InnerText = "TRACE";
                propertyGroup.AppendChild(defineConstants);

                var errorReport = document.CreateElement("ErrorReport");
                errorReport.InnerText = "prompt";
                propertyGroup.AppendChild(errorReport);

                var otuputType = document.CreateElement("OutputType");
                otuputType.InnerText = "Library";
                propertyGroup.AppendChild(otuputType);


                project.AppendChild(propertyGroup);
            }

            if (Assemblies.Count > 0)
            {
                var assemblyGroup = document.CreateElement("ItemGroup");
                foreach (var assmb in Assemblies)
                {
                    var reference = document.CreateElement("Reference");
                    if (!String.IsNullOrEmpty(assmb.HintPath))
                    {
                        var hint = document.CreateElement("HintPath");
                        hint.InnerText = assmb.HintPath;
                        reference.AppendChild(hint);
                    }

                    if (assmb.IsPrivate != null)
                    {
                        var prv = document.CreateElement("Private");
                        prv.InnerText = assmb.IsPrivate.Value ? "True" : "False";
                        reference.AppendChild(prv);
                    }

                    String res = assmb.Name;
                    if (!String.IsNullOrEmpty(assmb.Version))
                    {
                        res += $", Version={assmb.Version}";
                    }

                    if (!String.IsNullOrEmpty(assmb.Culture))
                    {
                        res += $", Culture={assmb.Culture}";
                    }

                    if (!String.IsNullOrEmpty(assmb.PublicKeyToken))
                    {
                        res += $", PublicKeyToken={assmb.PublicKeyToken}";
                    }

                    reference.Attributes.Append(CreateAttribute(document, "Include", res));
                    assemblyGroup.AppendChild(reference);
                }

                project.AppendChild(assemblyGroup);
            }

            // ----------------------------------------------------------------------------------------------------------------------
            if (Compile.Count > 0)
            {
                var sourceGroup = document.CreateElement("ItemGroup");
                foreach (var comp in Compile)
                {
                    var source = document.CreateElement("Compile");
                    source.Attributes.Append(CreateAttribute(document, "Include", comp));
                    sourceGroup.AppendChild(source);
                }

                project.AppendChild(sourceGroup);
            }

            // ----------------------------------------------------------------------------------------------------------------------
            if (Content.Count > 0)
            {
                var contentGroup = document.CreateElement("ItemGroup");
                foreach (var cnt in Content)
                {
                    if (String.IsNullOrEmpty(cnt.TargetPath))
                    {
                        var content = document.CreateElement("Content");
                        if (cnt.CopyToOutput != CopyOuputRule.NULL)
                        {
                            var copyToOutput = document.CreateElement("CopyToOutputDirectory");
                            switch (cnt.CopyToOutput)
                            {
                                case CopyOuputRule.ALWAYS:
                                    copyToOutput.InnerText = "Always";
                                    break;
                                case CopyOuputRule.NEVER:
                                    copyToOutput.InnerText = "Never";
                                    break;
                                case CopyOuputRule.PRESERVE_NEWEST:
                                    copyToOutput.InnerText = " PreserveNewest";
                                    break;
                            }

                            content.AppendChild(copyToOutput);
                        }

                        content.Attributes.Append(CreateAttribute(document, "Include", cnt.Include));
                        contentGroup.AppendChild(content);
                    }
                    else
                    {
                        var content = document.CreateElement("ContentWithTargetPath");
                        if (cnt.CopyToOutput != CopyOuputRule.NULL)
                        {
                            var copyToOutput = document.CreateElement("CopyToOutputDirectory");
                            switch (cnt.CopyToOutput)
                            {
                                case CopyOuputRule.ALWAYS:
                                    copyToOutput.InnerText = "Always";
                                    break;
                                case CopyOuputRule.NEVER:
                                    copyToOutput.InnerText = "Never";
                                    break;
                                case CopyOuputRule.PRESERVE_NEWEST:
                                    copyToOutput.InnerText = " PreserveNewest";
                                    break;
                            }

                            content.AppendChild(copyToOutput);
                        }
                        var targetPath = document.CreateElement("TargetPath");
                        targetPath.InnerText = cnt.TargetPath;
                        content.AppendChild(targetPath);


                        content.Attributes.Append(CreateAttribute(document, "Include", cnt.Include));
                        contentGroup.AppendChild(content);
                    }

                }

                project.AppendChild(contentGroup);
            }

            if (None.Count > 0)
            {
                var contentGroup = document.CreateElement("ItemGroup");
                foreach (var cnt in None)
                {
                    var nn = document.CreateElement("None");
                    nn.Attributes.Append(CreateAttribute(document, "Include", cnt));
                    contentGroup.AppendChild(nn);
                }
                project.AppendChild(contentGroup);
            }

            // -------------------------------------------------------------------------------------------------------------------

            var imp = document.CreateElement("Import");
            imp.Attributes.Append(CreateAttribute(document, "Project", "$(MSBuildBinPath)\\Microsoft.CSharp.targets"));
            project.AppendChild(imp);

            Console.WriteLine($"Created Project: {path}");
            document.Save(path);

            return true;
        }
    }
}
