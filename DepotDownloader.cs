using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;

namespace Parkitool
{
    public class DepotDownloader
    {
        public const uint INVALID_APP_ID = uint.MaxValue;
        public const uint INVALID_DEPOT_ID = uint.MaxValue;
        public const ulong INVALID_MANIFEST_ID = ulong.MaxValue;

        private Steam3Session steam3;
        private Steam3Session.Credentials steam3Credentials;

        KeyValue GetSteam3AppSection(uint appId, EAppInfoSection section)
        {
            if (steam3 == null || steam3.AppInfo == null)
            {
                return null;
            }

            SteamApps.PICSProductInfoCallback.PICSProductInfo app;
            if (!steam3.AppInfo.TryGetValue(appId, out app) || app == null)
            {
                return null;
            }

            KeyValue appinfo = app.KeyValues;
            string section_key;

            switch (section)
            {
                case EAppInfoSection.Common:
                    section_key = "common";
                    break;
                case EAppInfoSection.Extended:
                    section_key = "extended";
                    break;
                case EAppInfoSection.Config:
                    section_key = "config";
                    break;
                case EAppInfoSection.Depots:
                    section_key = "depots";
                    break;
                default:
                    throw new NotImplementedException();
            }

            KeyValue section_kv = appinfo.Children.Where(c => c.Name == section_key).FirstOrDefault();
            return section_kv;
        }

        public bool Login(String username, String password)
        {
            steam3 = new Steam3Session(
                new SteamUser.LogOnDetails()
                {
                    Username = username,
                    Password =  password,
                    ShouldRememberPassword = false,
                    LoginID = 0x534B32, // "SK2"
                }
            );

            steam3Credentials = steam3.WaitForCredentials();

            if ( !steam3Credentials.IsValid )
            {
                Console.WriteLine( "Unable to get steam3 credentials." );
                return false;
            }

            return true;
        }

        public async Task DownloadDepot(String path, uint appId, uint depotId, string branch,
            Func<String, bool> includeFile)
        {
            Directory.CreateDirectory(path);

            // retrieve app info
            steam3.RequestAppInfo(appId);

            var depotIDs = new List<uint>();
            KeyValue depots = GetSteam3AppSection(appId, EAppInfoSection.Depots);

            Console.WriteLine("Using app branch: '{0}'.", branch);
            ulong manifestId = GetSteam3DepotManifest(depotId, appId, branch);

            steam3.RequestDepotKey(depotId, appId);
            byte[] depotKey = steam3.DepotKeys[depotId];
            string contentName = GetAppOrDepotName(depotId, appId);

            Console.WriteLine("Downloading depot {0} - {1}", depotId, contentName);

            DepotManifest depotManifest = null;

            CancellationTokenSource cts = new CancellationTokenSource();
            CDNClientPool cdnPool = new CDNClientPool(steam3);
            cdnPool.ExhaustedToken = cts;

            while (depotManifest == null)
            {
                Tuple<SteamKit2.CDN.Server, string> connection = null;
                try
                {
                    connection = await cdnPool.GetConnectionForDepot(appId, depotId, CancellationToken.None);
                    
                    depotManifest = await cdnPool.CDNClient.DownloadManifestAsync(depotId, manifestId,
                        0, connection.Item1,  depotKey).ConfigureAwait(false);

                    cdnPool.ReturnConnection(connection);
                }
                catch (SteamKitWebRequestException e)
                {
                    cdnPool.ReturnBrokenConnection(connection);

                    if (e.StatusCode == HttpStatusCode.Unauthorized || e.StatusCode == HttpStatusCode.Forbidden)
                    {
                        Console.WriteLine("Encountered 401 for depot manifest {0} {1}. Aborting.", depotId, manifestId);
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Encountered error downloading depot manifest {0} {1}: {2}", depotId,
                            manifestId, e.StatusCode);
                    }
                }
                catch (Exception e)
                {
                    cdnPool.ReturnBrokenConnection(connection);
                    Console.WriteLine("Encountered error downloading manifest for depot {0} {1}: {2}", depotId,
                        manifestId, e.Message);
                }
            }

            if (depotManifest == null)
            {
                Console.WriteLine("\nUnable to download manifest {0} for depot {1}", manifestId, depotId);
                return;
            }


            ulong size_downloaded = 0;
            foreach (var folder in depotManifest.Files.AsParallel().Where(f => f.Flags.HasFlag(EDepotFileFlag.Directory))
                .ToList())
            {
                Directory.CreateDirectory(Path.Join(path, folder.FileName));
            }

