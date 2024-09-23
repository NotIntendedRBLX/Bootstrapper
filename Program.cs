using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Security.Permissions;
using Microsoft.Win32;
using System.IO;
using System.Net;
using Serilog;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

class Program
{
    static void UnzipFromStream(string zipPath, string fullZipToPath)
    {
        FastZip fastZip = new FastZip();
        fastZip.CreateEmptyDirectories = true;
        fastZip.ExtractZip(zipPath, fullZipToPath, null);
    }

    public void UnZip(string zipPath, string outPath)
    {
        ZipInputStream zipIn = null;
        ZipEntry entry = null;
        FileStream streamWriter = null;

        try
        {
            //string dirPath = Path.GetDirectoryName(destDirPath + entry.Name);
            if (!Directory.Exists(outPath)) Directory.CreateDirectory(outPath);

            zipIn = new ZipInputStream(File.OpenRead(zipPath));
            while ((entry = zipIn.GetNextEntry()) != null)
            {
                Log.Information(outPath + entry.Name + (entry.IsDirectory ? " is directory" : " is not directory"));
                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(outPath + entry.Name);
                }
                else
                {
                    streamWriter = File.Create(outPath + entry.Name);

                    int size = 2048;
                    byte[] buffer = new byte[size];
                    while ((size = zipIn.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        streamWriter.Write(buffer, 0, size);
                    }

                    streamWriter.Close();
                }
            }
        }
        catch (System.Threading.ThreadAbortException ex) { }
        catch (Exception ex) { throw ex; }

        if (zipIn != null) zipIn.Close();
        if (streamWriter != null) streamWriter.Close();
    }

    static async Task<string> DownloadAndUnZip(HttpClient client, string url, string zipPath, string path)
    {
        try
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);

            using (var fs = new FileStream(zipPath, FileMode.OpenOrCreate))
            {
                var res = await client.GetStreamAsync(url);
                await res.CopyToAsync(fs);
            }

            Log.Information("Extracting...");
            UnzipFromStream(zipPath, path);
            return "Done!";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    static void CreateFolders(string calvyPath, string versionsPath, string latestVersionPath)
    {
        if (!Directory.Exists(calvyPath))
        {
            Directory.CreateDirectory(calvyPath);
        }

        if (!Directory.Exists(versionsPath))
        {
            Directory.CreateDirectory(versionsPath);
        }

        if (!Directory.Exists(latestVersionPath))
        {
            Directory.CreateDirectory(latestVersionPath);
        }
    }

