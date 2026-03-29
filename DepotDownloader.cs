using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;

namespace Parkitool
{
    public class DepotConfig {
        public int CellID { get; set; }
    }
    
    public class DepotDownloader
    {
        
        public static DepotConfig Config = new();
        
        public const uint INVALID_APP_ID = uint.MaxValue;
        public const uint INVALID_DEPOT_ID = uint.MaxValue;
        public const ulong INVALID_MANIFEST_ID = ulong.MaxValue;

        private Steam3Session steam3;
        // private Steam3Session.Credentials steam3Credentials;

        // KeyValue GetSteam3AppSection(uint appId, EAppInfoSection section)
        // {
        //     if (steam3 == null || steam3.AppInfo == null)
        //     {
        //         return null;
        //     }
        //
        //     SteamApps.PICSProductInfoCallback.PICSProductInfo app;
        //     if (!steam3.AppInfo.TryGetValue(appId, out app) || app == null)
        //     {
        //         return null;
        //     }
        //
        //     KeyValue appinfo = app.KeyValues;
        //     string section_key;
        //
        //     switch (section)
        //     {
        //         case EAppInfoSection.Common:
        //             section_key = "common";
        //             break;
        //         case EAppInfoSection.Extended:
        //             section_key = "extended";
        //             break;
        //         case EAppInfoSection.Config:
        //             section_key = "config";
        //             break;
        //         case EAppInfoSection.Depots:
        //             section_key = "depots";
        //             break;
        //         default:
        //             throw new NotImplementedException();
        //     }
        //
        //     KeyValue section_kv = appinfo.Children.Where(c => c.Name == section_key).FirstOrDefault();
        //     return section_kv;
        // }

        public bool Login(String username, String password)
        {
            steam3 = new Steam3Session(
                new SteamUser.LogOnDetails()
                {
                    Username = username,
                    Password =  password,
                    ShouldRememberPassword = Steam3Session.RememberPassword,
                    LoginID = 0x534B32, // "SK2"
                }
            );

            if (!steam3.WaitForCredentials())
            {
                Console.WriteLine("Unable to get steam3 credentials.");
                return false;
            }

            Task.Run(steam3.TickCallbacks);

            return true;
        }

        async Task<bool> AccountHasAccess(uint appId, uint depotId)
        {
            if (steam3 == null || steam3.steamUser.SteamID == null || (steam3.Licenses == null && steam3.steamUser.SteamID.AccountType != EAccountType.AnonUser))
                return false;

            IEnumerable<uint> licenseQuery;
            if (steam3.steamUser.SteamID.AccountType == EAccountType.AnonUser)
            {
                licenseQuery = [17906];
            }
            else
            {
                licenseQuery = steam3.Licenses.Select(x => x.PackageID).Distinct();
            }

            await steam3.RequestPackageInfo(licenseQuery);

            foreach (var license in licenseQuery)
            {
                if (steam3.PackageInfo.TryGetValue(license, out var package) && package != null)
                {
                    if (package.KeyValues["appids"].Children.Any(child => child.AsUnsignedInteger() == depotId))
                        return true;

                    if (package.KeyValues["depotids"].Children.Any(child => child.AsUnsignedInteger() == depotId))
                        return true;
                }
            }

            // Check if this app is free to download without a license
            var info = GetSteam3AppSection(appId, EAppInfoSection.Common);
            if (info != null && info["FreeToDownload"].AsBoolean())
                return true;

            return false;
        }

        
        