            ulong TotalBytesCompressed = 0;
            ulong TotalBytesUncompressed = 0;
            ulong DepotBytesCompressed = 0;
            ulong DepotBytesUncompressed = 0;

            var semaphore = new SemaphoreSlim(10);
            var files = depotManifest.Files.AsParallel().Where(f => !f.Flags.HasFlag(EDepotFileFlag.Directory) && includeFile(f.FileName))
                .ToArray();

            ulong complete_download_size = 0;
            foreach (var file in files)
            {
                complete_download_size += file.TotalSize;
            }

            var tasks = new Task[files.Length];
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var task = Task.Run(async () =>
                {
                    cts.Token.ThrowIfCancellationRequested();

                    try
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                        cts.Token.ThrowIfCancellationRequested();

                        string fileFinalPath = Path.Combine(path, file.FileName);

                        FileStream fs = null;
                        List<DepotManifest.ChunkData> neededChunks = new List<DepotManifest.ChunkData>();
                        FileInfo fi = new FileInfo(fileFinalPath);
                        if (!fi.Exists)
                        {
                            // create new file. need all chunks
                            fs = File.Create(fileFinalPath);
                            fs.SetLength((long) file.TotalSize);
                            neededChunks = new List<DepotManifest.ChunkData>(file.Chunks);
                        }
                        else
                        {
                            fs = File.Open(fileFinalPath, FileMode.Truncate);
                            fs.SetLength((long) file.TotalSize);
                            neededChunks = new List<DepotManifest.ChunkData>(file.Chunks);
                        }

                        foreach (var chunk in neededChunks)
                        {
                            if (cts.IsCancellationRequested) break;

                            string chunkID = Util.EncodeHexString(chunk.ChunkID);

                            byte[] chunkdata = new byte[2048];
                            int chunkSize = 0;

                            while (!cts.IsCancellationRequested)
                            {
                                Tuple<SteamKit2.CDN.Server, string> connection;
                                try
                                {
                                    connection = await cdnPool.GetConnectionForDepot(appId, depotId, cts.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                    break;
                                }

                                DepotManifest.ChunkData data = new DepotManifest.ChunkData();
                                data.ChunkID = chunk.ChunkID;
                                data.Checksum = chunk.Checksum;
                                data.Offset = chunk.Offset;
                                data.CompressedLength = chunk.CompressedLength;
                                data.UncompressedLength = chunk.UncompressedLength;

                                try
                                {
                                    chunkSize = await cdnPool.CDNClient.DownloadDepotChunkAsync(depotId, data,
                                        connection.Item1, chunkdata, depotKey).ConfigureAwait(false);
                                    cdnPool.ReturnConnection(connection);
                                    break;
                                }
                                catch (SteamKitWebRequestException e)
                                {
                                    cdnPool.ReturnBrokenConnection(connection);

                                    if (e.StatusCode == HttpStatusCode.Unauthorized ||
                                        e.StatusCode == HttpStatusCode.Forbidden)
                                    {
                                        Console.WriteLine("Encountered 401 for chunk {0}. Aborting.", chunkID);
                                        cts.Cancel();
                                        break;
                                    }
                                    else
                                    {
                                        Console.WriteLine("Encountered error downloading chunk {0}: {1}", chunkID,
                                            e.StatusCode);
                                    }
                                }
                                catch (Exception e)
                                {
                                    cdnPool.ReturnBrokenConnection(connection);
                                    Console.WriteLine("Encountered unexpected error downloading chunk {0}: {1}",
                                        chunkID, e.Message);
                                }
                            }

                            if (chunkSize > 0)
                            {
                                Console.WriteLine("Failed to find any server with chunk {0} for depot {1}. Aborting.",
                                    chunkID, depotId);
                                cts.Cancel();
                            }

                            // Throw the cancellation exception if requested so that this task is marked failed
                            cts.Token.ThrowIfCancellationRequested();

                            TotalBytesCompressed += chunk.CompressedLength;
                            DepotBytesCompressed += chunk.CompressedLength;
                            TotalBytesUncompressed += chunk.UncompressedLength;
                            DepotBytesUncompressed += chunk.UncompressedLength;

                            fs.Seek((long) chunk.Offset, SeekOrigin.Begin);
                            fs.Write(chunkdata, 0, chunkSize);

                            size_downloaded += chunk.UncompressedLength;
                        }

                        fs.Dispose();

                        Console.WriteLine("{0,6:#00.00}% {1}",
                            ((float) size_downloaded / (float) complete_download_size) * 100.0f, fileFinalPath);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tasks[i] = task;
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            Console.WriteLine("Depot {0} - Downloaded {1} bytes ({2} bytes uncompressed)", depotId,
                DepotBytesCompressed, DepotBytesUncompressed);
        }

        string GetAppOrDepotName( uint depotId, uint appId )
        {
            if ( depotId == INVALID_DEPOT_ID )
            {
                KeyValue info = GetSteam3AppSection( appId, EAppInfoSection.Common );

                if ( info == null )
                    return String.Empty;

                return info[ "name" ].AsString();
            }
            else
            {
                KeyValue depots = GetSteam3AppSection( appId, EAppInfoSection.Depots );

                if ( depots == null )
                    return String.Empty;

                KeyValue depotChild = depots[ depotId.ToString() ];

                if ( depotChild == null )
                    return String.Empty;

                return depotChild[ "name" ].AsString();
            }
        }

        // bool AccountHasAccess( uint depotId )
        // {
        //     if ( steam3 == null || steam3.steamUser.SteamID == null || ( steam3.Licenses == null && steam3.steamUser.SteamID.AccountType != EAccountType.AnonUser ) )
        //         return false;
        //
        //     IEnumerable<uint> licenseQuery;
        //     if ( steam3.steamUser.SteamID.AccountType == EAccountType.AnonUser )
        //     {
        //         licenseQuery = new List<uint>() { 17906 };
        //     }
        //     else
        //     {
        //         licenseQuery = steam3.Licenses.Select( x => x.PackageID ).Distinct();
        //     }
        //
        //     steam3.RequestPackageInfo( licenseQuery );
        //
        //     foreach ( var license in licenseQuery )
        //     {
        //         SteamApps.PICSProductInfoCallback.PICSProductInfo package;
        //         if ( steam3.PackageInfo.TryGetValue( license, out package ) && package != null )
        //         {
        //             if ( package.KeyValues[ "appids" ].Children.Any( child => child.AsUnsignedInteger() == depotId ) )
        //                 return true;
        //
        //             if ( package.KeyValues[ "depotids" ].Children.Any( child => child.AsUnsignedInteger() == depotId ) )
        //                 return true;
        //         }
        //     }
        //
        //     return false;
        // }

        ulong GetSteam3DepotManifest(uint depotId, uint appId, string branch)
        {
            KeyValue depots = GetSteam3AppSection(appId, EAppInfoSection.Depots);
            KeyValue depotChild = depots[depotId.ToString()];

            if (depotChild == KeyValue.Invalid)
                return INVALID_MANIFEST_ID;

            // Shared depots can either provide manifests, or leave you relying on their parent app.
            // It seems that with the latter, "sharedinstall" will exist (and equals 2 in the one existance I know of).
            // Rather than relay on the unknown sharedinstall key, just look for manifests. Test cases: 111710, 346680.
            if (depotChild["manifests"] == KeyValue.Invalid && depotChild["depotfromapp"] != KeyValue.Invalid)
            {
                uint otherAppId = depotChild["depotfromapp"].AsUnsignedInteger();
                if (otherAppId == appId)
                {
                    // This shouldn't ever happen, but ya never know with Valve. Don't infinite loop.
                    Console.WriteLine("App {0}, Depot {1} has depotfromapp of {2}!",
                        appId, depotId, otherAppId);
                    return INVALID_MANIFEST_ID;
                }

                steam3.RequestAppInfo(otherAppId);

                return GetSteam3DepotManifest(depotId, otherAppId, branch);
            }

            var manifests = depotChild["manifests"];
            var manifests_encrypted = depotChild["encryptedmanifests"];

            if (manifests.Children.Count == 0 && manifests_encrypted.Children.Count == 0)
                return INVALID_MANIFEST_ID;

            var node = manifests[branch];

            if (node.Value == null)
                return INVALID_MANIFEST_ID;

            return UInt64.Parse(node.Value);
        }

        // uint GetSteam3AppBuildNumber( uint appId, string branch )
        // {
        //     if ( appId == INVALID_APP_ID )
        //         return 0;
        //
        //
        //     KeyValue depots = ContentDownloader.GetSteam3AppSection( appId, EAppInfoSection.Depots );
        //     KeyValue branches = depots[ "branches" ];
        //     KeyValue node = branches[ branch ];
        //
        //     if ( node == KeyValue.Invalid )
        //         return 0;
        //
        //     KeyValue buildid = node[ "buildid" ];
        //
        //     if ( buildid == KeyValue.Invalid )
        //         return 0;
        //
        //     return uint.Parse( buildid.Value );
        // }


    }
}
