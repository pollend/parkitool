using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Parkitool
{
    public class Constants
    {
        public const String PARKITECT_CONFIG_FILE = "parkitect.json";
        public const String PROJECT_NAMESPACE = "http://schemas.microsoft.com/developer/msbuild/2003";
        public const String HIDDEN_FOLDER = ".Parkitect";

        public const String PARKITECT_ASSEMBLY_PATH = "Parkitect_Data/Managed";

        public static IList<String> IGNORE_FILES = new List<string>
        {
            HIDDEN_FOLDER,
            ".git",
            "obj",
            "bin"
        }.AsReadOnly();

        public static String GetParkitectPath
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return Path.GetFullPath(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Parkitect/", "Mods"));
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return Path.GetFullPath(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
                        "/../Library/Application Support/Parkitect/", "Mods"));
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".steam/steam/steamapps/common/Parkitect/Mods"));
                }

                return "./bin";
            }
        }

        public static readonly IList<string> SYSTEM_ASSEMBLIES = new List<string>
        {
            "Accessibility",
            "Mono.Data.Sqlite",
            "Mono.Data.Tds",
            "Mono.Messaging",
            "Mono.Posix",
            "Mono.Security",
            "Mono.WebBrowser",
            "mscorlib",
            "netstandard",
            "System.ComponentModel.DataAnnotations",
            "System.Configuration",
            "System.Configuration.Install",
            "System.Core",
            "System.Data",
            "System.Design",
            "System.DirectoryServices",
            "System",
            "System.Drawing",
            "System.EnterpriseServices",
            "System.IdentityModel",
            "System.IdentityModel.Selectors",
            "System.Messaging",
            "System.Numerics",
            "System.Runtime.Serialization",
            "System.Runtime.Serialization.Formatters.Soap",
            "System.Security",
            "System.ServiceModel.Activation",
            "System.ServiceModel",
            "System.ServiceModel.Internals",
            "System.Transactions",
            "System.Web.ApplicationServices",
            "System.Web",
            "System.Web.Services",
            "System.Windows.Forms",
            "System.Xml",
        }.AsReadOnly();

        public static readonly IList<string> THIRD_PARTY_ASSEMBLIES =
            new List<string>
            {
                "Novell.Directory.Ldap",
                "protobuf-net",
                "DOTween43",
                "DOTween46",
                "DOTween50",
                "DOTween",
                "ICSharpCode.SharpZipLib",
                "ThirdParty",
                "ThirdParty.DynamicDecals",
                "ThirdParty.GraphMaker",
                "ThirdParty.Lutify",
                "ThirdParty.ScreenSpaceCloudShadow",
                "ThirdParty.TiltShift",
                "ThirdParty.UnityUiExtensions",
            }.AsReadOnly();

        public static readonly IList<string> UNITYENGINE_ASSEMBLIES = new List<string>
        {
            "UnityEngine.AccessibilityModule",
            "UnityEngine.AIModule",
            "UnityEngine.Analytics",
            "UnityEngine.AnimationModule",
            "UnityEngine.ARModule",
            "UnityEngine.AssetBundleModule",
            "UnityEngine.AudioModule",
            "UnityEngine.ClothModule",
            "UnityEngine.ClusterInputModule",
            "UnityEngine.ClusterRendererModule",
            "UnityEngine.CoreModule",
            "UnityEngine.CrashLog",
            "UnityEngine.CrashReportingModule",
            "UnityEngine.DirectorModule",
            "UnityEngine",
            "UnityEngine.GameCenterModule",
            "UnityEngine.GridModule",
            "UnityEngine.ImageConversionModule",
            "UnityEngine.IMGUIModule",
            "UnityEngine.InputModule",
            "UnityEngine.JSONSerializeModule",
            "UnityEngine.Networking",
            "UnityEngine.ParticlesLegacyModule",
            "UnityEngine.ParticleSystemModule",
            "UnityEngine.PerformanceReportingModule",
            "UnityEngine.Physics2DModule",
            "UnityEngine.PhysicsModule",
            "UnityEngine.ScreenCaptureModule",
            "UnityEngine.SharedInternalsModule",
            "UnityEngine.SpatialTracking",
            "UnityEngine.SpriteMaskModule",
            "UnityEngine.SpriteShapeModule",
            "UnityEngine.StandardEvents",
            "UnityEngine.StyleSheetsModule",
            "UnityEngine.TerrainModule",
            "UnityEngine.TerrainPhysicsModule",
            "UnityEngine.TextRenderingModule",
            "UnityEngine.TilemapModule",
            "UnityEngine.Timeline",
            "UnityEngine.UI",
            "UnityEngine.UIElementsModule",
            "UnityEngine.UIModule",
            "UnityEngine.UNETModule",
            "UnityEngine.UnityAnalyticsModule",
            "UnityEngine.UnityConnectModule",
            "UnityEngine.UnityWebRequestAudioModule",
            "UnityEngine.UnityWebRequestModule",
            "UnityEngine.UnityWebRequestTextureModule",
            "UnityEngine.UnityWebRequestWWWModule",
            "UnityEngine.VehiclesModule",
            "UnityEngine.VideoModule",
            "UnityEngine.VRModule",
            "UnityEngine.WebModule",
            "UnityEngine.WindModule",
            "UnityFbxPrefab",
            "Unity.Postprocessing.Runtime"
        }.AsReadOnly();

        public static readonly IList<string> PARKITECT_ASSEMBLIES = new List<string>
        {
            "Parkitect"
        }.Concat(THIRD_PARTY_ASSEMBLIES).Concat(UNITYENGINE_ASSEMBLIES).ToList().AsReadOnly();

        public static readonly IList<string> ALL_ASSEMBLIES = new List<string>().Concat(PARKITECT_ASSEMBLIES)
            .Concat(SYSTEM_ASSEMBLIES).ToList().AsReadOnly();
    }
}
