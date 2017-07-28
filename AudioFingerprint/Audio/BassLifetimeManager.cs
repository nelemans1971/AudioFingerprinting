using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.AddOn.Tags;

namespace AudioFingerprint.Audio
{
    public class BassLifetimeManager : IDisposable
    {
        // =========================================================================================================================
        // This is the BASS.NET key to remove the nag screen.
        public static string bass_EMail = "";
        public static string bass_RegistrationKey = "";
        // =========================================================================================================================

        private const string FlacDllName = "bassflac.dll";
        private const string AacDllName = "bass_aac.dll";
        private static int initializedInstances;
        private static bool bassInitialized = false;
        private static bool recordDeviceAvailable = false;
        private static object lockObject = new object();
        private bool alreadyDisposed;

        public BassLifetimeManager(bool initializeRecordDevice = false)
        {
            lock (lockObject)
            {
                if (IsBassLibraryHasToBeInitialized(Interlocked.Increment(ref initializedInstances)))
                {
                    string IniPath = CDR.DB_Helper.FingerprintIniFile;
                    if (System.IO.File.Exists(IniPath))
                    {
                        try
                        {
                            CDR.Ini.IniFile ini = new CDR.Ini.IniFile(IniPath);
                            bass_EMail = ini.IniReadValue("BASS", "bass_EMail", bass_EMail);
                            bass_RegistrationKey = ini.IniReadValue("BASS", "bass_RegistrationKey", bass_RegistrationKey);
                        }
                        catch { }
                    }

                    RegisterBassKey();
                    string targetPath = GetTargetPathToLoadLibrariesFrom();
                    LoadBassLibraries(targetPath);
                    CheckIfFlacPluginIsLoaded(targetPath);
                    //CheckIfAacPluginIsLoaded(targetPath);

                    InitializeBassLibraryWithAudioDevices();
                    SetDefaultConfigs();
                    if (initializeRecordDevice)
                    {
                        InitializeRecordingDevice();
                    }
                    bassInitialized = true;
                }
                else
                {
                    // wait till bass lib is intialized
                    while (!bassInitialized)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }

        ~BassLifetimeManager()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (!alreadyDisposed)
            {
                if (Interlocked.Decrement(ref initializedInstances) == 0)
                {
                    // 0 - free all loaded plugins
                    if (!Bass.BASS_PluginFree(0))
                    {
                        Trace.WriteLine("Could not unload plugins for Bass library.", "Error");
                    }

                    if (!Bass.BASS_Free())
                    {
                        Trace.WriteLine("Could not free Bass library. Possible memory leak!", "Error");
                    }
                    bassInitialized = false;
                }
            }

            alreadyDisposed = true;
        }

        private bool IsBassLibraryHasToBeInitialized(int numberOfInstances)
        {
            return numberOfInstances == 1;
        }

        private void RegisterBassKey()
        {
            // Keep Bass.Net from putting a popup on the screen
            if (bass_EMail.Length > 0 && bass_RegistrationKey.Length > 0)
            {
                BassNet.Registration(bass_EMail, bass_RegistrationKey);
            }
        }

        private string GetTargetPathToLoadLibrariesFrom()
        {
            string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
            if (string.IsNullOrEmpty(executingPath))
            {
                throw new BassException("Executing path of the application is null or empty. Could not find folders with native DLL libraries.");
            }

            Uri uri = new Uri(executingPath);
            return Path.Combine(uri.LocalPath, Utils.Is64Bit ? "x64" : "x86");
        }

        private void LoadBassLibraries(string targetPath)
        {
            if (!Bass.LoadMe(targetPath))
            {
                throw new BassException("Could not load bass native libraries from the following path: " + targetPath);
            }

            if (!BassMix.LoadMe(targetPath))
            {
                throw new BassException("Could not load bassmix library from the following path: " + targetPath);
            }

            if (!BassFx.LoadMe(targetPath))
            {
                throw new BassException("Could not load bassfx library from the following path: " + targetPath);
            }

            DummyCallToLoadBassLibraries();
        }

        private void DummyCallToLoadBassLibraries()
        {
            Bass.BASS_GetVersion();
            BassMix.BASS_Mixer_GetVersion();
            BassFx.BASS_FX_GetVersion();
        }

        private void InitializeBassLibraryWithAudioDevices()
        {
            if (!Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero))
            {
                Trace.WriteLine("Failed to find a sound device on running machine. Playing audio files will not be supported. " + Bass.BASS_ErrorGetCode().ToString(), "Warning");
                if (!Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero))
                {
                    throw new BassException(Bass.BASS_ErrorGetCode().ToString());
                }
            }
        }

        private void CheckIfFlacPluginIsLoaded(string targetPath)
        {
            var loadedPlugIns = Bass.BASS_PluginLoadDirectory(targetPath);
            if (!loadedPlugIns.Any(p => p.Value.EndsWith(FlacDllName)))
            {
                Trace.WriteLine("Could not load bassflac.dll. FLAC format is not supported!", "Warning");
            }
        }

        private void CheckIfAacPluginIsLoaded(string targetPath)
        {
            var loadedPlugIns = Bass.BASS_PluginLoadDirectory(targetPath);
            if (!loadedPlugIns.Any(p => p.Value.EndsWith(AacDllName)))
            {
                Trace.WriteLine("Could not load bass_aac.dll. AAC format is not supported!", "Warning");
            }
        }

        private void SetDefaultConfigs()
        {
            // Set filter for anti aliasing
            if (!Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_MIXER_FILTER, 50))
            {
                throw new BassException(Bass.BASS_ErrorGetCode().ToString());
            }

            // Set floating parameters to be passed
            if (!Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_FLOATDSP, true))
            {
                throw new BassException(Bass.BASS_ErrorGetCode().ToString());
            }
        }

        private void InitializeRecordingDevice()
        {
            const int DefaultDevice = -1;
            recordDeviceAvailable = Bass.BASS_RecordInit(DefaultDevice);
            if (!recordDeviceAvailable)
            {
                Trace.WriteLine("No default recording device could be found on running machine. Recording is not supported: " + Bass.BASS_ErrorGetCode().ToString(), "Warning");
            }
        }

        public bool RecordDeviceAvailable
        {
            get
            {
                return recordDeviceAvailable;
            }
        }
    }

    public class BassException : Exception
    {
        public BassException(string errorMessage)
            : base(errorMessage)
        {
        }
    }
}