    static async Task Main(string[] args_)
    {
        string version = "v1";
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("bootstrapper.log",
                rollingInterval: RollingInterval.Hour,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        //string[] logo = {
        //"",
        //};

        //int terminalWidth = Console.WindowWidth;
        //foreach (string line in logo)
        //{
        //    int padding = (terminalWidth - line.Length) / 2;
        //    if (padding > 0)
        //    {
        //        Console.WriteLine($"{new string(' ', padding)}{line}");
        //    }
        //    else
        //    {
        //        Console.WriteLine(line);
        //    }
        //}

        Console.WriteLine("\n");
        Log.Information("calvity.ru | free-speech");

        var cmdArgs = Environment.GetCommandLineArgs();
        var args = new Dictionary<string, string>();
        if (cmdArgs.Length > 1)
        {
            var launchURL = cmdArgs[1];
            string[] unfilteredArgs = launchURL.Split('+');

            for (int i = 0; i < unfilteredArgs.Length; i++)
            {
                string[] argInfo = unfilteredArgs[i].Split(':');
                args[argInfo[0]] = unfilteredArgs[i].Replace(argInfo[0] + ":", "");
            }
        }
        else
        {
            Log.Fatal("no args");
            Log.CloseAndFlush();
            return;
        }

        Log.Information("contacting calvy servers");

        var client = new HttpClient();
        var latestVersion = "";

        try
        {
            latestVersion = await client.GetStringAsync("https://setup.calvity.ru/latest-version.txt");
            latestVersion = latestVersion.Trim();
        }
        catch (Exception ex)
        {
            Log.Error("failed. Error: " + ex.Message);
            Log.CloseAndFlush();
            return;
        }

        if (string.IsNullOrEmpty(latestVersion))
        {
            Log.Error("failed to get version, no information.");
            Log.CloseAndFlush();
            return;
        }
        Log.Information("version: " + latestVersion);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var calvyfolder = Path.Combine(localAppData, "calvity");
        var versionsPath = Path.Combine(calvyfolder, "versions");
        var latestVersionPath = Path.Combine(versionsPath, latestVersion);

        bool installFiles = false;
        if (args.ContainsKey("forceinstall") ? args["forceinstall"] == "true" : false)
        {
            installFiles = true;
        } 
        else
        {
            try
            {
                if (!Directory.Exists(calvyfolder)) installFiles = true;
                if (!Directory.Exists(versionsPath)) installFiles = true;
                if (!Directory.Exists(latestVersionPath)) installFiles = true;
            }
            catch (Exception ex)
            {
                Log.Fatal($"Failed to check/create directories. Error: {ex.Message}");
                Log.CloseAndFlush();
                return;
            }
        }

        if (installFiles)
        {
            Log.Information("Installing client... do not close this window!");
            
            string tempFolder = Path.Combine(Path.GetTempPath(), "CalvyTemp");
            if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

            if (Directory.Exists(latestVersionPath)) Directory.Delete(latestVersionPath, true);
            CreateFolders(calvyfolder, versionsPath, latestVersionPath);

            string ContentUrl = "https://setup.calvity.ru/" + latestVersion + "/content.zip";
            string PlatformContentUrl = "https://setup.calvity.ru/" + latestVersion + "/platformcontent.zip";
            string ShadersUrl = "https://setup.calvity.ru/" + latestVersion + "/shaders.zip";
            string MiscUrl = "https://setup.calvity.ru/" + latestVersion + "/misc.zip";
            string FinalActualAppUrl = "https://setup.calvity.ru/" + latestVersion + "/robloxapp.zip";


            string downloadRes = "";
            Log.Information("downloading platformcontent.zip...");
            downloadRes = await DownloadAndUnZip(client, PlatformContentUrl, Path.Combine(tempFolder, "platformcontent.zip"), latestVersionPath);
            Log.Information(downloadRes);


            Log.Information("downloading content.zip...");
            downloadRes = await DownloadAndUnZip(client, ContentUrl, Path.Combine(tempFolder, "content.zip"), latestVersionPath);
            Log.Information(downloadRes);


            Log.Information("downloading shaders.zip...");
            downloadRes = await DownloadAndUnZip(client, ShadersUrl, Path.Combine(tempFolder, "shaders.zip"), latestVersionPath);
            Log.Information(downloadRes);


            Log.Information("downloading misc.zip..."); // in csharp u need to close lines with semicolon 
            downloadRes = await DownloadAndUnZip(client, MiscUrl, Path.Combine(tempFolder, "misc.zip"), latestVersionPath);
            Log.Information(downloadRes);


            Log.Information("downloading robloxapp.zip...");
            downloadRes = await DownloadAndUnZip(client, FinalActualAppUrl, Path.Combine(tempFolder, "robloxapp.zip"), latestVersionPath);
            Log.Information(downloadRes);



            Log.Information("installed");
            /*Log.CloseAndFlush();
            return; commented bc it should still proceed with game launch!*/
        }

        // claby

        if (!args.ContainsKey("gameinfo"))
        {
            Log.Fatal("arg 'gameinfo' is missing.");
            Log.CloseAndFlush();
            return;
        }

        if (!args.ContainsKey("placelauncherurl"))
        {
            Log.Fatal("arg 'placelauncherurl' is missing.");
            Log.CloseAndFlush();
            return;
        }

        Log.Information("launching calvity...");
        var RobloxApp = Path.Combine(latestVersionPath, "RobloxPlayerBeta.exe");
        var launchArgs = $"-a \"http://calvity.ru/Login/Negotiate.ashx\" -t \"{args["gameinfo"]}\" -j \"{args["placelauncherurl"]}\"";

        ProcessStartInfo procStartInfo = new ProcessStartInfo(RobloxApp, launchArgs) { };
        Process proc = new Process();
        proc.StartInfo = procStartInfo;
        proc.Start();
        Log.Information("launched!");

        Log.CloseAndFlush();
    }
}