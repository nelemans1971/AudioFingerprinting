#region License
// Copyright (c) 2015-2017 Stichting Centrale Discotheek Rotterdam.
// 
// website: https://www.muziekweb.nl
// e-mail:  info@muziekweb.nl
//
// This code is under MIT licence, you can find the complete file here: 
// LICENSE.MIT
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CDR.Logging;

namespace RadioChannelWebService
{
    class ReceiveIndex
    {
        private Thread T = null;
        private bool running = false;
        // Zie GetTemporaryDirectory() waar de map wordt gegenereerd, vanwege tijd is het het beste om dezelfde drive te 
        // gebruiken als waar nu de indexen al staan (dus dubbele ruimte nodig)
        // dan kan namelijk een switch snel gebeuren (webservice stoppen, oude index verwijderen, 
        // nieuwe index verplaatsen en webservice weer actieveren)
        private string basePath = @"H:\";

        public ReceiveIndex(string basePath)
        {
            this.basePath = basePath;

            ThreadStart Ts = new ThreadStart(ReceiveTCP);
            T = new Thread(Ts);
            T.Start();
        }
        
        public void Stop()
        {
            running = false;
            T.Abort();
            T = null;
        }

        private void ReceiveTCP()
        {
            running = true;
            string indexDir = "";

            TcpListener Listener = null;
            try
            {
                Listener = new TcpListener(IPAddress.Any, 54236);
                Listener.Start();
                while (running)
                {
                    indexDir = "";
                    if (Listener.Pending())
                    {
                        try
                        {
                            TcpClient client = Listener.AcceptTcpClient();
                            using (NetworkStream netstream = client.GetStream())
                            {
                                netstream.ReadTimeout = 5000; // 5 seconden

                                // Create temproray directory where index files are received
                                indexDir = GetTemporaryDirectory();

                                byte[] buffer = new byte[1024];

                                string filename = "";
                                long filesize = -1;

                                int recBytes;
                                bool bussy = false;
                                while (client.Connected)
                                {
                                    // Get file size for transfer
                                    recBytes = netstream.Read(buffer, 0, 8);
                                    if (recBytes != 8)
                                    {
                                        // error!
                                        break;
                                    }
                                    filesize = BitConverter.ToInt64(buffer, 0);
                                    // Zijn we klaar met het versturen van bestanden?
                                    if (filesize < 0)
                                    {
                                        // Ja, we zijn klaar
                                        bussy = false; // expliciet nog een keer doen
                                        break;
                                    }
                                    bussy = true;

                                    // Get length filename
                                    recBytes = netstream.Read(buffer, 0, 4);
                                    if (recBytes != 4)
                                    {
                                        // error!
                                        break;
                                    }
                                    int strLen = BitConverter.ToInt32(buffer, 0);
                                    if (strLen > 1024)
                                    {
                                        strLen = 1024;
                                    }

                                    // We assume never larger than 1024!
                                    recBytes = netstream.Read(buffer, 0, strLen);
                                    if (recBytes != strLen)
                                    {
                                        // error!
                                        break;
                                    }
                                    filename = Encoding.UTF8.GetString(buffer, 0, strLen);

                                    using (FileStream fs = new FileStream(Path.Combine(indexDir, filename), FileMode.OpenOrCreate, FileAccess.Write))
                                    {
                                        // now download file upto "filesize" bytes
                                        while (client.Connected && filesize > 0)
                                        {
                                            int readBytes = buffer.Length;
                                            if (readBytes > filesize)
                                            {
                                                readBytes = Convert.ToInt32(filesize);
                                            }
                                            recBytes = netstream.Read(buffer, 0, readBytes);
                                            if (recBytes <= 0)
                                            {
                                                break;
                                            }
                                            fs.Write(buffer, 0, recBytes);
                                            filesize -= recBytes;
                                        } //while
                                    } //using fs
                                    // Zorg dat files handles gesloten
                                    GC.WaitForPendingFinalizers();
                                    bussy = false;
                                }


                                if (!bussy && LuceneIndexComplete(indexDir))
                                {
                                    LuceneIndexes.ReloadSubFingerIndex(indexDir);
                                    CDRLogger.Logger.LogInfo("Nieuwe subfinger index wordt geactiveerd");
                                }
                                else
                                {
                                    ClearAndRemoveFolder(indexDir);
                                }
                            } //using
                            try
                            {
                                client.Close();
                            }
                            catch
                            {
                            }
                        }
                        catch
                        {
                            // Er gaat wat mis (verbinding verbroken of zo)
                            ClearAndRemoveFolder(indexDir);
                        }
                    } // if pending

                    // Wait 100 ms for check for new connection
                    Thread.Sleep(100);
                } //while
            }
            catch (Exception e)
            {
                CDRLogger.Logger.LogError(e);
            }
        }

        private bool LuceneIndexComplete(string indexDir)
        {
            int countFiles = 0;
            try
            {
                DirectoryInfo dir = new DirectoryInfo(indexDir);
                foreach (FileInfo fi in dir.GetFiles())
                {
                    switch (Path.GetExtension(fi.FullName).ToLower())
                    {
                        case ".fdt":
                        case ".fdx":
                        case ".fnm":
                        case ".frq":
                        case ".tii":
                        case ".tis":
                        case ".gen":
                            countFiles++;
                            break;
                    } //switch
                    if (Path.GetFileName(fi.FullName).ToLower().Contains("segments_"))
                    {
                        countFiles++;
                    }
                }
            }
            catch
            {
            }

            return (countFiles >= 8);
        }

        private string GetTemporaryDirectory()
        {
            //Path.GetTempPath()
            string tempDirectory = Path.Combine(basePath, Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        private void ClearAndRemoveFolder(string folderName)
        {
            if (string.IsNullOrEmpty(folderName) || folderName.Trim().Length <= 3)
            {
                return;
            }

            try
            {
                DirectoryInfo dir = new DirectoryInfo(folderName);
                foreach (FileInfo fi in dir.GetFiles())
                {
                    fi.IsReadOnly = false;
                    fi.Delete();
                }

                foreach (DirectoryInfo di in dir.GetDirectories())
                {
                    ClearAndRemoveFolder(di.FullName);
                    di.Delete();
                }

                Directory.Delete(folderName);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Get Date Modified for subfingerprint index
        /// </summary>
        public static DateTime DateTimeOfIndex
        {
            get
            {
                DateTime lastModified = System.IO.File.GetLastWriteTime(Path.Combine(LuceneIndexes.SubFingerprintLocation, "segments.gen"));
                
                return lastModified;
            }
        }
    }
}