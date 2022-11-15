using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace WinLaunchUpdate
{
    class ProgressEventArgs
    {
        public string Status;
        public double Progress;
    }

    internal class Updater
    {
        public static event EventHandler<ProgressEventArgs> Progress;

        static string? UpdateZipPath;
        static string? UpdateTempFilesPath;
        static string? InstallPath;

        public static bool CheckForUpdate(out string updateURL, out string updateVersion)
        {
            updateVersion = null;
            updateURL = null;

            try
            {
                string UpdateInfoURL = "http://bit.ly/1sjlwOI";

                using (var wc = new WebClient())
                {
                    string xml = wc.DownloadString(UpdateInfoURL);

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(xml);

                    updateVersion = doc.GetElementsByTagName("version")[0].InnerText;
                    updateURL = doc.GetElementsByTagName("url")[0].InnerText;

                    return true;
                }
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static void UnZipFiles(string zipPathAndFile, string outputFolder, bool deleteZipFile)
        {
            Progress(null, new ProgressEventArgs() { Status = "Unpacking", Progress = 0.7 });

            using (var zipArchive = ZipFile.OpenRead(zipPathAndFile))
            {
                zipArchive.ExtractToDirectory(outputFolder, true);
            }

            if (deleteZipFile)
                File.Delete(zipPathAndFile);
        }

        public static bool CopyDirectory(string SourcePath, string DestinationPath, bool OverwriteExisting)
        {
            bool ret = false;
            try
            {
                SourcePath = SourcePath.EndsWith(@"\") ? SourcePath : SourcePath + @"\";
                DestinationPath = DestinationPath.EndsWith(@"\") ? DestinationPath : DestinationPath + @"\";

                if (Directory.Exists(SourcePath))
                {
                    if (Directory.Exists(DestinationPath) == false)
                        Directory.CreateDirectory(DestinationPath);

                    foreach (string fls in Directory.GetFiles(SourcePath))
                    {
                        try
                        {
                            FileInfo flinfo = new FileInfo(fls);
                            flinfo.CopyTo(DestinationPath + flinfo.Name, OverwriteExisting);
                        }
                        catch { }
                    }

                    foreach (string drs in Directory.GetDirectories(SourcePath))
                    {
                        DirectoryInfo drinfo = new DirectoryInfo(drs);
                        if (CopyDirectory(drs, DestinationPath + drinfo.Name, OverwriteExisting) == false)
                            ret = false;
                    }
                }
                ret = true;
            }
            catch (Exception ex)
            {
                ret = false;
            }
            return ret;
        }

        static void ExitUpdate()
        {
            //restart WinLaunch
            string WinLaunchPath = System.IO.Path.Combine(InstallPath, "WinLaunch.exe");

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.FileName = WinLaunchPath;

            if (App.Silent)
                startInfo.Arguments = "-hide";

            Process.Start(startInfo);

            Environment.Exit(0);
        }

        public static void Update()
        {
            string updateURL = "";
            string updateVersion = "";

            //winlaunch install path
            InstallPath = AppDomain.CurrentDomain.BaseDirectory;

            if (!CheckForUpdate(out updateURL, out updateVersion))
            {
                //no update to install
                ExitUpdate();
                return;
            }

            //make sure WinLaunch AppData exists
            string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch");
            if (!System.IO.Directory.Exists(appData))
            {
                System.IO.Directory.CreateDirectory(appData);
            }

            //temp paths
            UpdateZipPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch\\Update " + updateVersion + ".zip");
            UpdateTempFilesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch\\Update " + updateVersion);

            //download update 
            using (var wc = new WebClient())
            {
                wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += Wc_DownloadFileCompleted;

                wc.DownloadFileAsync(new Uri(updateURL), UpdateZipPath);
            }
        }

        private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Progress(null, new ProgressEventArgs() { Status = "Downloading", Progress = e.ProgressPercentage * 0.7 });
        }

        private static void Wc_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            //unzip to temp
            if (Directory.Exists(UpdateTempFilesPath))
                Directory.Delete(UpdateTempFilesPath, true);

            UnZipFiles(UpdateZipPath, UpdateTempFilesPath, true);

            //kill WinLaunch if running
            Process[] p = Process.GetProcessesByName("WinLaunch");
            if (p.Length > 0)
            {
                foreach (var process in p)
                {
                    process.Kill();
                }   

                //wait for all processes to exit
                Thread.Sleep(3000);
            }

            Progress(null, new ProgressEventArgs() { Status = "Copying", Progress = 0.9 });

            //copy to install dir
            CopyDirectory(UpdateTempFilesPath, InstallPath, true);

            Progress(null, new ProgressEventArgs() { Status = "Cleaning Up", Progress = 1.0 });

            //remove temp
            if (File.Exists(UpdateZipPath))
                File.Delete(UpdateZipPath);

            if (Directory.Exists(UpdateTempFilesPath))
                Directory.Delete(UpdateTempFilesPath, true);

            ExitUpdate();
            return;
        }
    }
}
