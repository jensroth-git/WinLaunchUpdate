using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace WinLaunchUpdate
{
    class Program
    {
        public static bool CheckForUpdate(out string updateURL, out string updateVersion)
        {
            updateVersion = null;
            updateURL = null;
            string updateSignature = null;

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
                    updateSignature = doc.GetElementsByTagName("signature")[0].InnerText;

                    //verify signature
                    string unsignedMessage = updateURL + updateVersion;
                    byte[] unsignedMessageBytes = System.Text.Encoding.Unicode.GetBytes(unsignedMessage);
                    byte[] signedMessageBytes = Convert.FromBase64String(updateSignature);

                    //create RSA public key
                    string pubKeyString = "<RSAKeyValue><Modulus>nPnBFiUsgdANJct8U9CgFLMh0ygdBw8PiZ7G9eBn1K5g9CMlLAaIccRMXP+jl5OZ4fRs22DfiYhMYqkcF+pry31cP3osKlTx0/WsFVonuUfvm4urfM9KT8+nZwJ+37kHcq1f6MHdmb4dbS57XFWiBFWFmPRKccpkIgiXjgrh5JzBBvBS7Ig88M7eUTo/laX6etmMwAodIzPCDswILaoWLhu3QVKmO81Hci5EtREmjcnS9TWMJ6Czdh3/Z1fEAPJiQB2wTxj/CpyH7B+pS0Y/qA/4AqYgH/eTbnk7JHkmhkBSyPcA4Xy9yJrljhws/v9zWcARtSDSz3BEr+QPGnoPEQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
                    RSACryptoServiceProvider publicRSA = new RSACryptoServiceProvider();
                    publicRSA.FromXmlString(pubKeyString);

                    if (!publicRSA.VerifyData(unsignedMessageBytes, CryptoConfig.MapNameToOID("SHA512"), signedMessageBytes))
                    {
                        //invalid signature
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static void UnZipFiles(string zipPathAndFile, string outputFolder, string password, bool deleteZipFile)
        {
            ZipInputStream s = new ZipInputStream(File.OpenRead(zipPathAndFile));

            if (password != null && password != String.Empty)
                s.Password = password;

            ZipEntry theEntry;
            string tmpEntry = String.Empty;
            while ((theEntry = s.GetNextEntry()) != null)
            {
                string directoryName = outputFolder;
                string fileName = Path.GetFileName(theEntry.Name);

                // create directory 
                if (directoryName != "")
                {
                    Directory.CreateDirectory(directoryName);
                }

                if (fileName != String.Empty)
                {
                    if (theEntry.Name.IndexOf(".ini") < 0)
                    {
                        string fullPath = directoryName + "\\" + theEntry.Name;
                        fullPath = fullPath.Replace("\\ ", "\\");
                        string fullDirPath = Path.GetDirectoryName(fullPath);

                        if (!Directory.Exists(fullDirPath))
                            Directory.CreateDirectory(fullDirPath);

                        FileStream streamWriter = File.Create(fullPath);
                        int size = 2048;
                        byte[] data = new byte[2048];

                        while (true)
                        {
                            size = s.Read(data, 0, data.Length);

                            if (size > 0)
                            {
                                streamWriter.Write(data, 0, size);
                            }
                            else
                            {
                                break;
                            }
                        }

                        streamWriter.Close();
                    }
                }
            }

            s.Close();

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
            string InstallPath = Assembly.GetExecutingAssembly().Location;
            InstallPath = System.IO.Path.GetDirectoryName(InstallPath);

            string WinLaunchPath = System.IO.Path.Combine(InstallPath, "WinLaunch.exe");
            Process.Start(WinLaunchPath);

            Environment.Exit(0);
        }

        static void Main(string[] args)
        {
            string updateURL = "";
            string updateVersion = "";

            if(!CheckForUpdate(out updateURL, out updateVersion))
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
            string UpdateZipPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch\\Update " + updateVersion + ".zip");
            string UpdateTempFilesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLaunch\\Update " + updateVersion);

            //winlaunch install path
            string InstallPath = Assembly.GetExecutingAssembly().Location;
            InstallPath = System.IO.Path.GetDirectoryName(InstallPath);


            //download update 
            using (var wc = new WebClient())
            {
                wc.DownloadFile(updateURL, UpdateZipPath);
            }

            //unzip to temp
            if (Directory.Exists(UpdateTempFilesPath))
                Directory.Delete(UpdateTempFilesPath, true);

            UnZipFiles(UpdateZipPath, UpdateTempFilesPath, null, true);

            //kill WinLaunch if running
            Process[] p = Process.GetProcessesByName("WinLaunch");
            if(p.Length > 0)
            { 
                foreach (var process in p)
                {
                    process.Kill();
                }

                //wait for all processes to exit
                Thread.Sleep(3000);
            }

            //copy to install dir
            if(!CopyDirectory(UpdateTempFilesPath, InstallPath, true))
            {
                //error copying files
                if (File.Exists(UpdateZipPath))
                    File.Delete(UpdateZipPath);

                if (Directory.Exists(UpdateTempFilesPath))
                    Directory.Delete(UpdateTempFilesPath, true);

                ExitUpdate();
                return;
            }

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
