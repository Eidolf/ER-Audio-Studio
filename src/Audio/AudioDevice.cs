using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ErAudioTool.Audio
{
    public enum DeviceType
    {
        RenderLoopback,
        CaptureMicrophone
    }

    public class AudioDeviceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsDefault { get; set; }
        public DeviceState State { get; set; }
        public DeviceType Type { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public string FormatDescription { get; set; }

        public override string ToString()
        {
            string def = IsDefault ? " [Standard]" : "";
            string icon = Type == DeviceType.CaptureMicrophone ? "🎤 " : "🔊 ";
            return string.Format("{0}{1}{2}", icon, Name, def);
        }
    }

    public static class AudioDeviceEnumerator
    {
        public static List<AudioDeviceInfo> GetDevices(EDataFlow dataFlow, DeviceType devType)
        {
            var list = new List<AudioDeviceInfo>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

            IMMDevice defaultDev = null;
            string defaultId = null;
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eConsole, out defaultDev) == 0 && defaultDev != null)
                {
                    defaultDev.GetId(out defaultId);
                }
            }
            catch { }

            IMMDeviceCollection col;
            int hr = enumerator.EnumAudioEndpoints(dataFlow, DeviceState.Active, out col);
            if (hr != 0 || col == null) return list;

            int count;
            col.GetCount(out count);

            for (int i = 0; i < count; i++)
            {
                IMMDevice dev;
                if (col.Item(i, out dev) == 0 && dev != null)
                {
                    try
                    {
                        string id;
                        dev.GetId(out id);

                        DeviceState state;
                        dev.GetState(out state);

                        string friendlyName = GetDeviceFriendlyName(dev);
                        if (string.IsNullOrEmpty(friendlyName))
                        {
                            friendlyName = (devType == DeviceType.CaptureMicrophone ? "Mikrofon " : "Audiogerät ") + (i + 1);
                        }

                        var info = new AudioDeviceInfo
                        {
                            Id = id,
                            Name = friendlyName,
                            IsDefault = !string.IsNullOrEmpty(defaultId) && string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase),
                            State = state,
                            Type = devType,
                            SampleRate = 48000,
                            Channels = 2,
                            BitsPerSample = 32
                        };

                        // Mix format
                        try
                        {
                            object objClient;
                            Guid iid = WasapiGuids.IID_IAudioClient;
                            if (dev.Activate(ref iid, 1, IntPtr.Zero, out objClient) == 0 && objClient != null)
                            {
                                var client = (IAudioClient)objClient;
                                IntPtr pFormat;
                                if (client.GetMixFormat(out pFormat) == 0 && pFormat != IntPtr.Zero)
                                {
                                    var fmt = (WAVEFORMATEX)Marshal.PtrToStructure(pFormat, typeof(WAVEFORMATEX));
                                    info.SampleRate = (int)fmt.nSamplesPerSec;
                                    info.Channels = fmt.nChannels;
                                    info.BitsPerSample = fmt.wBitsPerSample;
                                    Marshal.FreeCoTaskMem(pFormat);
                                }
                            }
                        }
                        catch { }

                        string chDesc = info.Channels == 1 ? "Mono" : info.Channels == 2 ? "Stereo" : info.Channels + "-Kanal";
                        info.FormatDescription = string.Format("{0:N0} Hz • {1} • {2}-Bit", info.SampleRate, chDesc, info.BitsPerSample);

                        list.Add(info);
                    }
                    catch { }
                }
            }

            list.Sort((a, b) =>
            {
                if (a.IsDefault && !b.IsDefault) return -1;
                if (!a.IsDefault && b.IsDefault) return 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        public static List<AudioDeviceInfo> GetRenderDevices()
        {
            return GetDevices(EDataFlow.eRender, DeviceType.RenderLoopback);
        }

        public static List<AudioDeviceInfo> GetCaptureDevices()
        {
            return GetDevices(EDataFlow.eCapture, DeviceType.CaptureMicrophone);
        }

        public static AudioDeviceInfo GetDefaultRenderDevice()
        {
            var devices = GetRenderDevices();
            foreach (var d in devices)
            {
                if (d.IsDefault) return d;
            }
            return devices.Count > 0 ? devices[0] : null;
        }

        public static IMMDevice ActivateDevice(string deviceId, EDataFlow defaultFlow = EDataFlow.eRender)
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice dev = null;

            if (string.IsNullOrEmpty(deviceId))
            {
                enumerator.GetDefaultAudioEndpoint(defaultFlow, ERole.eConsole, out dev);
            }
            else
            {
                int hr = enumerator.GetDevice(deviceId, out dev);
                if (hr != 0 || dev == null)
                {
                    enumerator.GetDefaultAudioEndpoint(defaultFlow, ERole.eConsole, out dev);
                }
            }

            return dev;
        }

        private static string GetDeviceFriendlyName(IMMDevice dev)
        {
            try
            {
                IPropertyStore store;
                if (dev.OpenPropertyStore(0, out store) == 0 && store != null)
                {
                    PropertyKey key = PropertyKey.PKEY_Device_FriendlyName;
                    PropVariant pv;
                    if (store.GetValue(ref key, out pv) == 0 && pv.pwszVal != IntPtr.Zero)
                    {
                        return Marshal.PtrToStringUni(pv.pwszVal);
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
