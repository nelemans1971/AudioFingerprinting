using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;

namespace AudioFingerprint.Audio
{
    /// <summary>
    /// Get an Ice or shotcast stream
    /// </summary>
    public class IceCastStream : IDisposable
    {
        private IntPtr _disposed = IntPtr.Zero;

        private Thread thread = null;
        private bool threadStarted = false;
        public EventWaitHandle waitEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        private string _metadata;
        private SongInfo _currentSong;

        public static event MessageLogged OnMessageLogged;
        public event MetadataChanged OnMetadataChanged;
        public event CurrentSongChanged OnCurrentSongChanged;
        public event StreamUpdate OnStreamUpdate;

        private SynchronizationContext synchronizationContext = null;
        private object lockObject = new object();
        private CircularBlockBuffer cbbChunkAudio = null; // for chunking the audio stream (not for listening to it)
        private AudioFingerprint.Audio.BassLifetimeManager lifetimeManager;
        private GCHandle? gcHandle = null;
        private float[] inSecBuffer = null; // buffer 1 second of pcm data which we can send then
        private int inSecPtr = 0;

        private int listenHandle = 0;
        private bool listen = false;
        private CircularBlockBuffer cbbListenAudio = null; // for listining to audiostream (when no speakers are on we keepat 16kb buffer)

        public IceCastStream(string url, bool useMainThreadForCallbacks = true)
        {
            // in a winform and wpf enviroment this will be set
            if (useMainThreadForCallbacks)
            {
                this.synchronizationContext = SynchronizationContext.Current;
            }
            
            BassNetInitialize();

            // When it's a playlist get, download it and use the first url
            if (url.ToUpper().Contains(".PLS"))
            {
                url = ConvertPls2Url(url);
            }
            if (url.ToUpper().Contains("CONCERTZENDER"))
            {
                url = ConvertPls2Url(url);
            }
            else if (url.ToUpper().Contains(".ASX"))
            {
                url = ConvertASX2Url(url);
            }
            else if (url.ToUpper().Contains(".M3U"))
            {
                url = ConvertM3U2Url(url);
            }
            else if (url.ToUpper().Contains("HTTPS:"))
            {
                url = ConverHTTPS2Url(url);
            }

            this.Url = url;
        }
        
        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            KillThread();

            // Thread-safe single disposal
            if (Interlocked.Exchange(ref _disposed, (IntPtr)1) != IntPtr.Zero)
            {
                return;
            }

            OnMessageLogged = null;

            lifetimeManager.Dispose();
            lifetimeManager = null;
        }

        private string ConvertPls2Url(string plsUrl)
        {
            List<string> iniPLS = new List<string>();
            
            WebClient wc = new WebClient();
            try
            {
                using (MemoryStream stream = new MemoryStream(wc.DownloadData(plsUrl)))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    List<string> iniList = new List<string>();
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (reader.Peek() != -1)
                        {
                            string line = reader.ReadLine();
                            iniList.Add(line);
                        }
                    }
                    MemoryStream ms = new MemoryStream();
                    foreach (string line in iniList)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(line);
                        ms.Write(bytes, 0, bytes.Length);
                        ms.WriteByte(13);
                        ms.WriteByte(10);
                    }
                    ms.Seek(0, SeekOrigin.Begin);

                    CDR.IniFiles2.IniFile ini = CDR.IniFiles2.IniFile.FromStream(new CDR.IniFiles2.IniFileReader(ms));