        public async Task DownloadDepot(String path, uint appId, uint depotId, string branch,
            Func<String, bool> includeFile)
        {
            Directory.CreateDirectory(path);
            await steam3.RequestAppInfo(appId);
            
            var manifestId = await GetSteam3DepotManifest(depotId, appId, branch);
            
            // var depotIDs = new List<uint>();
            // KeyValue depots = GetSteam3AppSection(appId, EAppInfoSection.Depots);

            Console.WriteLine("Using app branch: '{0}'.", branch);
            //ulong manifestId = GetSteam3DepotManifest(depotId, appId, branch);

            if (!await AccountHasAccess(appId, depotId))
            {
                Console.WriteLine("Depot {0} is not available from this account.", depotId);
                return;
            }
            
            await steam3.RequestDepotKey(depotId, appId);
            if (!steam3.DepotKeys.TryGetValue(depotId, out var depotKey))
            {
                Console.WriteLine("No valid depot key for {0}, unable to download.", depotId);
                return;
            }
            
            string contentName = GetAppOrDepotName(depotId, appId);
            Console.WriteLine("Downloading depot {0} - {1}", depotId, contentName);

            DepotManifest depotManifest = null;

            CancellationTokenSource cts = new CancellationTokenSource();
            CDNClientPool cdnPool = new CDNClientPool(steam3, appId);
            
            await cdnPool.UpdateServerList();
            
            ulong manifestRequestCode = 0;
            var manifestRequestCodeExpiration = DateTime.MinValue;
            while (depotManifest == null)
            {
                var connection = cdnPool.GetConnection();
                try
                {
                    string cdnToken = null;
                    if (steam3.CDNAuthTokens.TryGetValue((depotId, connection.Host), out var authTokenCallbackPromise))
                    {
                        var result = await authTokenCallbackPromise.Task;
                        cdnToken = result.Token;
                    }
                    
                    var now = DateTime.Now;
                    if (manifestRequestCode == 0 || now >= manifestRequestCodeExpiration)
                    {
                        manifestRequestCode = await steam3.GetDepotManifestRequestCodeAsync(
                            depotId,
                            appId,
                            manifestId,
                            branch);
                        // This code will hopefully be valid for one period following the issuing period
                        manifestRequestCodeExpiration = now.Add(TimeSpan.FromMinutes(5));

                        // If we could not get the manifest code, this is a fatal error
                        if (manifestRequestCode == 0)
                        {
                            cts.Cancel();
                        }
                    }
                    
                    Console.WriteLine("ContentDownloader",
                        "Downloading manifest {0} from {1} with {2}",
                        manifestId,
                        connection,
                        cdnPool.ProxyServer != null ? cdnPool.ProxyServer : "no proxy");
                    depotManifest = await cdnPool.CDNClient.DownloadManifestAsync(
                        depotId,
                        manifestId,
                        manifestRequestCode,
                        connection,
                        depotKey,
                        cdnPool.ProxyServer,
                        cdnToken).ConfigureAwait(false);

                    cdnPool.ReturnConnection(connection);
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("Connection timeout downloading depot manifest {0} {1}. Retrying.", depotId, manifestId);
                }
                catch (SteamKitWebRequestException e)
                {
                    // If the CDN returned 403, attempt to get a cdn auth if we didn't yet
                    if (e.StatusCode == HttpStatusCode.Forbidden && !steam3.CDNAuthTokens.ContainsKey((depotId, connection.Host)))
                    {
                        await steam3.RequestCDNAuthToken(appId, depotId, connection);

                        cdnPool.ReturnConnection(connection);

                        continue;
                    }

                    cdnPool.ReturnBrokenConnection(connection);

                    if (e.StatusCode == HttpStatusCode.Unauthorized || e.StatusCode == HttpStatusCode.Forbidden)
                    {
                        Console.WriteLine("Encountered {2} for depot manifest {0} {1}. Aborting.", depotId, manifestId, (int)e.StatusCode);
                        break;
                    }

                    if (e.StatusCode == HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("Encountered 404 for depot manifest {0} {1}. Aborting.", depotId, manifestId);
                        break;
                    }

                    Console.WriteLine("Encountered error downloading depot manifest {0} {1}: {2}", depotId, manifestId, e.StatusCode);
                }
                catch (OperationCanceledException)
                {
                    break;
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
            
            // Throw the cancellation exception if requested so that this task is marked failed
            cts.Token.ThrowIfCancellationRequested();
            
            Console.WriteLine("Manifest {0} ({1})", manifestId, depotManifest.CreationTime);

            ulong size_downloaded = 0;
            foreach (var folder in depotManifest.Files.AsParallel().Where(f => f.Flags.HasFlag(EDepotFileFlag.Directory)).ToList())
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

                            var chunkBuffer = ArrayPool<byte>.Shared.Rent((int)chunk.UncompressedLength);
                            int written = 0;

                            do
                            {
                                var connection = cdnPool.GetConnection();
                                DepotManifest.ChunkData data = new DepotManifest.ChunkData();
                                data.ChunkID = chunk.ChunkID;
                                data.Checksum = chunk.Checksum;
                                data.Offset = chunk.Offset;
                                data.CompressedLength = chunk.CompressedLength;
                                data.UncompressedLength = chunk.UncompressedLength;

                                try
                                {
                                    Console.WriteLine("Downloading chunk {0} from {1} with {2}", chunkID, connection, cdnPool.ProxyServer != null ? cdnPool.ProxyServer : "no proxy");
                                    written = await cdnPool.CDNClient.DownloadDepotChunkAsync(depotId, data,
                                        connection, chunkBuffer, depotKey).ConfigureAwait(false);
                                    cdnPool.ReturnConnection(connection);
                                    break;
                                }
                                catch (TaskCanceledException)
                                {
                                    Console.WriteLine("Connection timeout downloading chunk {0}", chunkID);
                                    cdnPool.ReturnBrokenConnection(connection);
                                }
                                catch (SteamKitWebRequestException e)
                                {
                                    // If the CDN returned 403, attempt to get a cdn auth if we didn't yet,
                                    // if auth task already exists, make sure it didn't complete yet, so that it gets awaited above
                                    if (e.StatusCode == HttpStatusCode.Forbidden &&
                                        (!steam3.CDNAuthTokens.TryGetValue((depotId, connection.Host), out var authTokenCallbackPromise) || !authTokenCallbackPromise.Task.IsCompleted))
                                    {
                                        await steam3.RequestCDNAuthToken(appId, depotId, connection);

                                        cdnPool.ReturnConnection(connection);

                                        continue;
                                    }

                                    cdnPool.ReturnBrokenConnection(connection);

                                    if (e.StatusCode == HttpStatusCode.Unauthorized || e.StatusCode == HttpStatusCode.Forbidden)
                                    {
                                        Console.WriteLine("Encountered {1} for chunk {0}. Aborting.", chunkID, (int)e.StatusCode);
                                        break;
                                    }

                                    Console.WriteLine("Encountered error downloading chunk {0}: {1}", chunkID, e.StatusCode);
                                }
                                catch (OperationCanceledException)
                                {
                                    break;
                                }
                                catch (Exception e)
                                {
                                    cdnPool.ReturnBrokenConnection(connection);
                                    Console.WriteLine("Encountered unexpected error downloading chunk {0}: {1}",
                                        chunkID, e.Message);
                                }
                            } while (written == 0);

                            if (written == 0)
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
                            fs.Write(chunkBuffer, 0, written);

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

        
        KeyValue GetSteam3AppSection(uint appId, EAppInfoSection section)
        {
            if (steam3 == null || steam3.AppInfo == null)
            {
                return null;
            }

            if (!steam3.AppInfo.TryGetValue(appId, out var app) || app == null)
            {
                return null;
            }

            var appinfo = app.KeyValues;
            var section_key = section switch
            {
                EAppInfoSection.Common => "common",
                EAppInfoSection.Extended => "extended",
                EAppInfoSection.Config => "config",
                EAppInfoSection.Depots => "depots",
                _ => throw new NotImplementedException(),
            };
            var section_kv = appinfo.Children.Where(c => c.Name == section_key).FirstOrDefault();
            return section_kv;
        }
        
        async Task<ulong> GetSteam3DepotManifest(uint depotId, uint appId, string branch)
        {
            var depots = GetSteam3AppSection(appId, EAppInfoSection.Depots);
            var depotChild = depots[depotId.ToString()];

            if (depotChild == KeyValue.Invalid)
                return INVALID_MANIFEST_ID;

            // Shared depots can either provide manifests, or leave you relying on their parent app.
            // It seems that with the latter, "sharedinstall" will exist (and equals 2 in the one existance I know of).
            // Rather than relay on the unknown sharedinstall key, just look for manifests. Test cases: 111710, 346680.
            if (depotChild["manifests"] == KeyValue.Invalid && depotChild["depotfromapp"] != KeyValue.Invalid)
            {
                var otherAppId = depotChild["depotfromapp"].AsUnsignedInteger();
                if (otherAppId == appId)
                {
                    // This shouldn't ever happen, but ya never know with Valve. Don't infinite loop.
                    Console.WriteLine("App {0}, Depot {1} has depotfromapp of {2}!",
                        appId, depotId, otherAppId);
                    return INVALID_MANIFEST_ID;
                }

                await steam3.RequestAppInfo(otherAppId);

                return await GetSteam3DepotManifest(depotId, otherAppId, branch);
            }

            var manifests = depotChild["manifests"];

            if (manifests.Children.Count == 0)
                return INVALID_MANIFEST_ID;

            var node = manifests[branch]["gid"];

            // Non passworded branch, found the manifest
            if (node.Value != null)
                return ulong.Parse(node.Value);

            // If we requested public branch and it had no manifest, nothing to do
            // if (string.Equals(branch, DEFAULT_BRANCH, StringComparison.OrdinalIgnoreCase))
                // return INVALID_MANIFEST_ID;
            //
            // // Either the branch just doesn't exist, or it has a password
            // if (string.IsNullOrEmpty(Config.BetaPassword))
            // {
            //     Console.WriteLine($"Branch {branch} for depot {depotId} was not found, either it does not exist or it has a password.");
            //     return INVALID_MANIFEST_ID;
            // }

            // if (!steam3.AppBetaPasswords.ContainsKey(branch))
            // {
            //     // Submit the password to Steam now to get encryption keys
            //     await steam3.CheckAppBetaPassword(appId, Config.BetaPassword);
            //
            //     if (!steam3.AppBetaPasswords.ContainsKey(branch))
            //     {
            //         Console.WriteLine($"Error: Password was invalid for branch {branch} (or the branch does not exist)");
            //         return INVALID_MANIFEST_ID;
            //     }
            // }

            // Got the password, request private depot section
            // TODO: We're probably repeating this request for every depot?
            var privateDepotSection = await steam3.GetPrivateBetaDepotSection(appId, branch);

            // Now repeat the same code to get the manifest gid from depot section
            depotChild = privateDepotSection[depotId.ToString()];

            if (depotChild == KeyValue.Invalid)
                return INVALID_MANIFEST_ID;

            manifests = depotChild["manifests"];

            if (manifests.Children.Count == 0)
                return INVALID_MANIFEST_ID;

            node = manifests[branch]["gid"];

            if (node.Value == null)
                return INVALID_MANIFEST_ID;

            return ulong.Parse(node.Value);
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
