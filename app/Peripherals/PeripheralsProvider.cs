using GHelper.Peripherals.Mouse;
using GHelper.Peripherals.Mouse.Models;
using HidSharp;
using System.Runtime.CompilerServices;

namespace GHelper.Peripherals
{
    /// <summary>
    /// Detects and tracks the single supported mouse: Strix Impact III Wireless (OMNI).
    /// </summary>
    public class PeripheralsProvider
    {
        private static readonly object _LOCK = new object();

        private const int OMNI_PID = 0x1ACE;

        public static List<AsusMouse> ConnectedMice = new List<AsusMouse>();

        private static System.Timers.Timer timer = new System.Timers.Timer(1000);

        static PeripheralsProvider()
        {
            timer.Elapsed += DeviceTimer_Elapsed;
        }

        public static List<AsusMouse> SnapshotMice()
        {
            lock (_LOCK)
            {
                return new List<AsusMouse>(ConnectedMice);
            }
        }

        public static void Disconnect(AsusMouse am)
        {
            lock (_LOCK)
            {
                am.Disconnect -= Mouse_Disconnect;
                ConnectedMice.Remove(am);
            }
        }

        public static void Connect(AsusMouse am)
        {
            if (IsDeviceConnected(am))
            {
                return;
            }

            try
            {
                am.Connect();
            }
            catch (IOException e)
            {
                Logger.WriteLine(am.GetDisplayName() + " failed to connect to device: " + e);
                return;
            }

            int tries = 0;
            while (!am.IsDeviceReady && tries < 3)
            {
                Thread.Sleep(250);
                Logger.WriteLine(am.GetDisplayName() + " synchronising. Try " + (tries + 1));
                am.SynchronizeDevice();
                ++tries;
            }

            lock (_LOCK)
            {
                ConnectedMice.Add(am);
            }
            Logger.WriteLine(am.GetDisplayName() + " added to the list: " + ConnectedMice.Count + " devices are connected.");

            am.Disconnect += Mouse_Disconnect;
        }

        private static bool IsDeviceConnected(IPeripheral peripheral)
        {
            lock (_LOCK)
            {
                return ConnectedMice.Contains(peripheral);
            }
        }

        private static void Mouse_Disconnect(object? sender, EventArgs e)
        {
            if (sender is null)
            {
                return;
            }

            AsusMouse am = (AsusMouse)sender;
            lock (_LOCK)
            {
                ConnectedMice.Remove(am);
            }

            Logger.WriteLine(am.GetDisplayName() + " reported disconnect. " + ConnectedMice.Count + " devices are connected.");
            am.Dispose();
        }

        private static List<HidDevice> asusHidDevices = new();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void DetectAllAsusMice()
        {
            asusHidDevices = DeviceList.Local.GetHidDevices(0x0B05).ToList();

            // This build supports a single model: Strix Impact III Wireless (OMNI),
            // paired through the OMNI receiver.
            DedectOmniMouse();
        }

        public static void DedectOmniMouse()
        {
            var omnis = asusHidDevices.Where(x => x.ProductID == OMNI_PID && x.DevicePath.Contains("mi_02&col01"));
            var devices = asusHidDevices.Where(x => x.ProductID == OMNI_PID && x.DevicePath.Contains("mi_02&col03")).ToList();
            foreach (var omni in omnis)
                DedectOmniMouse(omni, devices.FirstOrDefault(x => OmniInstance(x.DevicePath) == OmniInstance(omni.DevicePath)));
        }

        private static string OmniInstance(string devicePath)
        {
            var parts = devicePath.Split('#');
            if (parts.Length < 3) return devicePath;
            int cut = parts[2].LastIndexOf('&');
            return cut > 0 ? parts[2][..cut] : parts[2];
        }

        private static void DedectOmniMouse(HidDevice omni, HidDevice? device)
        {
            try
            {
                if (device is null) return;

                var config = new OpenConfiguration();
                config.SetOption(OpenOption.Interruptible, true);
                config.SetOption(OpenOption.Exclusive, false);
                config.SetOption(OpenOption.Priority, 10);

                AsusMouse? omniMouse;

                using (var stream = omni.Open(config))
                {
                    stream.ReadTimeout = 2000;
                    stream.WriteTimeout = 2000;

                    var response = new byte[64];
                    stream.Write([0x01, 0xA0, 0x00, 0x00]);
                    stream.Read(response);

                    Logger.WriteLine($"Omni paired devices: {BitConverter.ToString(response.Skip(5).Take(12).ToArray())}");

                    omniMouse = ResolveOmniMouse(response);
                }

                if (omniMouse is null) return;

                omniMouse.SetPath(device.DevicePath);

                using (var stream = device.Open(config))
                {
                    stream.ReadTimeout = 2000;
                    stream.WriteTimeout = 2000;

                    var response = new byte[64];

                    stream.Write([0x03, 0x7D, 0x20, 0x02]);
                    stream.Read(response);
                    Logger.WriteLine("Booster: " + BitConverter.ToString(response.Skip(5).Take(12).ToArray()));
                    omniMouse.Booster = response[5] == 0x01;

                    DetectMouse(omniMouse);
                }
            }
            catch
            {
                return;
            }
        }

        private static AsusMouse? ResolveOmniMouse(byte[] response)
        {
            for (int offset = 5; offset + 3 < response.Length; offset += 4)
            {
                int pid = response[offset] | (response[offset + 1] << 8);
                if (pid == 0) break;

                var mouse = MouseFromOmniPid(pid);
                if (mouse is not null)
                {
                    Logger.WriteLine($"Omni slot @{offset}: {pid:X4} -> {mouse.GetDisplayName()}");
                    return mouse;
                }
            }

            return null;
        }

        private static AsusMouse? MouseFromOmniPid(int pid) => pid == 0x1AD7 ? new StrixImpactIIIWirelessOmni() : null;

        public static void DetectMouse(AsusMouse am)
        {
            if (am.IsDeviceConnected(asusHidDevices) && !IsDeviceConnected(am))
            {
                Logger.WriteLine("Detected a new " + am.GetDisplayName() + " . Connecting...");
                Connect(am);
            }
        }

        public static void RegisterForDeviceEvents()
        {
            HidSharp.DeviceList.Local.Changed += Device_Changed;
        }

        public static void UnregisterForDeviceEvents()
        {
            HidSharp.DeviceList.Local.Changed -= Device_Changed;
        }

        private static void Device_Changed(object? sender, HidSharp.DeviceListChangedEventArgs e)
        {
            timer.Start();
        }

        private static void DeviceTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            timer.Stop();
            Logger.WriteLine("HID Device Event: Checking for ASUS mice");
            DetectAllAsusMice();
        }
    }
}