                    // Wel juist eversie van pls file
                    if (Convert.ToInt32(ini["playlist"]["Version"]) >= 1)
                    {
                        if (Convert.ToInt32(ini["playlist"]["NumberOfEntries"]) > 0)
                        {
                            return ini["playlist"]["File1"].ToString();
                        }
                    }
                    else if (Convert.ToInt32(ini["playlist"]["NumberOfEntries"]) > 0)
                    {
                        return ini["playlist"]["File1"].ToString();
                    }
                } //using
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return string.Empty;
        }

        private string ConvertASX2Url(string plsUrl)
        {
            List<string> iniPLS = new List<string>();

            WebClient wc = new WebClient();
            try
            {
                using (MemoryStream stream = new MemoryStream(wc.DownloadData(plsUrl)))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    StringBuilder sb = new StringBuilder(1024);
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (reader.Peek() != -1)
                        {
                            string line = reader.ReadLine();
                            sb.Append(line);
                            sb.Append("\r\n");
                        }
                    }
                   
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(sb.ToString());
                    XmlElement xRef = xmlDoc.GetElementsByTagName("ref")[0] as XmlElement;
                    return xRef.Attributes["href"].Value.ToString();                    
                } //using
            }
            catch
            {
            }

            return string.Empty;
        }

        private string ConvertM3U2Url(string plsUrl)
        {
            List<string> iniPLS = new List<string>();

            WebClient wc = new WebClient();
            try
            {
                using (MemoryStream stream = new MemoryStream(wc.DownloadData(plsUrl)))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    string line = "";
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (reader.Peek() != -1)
                        {
                            string tmpLine = reader.ReadLine().Trim();
                            if (tmpLine.Length > 0 && tmpLine[0] != '#')
                            {
                                line = tmpLine;
                                break;
                            }
                        }
                    }

                    return line;
                } //using
            }
            catch
            {
            }

            return string.Empty;
        }

        /// <summary>
        /// Streaming gebuert niet in https dus die levert een redirect op
        /// </summary>
        private string ConverHTTPS2Url(string url)
        {
            WebClient wc = new WebClient();
            try
            {
                using (MemoryStream stream = new MemoryStream(wc.DownloadData(url)))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    string line = "";
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (reader.Peek() != -1)
                        {
                            string tmpLine = reader.ReadLine().Trim();
                            if (tmpLine.Length > 0 && tmpLine[0] != '#')
                            {
                                line = tmpLine;
                                break;
                            }
                        }
                    }

                    return line;
                } //using
            }
            catch
            {
            }

            return string.Empty;
        }

        public string Url
        {
            get;
            private set;
        }

        public bool Running
        {
            get
            {
                return threadStarted;
            }
        }

        public bool Listen
        {
            get
            {
                return listen;
            }
            set
            {
                listen = value;
            }
        }

        public string Metadata
        {
            get
            {
                return _metadata;
            }
            private set
            {
                if (OnMetadataChanged != null)
                {
                    DoCallback(OnMetadataChanged, this, new MetadataEventArgs(_metadata, value));
                }
                _metadata = value;
            }
        }

        public SongInfo CurrentSong
        {
            get
            {
                return _currentSong;
            }
            private set
            {
                if (OnCurrentSongChanged != null)
                {
                    DoCallback(OnCurrentSongChanged, this, new CurrentSongEventArgs(_currentSong, value));
                }
                _currentSong = value;
            }
        }

        public void Start()
        {
            if (!string.IsNullOrEmpty(Url))
            {
                if (thread != null)
                {
                    KillThread();
                }

                threadStarted = false;

                // Start de thread
                ThreadStart st = new ThreadStart(GetHttpStream); // create a thread and attach to the object
                thread = new Thread(st);
                thread.IsBackground = true;
                thread.Start();
            }
        }
        
        private void KillThread()
        {
            try
            {
                if (thread != null)
                {
                    // Stop de thread's
                    if (threadStarted)
                    {
                        threadStarted = false;

                        // Maak thread wakker
                        waitEvent.Set(); // start de thread mocht de thread in een slaap toestand staan
                        // Wacht max 1 minuut om het ding te laten stoppen
                        thread.Join(new TimeSpan(0, 1, 0));
                    }
                    thread.Abort();
                    thread = null;
                }
            }
            catch
            {
            }
        }

        private void GetHttpStream()
        {
            threadStarted = true;
            do
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
                    request.Headers.Clear();
                    request.Headers.Add("Icy-MetaData", "1");
                    request.KeepAlive = false;
                    request.UserAgent = "VLC media player";
                    cbbChunkAudio = new CircularBlockBuffer();
                    cbbListenAudio = new CircularBlockBuffer();
                    inSecBuffer = null; // buffer 1 second of pcm data which we can send then
                    inSecPtr = 0;

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        // get the position of metadata (if available)
                        int metaInt = int.Parse(response.Headers["Icy-MetaInt"]);
                        int receivedBytes = 0;

                        using (Stream socketStream = response.GetResponseStream())
                        {
                            try
                            {
                                socketStream.ReadTimeout = 20 * 1000; // 20 second timeout (throws exception when time expires)
                                while (Running)
                                {
                                    byte[] buffer = new byte[16384];
                                    if (receivedBytes == metaInt)
                                    {
                                        int metaLen = socketStream.ReadByte();
                                        if (metaLen > 0)
                                        {
                                            byte[] metaInfo = new byte[metaLen * 16];
                                            int len = 0;
                                            while ((len += socketStream.Read(metaInfo, len, metaInfo.Length - len)) < metaInfo.Length)
                                                ;
                                            ParseMetaInfo(metaInfo);
                                        }
                                        receivedBytes = 0;
                                    }

                                    int bytesLeft = ((metaInt - receivedBytes) > buffer.Length) ? buffer.Length : (metaInt - receivedBytes);
                                    int result = socketStream.Read(buffer, 0, bytesLeft);
                                    receivedBytes += result;

                                    // Doe nu wat met de data
                                    ProcessStreamData(buffer, 0, result);
                                    AudioToSpeaker(buffer, 0, result);
                                } // while running
                            }
                            finally
                            {
                                // Needed so the using call doesn't "hang" the code (http://stackoverflow.com/questions/5855088/httpwebresponse-getresponsestream-hanging-in-dispose)
                                request.Abort();
                            }
                        } //using
                    } //using httpresponse
                }
                catch (IOException ex)
                {
                    IceCastStream.Log(this, string.Format("Handled IOException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace));
                }
                catch (SocketException ex)
                {
                    IceCastStream.Log(this, string.Format("Handled SocketException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace));
                }
                catch (WebException ex)
                {
                    IceCastStream.Log(this, string.Format("Handled WebException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace));
                }
                finally
                {
                    if (inputHandle != 0)
                    {
                        Bass.BASS_StreamFree(inputHandle);
                        inputHandle = 0;
                    }
                    if (inputHandle != 0)
                    {
                        Bass.BASS_StreamFree(mixerHandle);
                        mixerHandle = 0;
                    }
                    if (listenHandle != 0)
                    {
                        Bass.BASS_ChannelStop(listenHandle);
                        Bass.BASS_StreamFree(listenHandle);
                        listenHandle = 0;
                    }


                    cbbChunkAudio = null;
                    inSecBuffer = null;
                    inSecPtr = 0;
                    cbbListenAudio = null;
                    listen = false;
                }

                // Wait 10 seconds before trying to connect again
                int countSleep = 0;
                while (Running && countSleep < 100)
                {
                    Thread.Sleep(100);
                    countSleep++;
                }
            } while (Running);
        }

        /// <summary>
        /// Parses the received Meta Info
        /// </summary>
        /// <param name="metaInfo"></param>
        private void ParseMetaInfo(byte[] metaInfo)
        {
            string metaString = Encoding.ASCII.GetString(metaInfo);
            Metadata = metaString;
            UpdateCurrentSong(this, new MetadataEventArgs(Metadata, metaString));
        }

        private void UpdateCurrentSong(object sender, MetadataEventArgs args)
        {
            const string metadataSongPattern = @"StreamTitle='(?<artist>.+?) - (?<title>.+?)';";
            Match match = Regex.Match(args.NewMetadata, metadataSongPattern);
            if (match.Success)
            {
                CurrentSong = new SongInfo(match.Groups["artist"].Value, match.Groups["title"].Value);
            }
        }

        private void ProcessStreamData(byte[] buffer, int offset, int length)
        {
            if (length < 1)
            {
                return;
            }

            // Write compressed audio data to Circulairbuffer
            byte[] data = new byte[length];
            Buffer.BlockCopy(buffer, offset, data, 0, length);
            lock (lockObject)
            {
                cbbChunkAudio.Write(buffer, offset, length);
            }


            // If basshandle wasn't opened then open it now (when there is enough data. Min 4010 bytes)
            if (inputHandle == 0 && cbbChunkAudio.UsedBytes >= 8000)
            {
                // bass needs for mp3 atleast 4000 bytes before is can play
                inputHandle = Bass.BASS_StreamCreateFileUser(BASSStreamSystem.STREAMFILE_BUFFERPUSH, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT, bassFileProcs, GCHandle.ToIntPtr((GCHandle)gcHandle));
                Bass.BASS_ChannelSetSync(inputHandle, BASSSync.BASS_SYNC_STALL, 0, bassStalledSync, GCHandle.ToIntPtr((GCHandle)gcHandle));
                Bass.BASS_ChannelSetSync(inputHandle, BASSSync.BASS_SYNC_END, 0, bassEndSync, GCHandle.ToIntPtr((GCHandle)gcHandle));                

                mixerHandle = BassMix.BASS_Mixer_StreamCreate(44100, 2, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);
                BassMix.BASS_Mixer_StreamAddChannel(mixerHandle, inputHandle, BASSFlag.BASS_MIXER_FILTER | BASSFlag.BASS_MIXER_DOWNMIX);
                Bass.BASS_ChannelPlay(inputHandle, false);
                Bass.BASS_ChannelPlay(mixerHandle, false);
                return;
            }

            // push new audiodata when available (and basstream is opened)
            if (inputHandle != 0)
            {
                // voed bass met audio data zodat deze het naar pcm data kan decompressen
                long bassBufLen = Bass.BASS_StreamGetFilePosition(inputHandle, BASSStreamFilePosition.BASS_FILEPOS_END);
                long bassBufPos = Bass.BASS_StreamGetFilePosition(inputHandle, BASSStreamFilePosition.BASS_FILEPOS_BUFFER);
                int todo = Convert.ToInt32(bassBufLen - bassBufPos);
                if (todo > 0)
                {
                    int count = todo;
                    int writeBytes = 0;
                    while (!cbbChunkAudio.IsEmpty && todo > 0 && writeBytes > -1)
                    {
                        if (count > 16384)
                        {
                            count = 16384;
                        }
                        byte[] tmpBuffer = new byte[count];
                        lock (lockObject)
                        {
                            count = cbbChunkAudio.Read(tmpBuffer, count);
                        }
                        if (count > 0)
                        {
                            writeBytes = Bass.BASS_StreamPutFileData(inputHandle, tmpBuffer, count);
                            todo -= writeBytes;
                        }
                    } //while audio buffer not empty
                }
                ProcessDecompressedData();
            }
        }

        private void ProcessDecompressedData()
        {
            if (mixerHandle != 0)
            {
                int bytesRead = 0;
                do
                {
                    int bufferSizeInBytes = (int)Bass.BASS_ChannelSeconds2Bytes(mixerHandle, 1.0);
                    if (inSecBuffer == null)
                    {
                        inSecBuffer = new float[bufferSizeInBytes / 4]; // we hebben floats
                        inSecPtr = 0;
                    }
                    else
                    {
                        bufferSizeInBytes = (inSecBuffer.Length - inSecPtr) * 4;
                    }

                    float[] buffer = new float[bufferSizeInBytes / 4];
                    bytesRead = Bass.BASS_ChannelGetData(mixerHandle, buffer, bufferSizeInBytes | (int)BASSData.BASS_DATA_FLOAT);
                    if (bytesRead <= 0)
                    {
                        return;
                    }

                    // nu data naar insecbuffer kopieren
                    Buffer.BlockCopy(buffer, 0, inSecBuffer, inSecPtr * 4, bytesRead);
                    inSecPtr += (int)(bytesRead / 4);

                    if (inSecPtr >= inSecBuffer.Length)
                    {
                        if (OnStreamUpdate != null)
                        {
                            DoCallback(OnStreamUpdate, this, new StreamUpdateEventArgs(inSecBuffer));
                        }
                        inSecBuffer = null;
                        inSecPtr = 0;
                    }
                } while (bytesRead > 0);

            } //if basshandle valid
        }

        private void AudioToSpeaker(byte[] buffer, int offset, int length)
        {
            // Write compressed audio data to Circulairbuffer
            lock (lockObject)
            {
                cbbListenAudio.Write(buffer, offset, length);
                // When not listening to audio clear buffer up to 16 kb
                if (listenHandle == 0)
                {
                    // Purge circulair buffer
                    // we only keep 16 kb in memory
                    while (cbbListenAudio.UsedBytes > (16 * 1024))
                    {
                        byte[] dummyBuffer = new byte[1024];
                        cbbListenAudio.Read(dummyBuffer, 1024);
                    } //while
                }
            }

            if (!listen && listenHandle == 0)
            {
                return;
            }

            // Geluid wordt uitgezet
            if (!listen && listenHandle != 0)
            {
                Bass.BASS_ChannelStop(listenHandle);
                Bass.BASS_StreamFree(listenHandle);
                listenHandle = 0;
                return;
            }

            // Geluid wordt aangezet
            if (listen && listenHandle == 0 && cbbListenAudio.UsedBytes >= 8000)
            {
                // bass needs for mp3 atleast 4000 bytes before is can play
                listenHandle = Bass.BASS_StreamCreateFileUser(BASSStreamSystem.STREAMFILE_BUFFERPUSH, BASSFlag.BASS_DEFAULT, bassFileProcsOut, GCHandle.ToIntPtr((GCHandle)gcHandle));
                Bass.BASS_ChannelSetSync(listenHandle, BASSSync.BASS_SYNC_STALL, 0, bassStalledSync, GCHandle.ToIntPtr((GCHandle)gcHandle));
                Bass.BASS_ChannelSetSync(listenHandle, BASSSync.BASS_SYNC_END, 0, bassEndSync, GCHandle.ToIntPtr((GCHandle)gcHandle));
                Bass.BASS_ChannelPlay(listenHandle, false);
            }

            // push new audiodata when available (and basstream is opened)
            if (listenHandle != 0)
            {
                // voed bass met audio data zodat deze het naar pcm data kan decompressen
                long bassBufLen = Bass.BASS_StreamGetFilePosition(listenHandle, BASSStreamFilePosition.BASS_FILEPOS_END);
                long bassBufPos = Bass.BASS_StreamGetFilePosition(listenHandle, BASSStreamFilePosition.BASS_FILEPOS_BUFFER);
                int todo = Convert.ToInt32(bassBufLen - bassBufPos);
                if (todo > 0)
                {
                    int count = todo;
                    int writeBytes = 0;
                    while (!cbbListenAudio.IsEmpty && todo > 0 && writeBytes > -1)
                    {
                        if (count > 16384)
                        {
                            count = 16384;
                        }
                        byte[] tmpBuffer = new byte[count];
                        lock (lockObject)
                        {
                            count = cbbListenAudio.Read(tmpBuffer, count);
                        }
                        if (count > 0)
                        {
                            writeBytes = Bass.BASS_StreamPutFileData(listenHandle, tmpBuffer, count);
                            todo -= writeBytes;
                        }
                    } //while audio buffer not empty
                }
            }
        }

        public static void Log(object sender, string log)
        {
            if (OnMessageLogged != null && sender is IceCastStream)
            {
                (sender as IceCastStream).DoCallback(OnMessageLogged, sender, new MessageLogEventArgs(log));
            }
            else
            {
                System.Diagnostics.Trace.WriteLine(log);
            }
        }

        public void Stop()
        {
            Dispose();
        }

        #region SynchronizationContext Helper

        /// <summary>
        /// If possible will call the user callback function on the 
        /// thread set by SynchronizationContextMethod. This
        /// will most of the time be the UI thread.
        /// </summary>
        private void DoCallback(params object[] objects)
        {
            SynchronizationContext sc;
            lock (lockObject)
            {
                sc = synchronizationContext;
            } //lock
            if (sc != null)
            {
                sc.Post(HandleCallUserCode, objects);
            }
            else
            {
                HandleCallUserCode(objects);
            }
        }

        /// <summary>
        /// Really call the user code with the specified 
        /// parameters
        /// </summary>
        /// <param name="state"></param>
        private void HandleCallUserCode(object state)
        {
            object[] objects = (object[])state;
            
            if (objects[0] is MessageLogged)
            {
                (objects[0] as MessageLogged)((object)objects[1], (MessageLogEventArgs)objects[2]);
            }
            else if (objects[0] is MetadataChanged)
            {
                (objects[0] as MetadataChanged)((object)objects[1], (MetadataEventArgs)objects[2]);
            }
            else if (objects[0] is CurrentSongChanged)
            {
                (objects[0] as CurrentSongChanged)((object)objects[1], (CurrentSongEventArgs)objects[2]);
            }
            else if (objects[0] is StreamUpdate)
            {
                (objects[0] as StreamUpdate)((object)objects[1], (StreamUpdateEventArgs)objects[2]);
            }
        }

        #endregion

        #region Bass required (callback) functions for DECODING
        
        private BASS_FILEPROCS bassFileProcs;
        private BASS_FILEPROCS bassFileProcsOut;

        private SYNCPROC bassStalledSync;
        private SYNCPROC bassEndSync;

        private int inputHandle = 0;
        private int mixerHandle = 0;


        /// <summary>
        /// Initialize the bass library
        /// </summary>
        /// <returns></returns>
        protected bool BassNetInitialize()
        {
            gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);

            // Keep Bass.Net from putting a popup on the screen
            lifetimeManager = new AudioFingerprint.Audio.BassLifetimeManager(false);
            bassFileProcs = new BASS_FILEPROCS(
                new FILECLOSEPROC(_BassCallback_FileProcUserClose),
                new FILELENPROC(_BassCallback_FileProcUserLength),
                new FILEREADPROC(_BassCallback_FileProcUserRead),
                new FILESEEKPROC(_BassCallback_FileProcUserSeek));
            bassStalledSync = new SYNCPROC(_BassCallback_StalledSync);
            bassEndSync = new SYNCPROC(_BassCallback_EndSync);

            bassFileProcsOut = new BASS_FILEPROCS(
                new FILECLOSEPROC(_BassCallback_FileProcUserClose),
                new FILELENPROC(_BassCallback_FileProcUserLength),
                new FILEREADPROC(_BassCallback_FileProcUserReadOut),
                new FILESEEKPROC(_BassCallback_FileProcUserSeek));

            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_BUFFER, 1000 * 4); // 2 seconds buffer (the default of bass also)

            return true;
        }

        private static void _BassCallback_StalledSync(int handle, int channel, int data, IntPtr user)
        {
            if (user != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(user);
                IceCastStream obj = (IceCastStream)gcHandle.Target;
                obj.BassCallback_StalledSync(handle, channel, data, Guid.Empty);
            }
        }

        private void BassCallback_StalledSync(int handle, int channel, int data, Guid newMediaItemGUID)
        {
            Console.WriteLine("STALLED");
        }

        private static void _BassCallback_EndSync(int handle, int channel, int data, IntPtr user)
        {
            if (user != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(user);
                IceCastStream obj = (IceCastStream)gcHandle.Target;
                obj.BassCallback_EndSync(handle, channel, data, Guid.Empty);
            }
        }

        private void BassCallback_EndSync(int handle, int channel, int data, Guid newMediaItemGUID)
        {
            Console.WriteLine("File ended");
        }

        /// <summary>
        /// USED when Stream freed
        /// </summary>
        private static void _BassCallback_FileProcUserClose(IntPtr user)
        {
            return;
        }

        private static long _BassCallback_FileProcUserLength(IntPtr user)
        {
            return 0;
        }

        /// <summary>
        /// Used to bootstrap bass to start playing. It's needs some data so it can detect wat 
        /// type of file it is. After that we can start using "BASS_StreamPutFileData"
        /// to push the remainder of the data to bass. This function will never be called again
        /// </summary>
        private static int _BassCallback_FileProcUserRead(IntPtr buffer, int length, IntPtr user)
        {
            if (user != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(user);
                IceCastStream obj = (IceCastStream)gcHandle.Target;
                return obj.BassCallback_FileProcUserRead(buffer, length);
            }

            return 0;
        }

        /// <summary>
        /// See above but now with class context
        /// </summary>
        private int BassCallback_FileProcUserRead(IntPtr buffer, int length)
        {
            int todo = length;
            lock (lockObject)
            {
                if (cbbChunkAudio != null)
                {
                    byte[] byteBuffer = new byte[length];

                    int count = cbbChunkAudio.Read(byteBuffer, todo);
                    todo -= count;
                    if (count > 0)
                    {
                        // Nu kopieren in unmanaged data
                        Marshal.Copy(byteBuffer, 0, buffer, count);
                    }
                }
            }

            return length - todo;
        }

        /// <summary>
        /// NOT USED just here for reference
        /// </summary>
        private static bool _BassCallback_FileProcUserSeek(long offset, IntPtr user)
        {
            return false;
        }

        #endregion

        #region Bass required (callback) functions for AUDIO output

        /// <summary>
        /// Used to bootstrap bass to start playing. It's needs some data so it can detect wat 
        /// type of file it is. After that we can start using "BASS_StreamPutFileData"
        /// to push the remainder of the data to bass. This function will never be called again
        /// </summary>
        private static int _BassCallback_FileProcUserReadOut(IntPtr buffer, int length, IntPtr user)
        {
            if (user != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(user);
                IceCastStream obj = (IceCastStream)gcHandle.Target;
                return obj.BassCallback_FileProcUserReadOut(buffer, length);
            }

            return 0;
        }

        /// <summary>
        /// See above but now with class context
        /// </summary>
        private int BassCallback_FileProcUserReadOut(IntPtr buffer, int length)
        {
            int todo = length;
            lock (lockObject)
            {
                if (cbbChunkAudio != null)
                {
                    byte[] byteBuffer = new byte[length];

                    int count = cbbListenAudio.Read(byteBuffer, todo);
                    todo -= count;
                    if (count > 0)
                    {
                        // Nu kopieren in unmanaged data
                        Marshal.Copy(byteBuffer, 0, buffer, count);
                    }
                }
            }

            return length - todo;
        }

        #endregion

    }

    public class SongInfo
    {
        public string Artist
        {
            get;
            private set;
        }
        public string Title
        {
            get;
            private set;
        }

        public SongInfo(string Artist, string Title)
        {
            this.Artist = Artist;
            this.Title = Title;
        }
    }

    public class MetadataEventArgs : EventArgs
    {
        public string OldMetadata
        {
            get;
            private set;
        }
        public string NewMetadata
        {
            get;
            private set;
        }

        public MetadataEventArgs(string OldMetadata, string NewMetadata)
        {
            this.OldMetadata = OldMetadata;
            this.NewMetadata = NewMetadata;
        }
    }

    public class CurrentSongEventArgs : EventArgs
    {
        public SongInfo OldSong
        {
            get;
            private set;
        }
        public SongInfo NewSong
        {
            get;
            private set;
        }

        public CurrentSongEventArgs(SongInfo OldSong, SongInfo NewSong)
        {
            this.OldSong = OldSong;
            this.NewSong = NewSong;
        }
    }

    public class StreamUpdateEventArgs : EventArgs
    {
        public float[] Data
        {
            get;
            private set;
        }

        public StreamUpdateEventArgs(float[] Data)
        {
            this.Data = Data;
        }
    }

    public class MessageLogEventArgs : EventArgs
    {
        public string Message
        {
            get;
            private set;
        }

        public MessageLogEventArgs(string Message)
        {
            this.Message = Message;
        }
    }

    public enum AudioFrameType
    {
        None = 0,
        MP3Frame,
        AACFrame
    }

    public delegate void MessageLogged (object sender, MessageLogEventArgs e);
    public delegate void MetadataChanged(object sender, MetadataEventArgs e);
    public delegate void CurrentSongChanged(object sender, CurrentSongEventArgs e);
    public delegate void StreamUpdate(object sender, StreamUpdateEventArgs e);
}
