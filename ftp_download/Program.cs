using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace ftp_download
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("【取得連線字串...】");
            Console.WriteLine("==============================================================");
            string ftpUrl = ConfigurationSettings.AppSettings["ftpUrl"].ToString();
            string ftpUser = ConfigurationSettings.AppSettings["ftpUser"].ToString();
            string ftpPass = ConfigurationSettings.AppSettings["ftpPass"].ToString();
            string targetDir = ConfigurationSettings.AppSettings["targetDir"].ToString();
            string mode = ConfigurationSettings.AppSettings["mode"].ToString();
            NetworkCredential credentials = new NetworkCredential(ftpUser, ftpPass);

            Console.WriteLine("連線來源：\t" + ftpUrl);
            Console.WriteLine("使用者名稱：\t" + ftpUser);
            Console.WriteLine("使用者密碼：\t" + ftpPass);
            Console.WriteLine("目標路徑：\t" + targetDir);
            Console.WriteLine("作業方式：\t" + (mode == "U" ? "Upload" : "Download"));
            Console.WriteLine("==============================================================");

            switch (mode)
            {
                case "U"://上傳，只針對單層
                    UploadToFTP(ftpUrl, credentials, targetDir, false);
                    break;
                case "D"://下載
                    DownloadFtpDirectory(ftpUrl, credentials, targetDir, false);
                    break;

            }
            Console.WriteLine("==============================================================");
            Console.WriteLine("【工作完成...】");
            Console.WriteLine("");
            Console.WriteLine("倒數3秒自動關閉...");
            System.Threading.Thread.Sleep(3000);//停顿3秒
            Environment.Exit(0);
        }
        static void DownloadFtpDirectory(string url, NetworkCredential credentials, string localPath, bool UsePassive/*主被動模式*/)
        {
            try
            {
                if (!Directory.Exists(localPath))
                    Directory.CreateDirectory(localPath);

                FtpWebRequest listRequest = (FtpWebRequest)WebRequest.Create(url);
                listRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                listRequest.Credentials = credentials;
                listRequest.UsePassive = UsePassive;

                List<string> lines = new List<string>();

                using (FtpWebResponse listResponse = (FtpWebResponse)listRequest.GetResponse())
                using (Stream listStream = listResponse.GetResponseStream())
                using (StreamReader listReader = new StreamReader(listStream,Encoding.Default))
                {
                    while (!listReader.EndOfStream)
                    {
                        lines.Add(listReader.ReadLine());
                    }
                }

                foreach (string line in lines)
                {
                    string[] tokens =
                        line.Split(new[] { ' ' }, 9, StringSplitOptions.RemoveEmptyEntries);
                    string name = tokens[8];
                    string permissions = tokens[0];

                    string localFilePath = Path.Combine(localPath, name);
                    string fileUrl = url + name;

                    //篩選shp檔案
                    if (!GetShpFile(localFilePath))
                        continue;

                    if (permissions[0] == 'd')
                    {
                        if (!Directory.Exists(localFilePath))
                        {
                            Directory.CreateDirectory(localFilePath);
                        }
                        Console.WriteLine("建立資料夾：" + localFilePath);
                        DownloadFtpDirectory(fileUrl + "/", credentials, localFilePath, false);
                    }
                    else
                    {
                        FtpWebRequest downloadRequest = (FtpWebRequest)WebRequest.Create(fileUrl);
                        downloadRequest.Method = WebRequestMethods.Ftp.DownloadFile;
                        downloadRequest.Credentials = credentials;
                        downloadRequest.UsePassive = UsePassive;

                        using (FtpWebResponse downloadResponse =
                                  (FtpWebResponse)downloadRequest.GetResponse())
                        using (Stream sourceStream = downloadResponse.GetResponseStream())
                        using (Stream targetStream = File.Create(localFilePath))
                        {
                            byte[] buffer = new byte[10240];
                            int read;
                            while ((read = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                targetStream.Write(buffer, 0, read);
                            }
                        }
                        Console.WriteLine("建立資料：" + localFilePath);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
        }
        static void UploadToFTP(string url, NetworkCredential credentials, string localPath, bool UsePassive/*主被動模式*/)
        {
            try
            {
                foreach (string f in Directory.GetFiles(localPath))
                {
                    string uploadFtpFileName = url + Path.GetFileName(f);
                    FtpWebRequest listRequest = (FtpWebRequest)WebRequest.Create(uploadFtpFileName);
                    listRequest.Method = WebRequestMethods.Ftp.UploadFile;
                    listRequest.Credentials = credentials;
                    listRequest.UsePassive = UsePassive;
                    listRequest.UseBinary = true;
                    listRequest.KeepAlive = true;

                    using (FileStream fs = File.OpenRead(f))
                    {
                        byte[] buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);
                        fs.Close();
                        using (var requestStream = listRequest.GetRequestStream())
                        {
                            requestStream.Write(buffer, 0, buffer.Length);
                        }
                        Console.WriteLine("建立資料：" + uploadFtpFileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
        }
        static bool GetShpFile(string localPath)
        {
            bool success = false;
            string ext = Path.GetExtension(localPath);
            if(ext.Contains(".shp") || ext.Contains(".shx") || ext.Contains(".dbf"))
                success = true;

            return success;
        }
    }
}
