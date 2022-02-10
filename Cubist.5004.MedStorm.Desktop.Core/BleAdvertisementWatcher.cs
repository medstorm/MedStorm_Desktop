using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Microsoft.AspNetCore.SignalR;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using System.Globalization;

namespace PSSApplication.Core
{
    public class BleAdvertisementWatcher : Hub
    {
        private static class Globals
        {
            public static readonly object mThreadLock = new object();
            //public static readonly Dictionary<string, BLEDevice> mDiscoveredDevices = new Dictionary<string, BLEDevice>();
            public static BluetoothLEDevice bluetoothLeDevice;
            public static GattDeviceService _service;
            public static GattCharacteristic characteristic;
            public static string deviceId;
            /// <summary>
            /// The underlying bluetooth watcher class
            /// </summary>
            public static BluetoothLEAdvertisementWatcher mWatcher;
        }

        public static readonly string ServiceUuid = "264eaed6-c1da-4436-b98c-db79a7cc97b5";

        public static readonly string ConnectionServiceUuid = "8dfa3c12-660c-4992-a528-ebf1fa02fe9d";
        public static readonly string ConnectionUuid = "6d5a8bd8-ce30-4c70-913b-21544a1ff4c3";
        public static readonly string CombinedUuid = "14abde20-31ed-4e0a-bdcf-7efc40f3fffb";

        private const int NumOfCondItems = 5;
        private const int NumBytesFloats = 4;

        private readonly string AdvertisementName;

        private bool isBusy = false;
        // Should try to refactor to get rid of context here. This class handles bluetooth communication,
        // not communication with web clients.
        private IHubContext<BleEndpoint> _context;

        public event EventHandler<MeasurementEventArgs> NewMeasurement;

        public BleAdvertisementWatcher(IHubContext<BleEndpoint> context, string advertisementName)
        {
            BleEndpoint.DebugWrite("New watcher created");
            Globals.mWatcher = new BluetoothLEAdvertisementWatcher();
            Globals.mWatcher.Received += Watcher_Received;
            Globals.mWatcher.Stopped += Watcher_Stopped;
            HookEvents(this);

            _context = context;
            AdvertisementName = advertisementName;
            if (string.IsNullOrWhiteSpace(AdvertisementName))
                throw new ArgumentException("Cannot retrieve AdvertisingName from appsettings.json");
        }

        /// <summary>
        /// Indicates if this watcher is listening for advertisements
        /// </summary>
        public bool Listening => Globals.mWatcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;

        public int SleepTimer = 10;

        private void Watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            BleEndpoint.DebugWrite("Watcher_Stopped");
            StopListening();
        }

        public void StartScanningForPainSensors()
        {
            BleEndpoint.DebugWrite("StartScanningForPainSensors");

            if (Globals.mWatcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                return;

            Globals.mWatcher.Start();
            StartedListening(); // Inform listeners
        }

        public void StopAdvertising()
        {
            BleEndpoint.DebugWrite("Stop advertising");
            StopAllBluetoothConnections();
            Globals.deviceId = null;
            if (Globals.mWatcher != null)
                Globals.mWatcher.Stop();
        }

        private async Task<bool> UnpairDevice(ulong bluetoothAddress)
        {
            BleEndpoint.DebugWrite("UnpairDevice");
            DeviceUnpairingResult unpairResult;
            bool isPaired = Globals.bluetoothLeDevice.DeviceInformation.Pairing.IsPaired;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //Try to unpair for 30 seconds, then fail
            while (isPaired && stopwatch.ElapsedMilliseconds < 30000)
            {
                unpairResult = await Globals.bluetoothLeDevice.DeviceInformation.Pairing.UnpairAsync();
                ReleaseAndClearBluetoothLEObjects();
                if (unpairResult.Status == DeviceUnpairingResultStatus.Unpaired)
                {
                    isPaired = false;
                }
                else
                {
                    Globals.bluetoothLeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                    if (Globals.bluetoothLeDevice == null)
                        break;
                }

                BleEndpoint.DebugWrite($"Pairing Result: {unpairResult.Status}");
            }
            stopwatch.Stop();
            return isPaired;
        }

        private async Task<DevicePairingResult> PairDevice(ulong bluetoothAddress)
        {
            BleEndpoint.DebugWrite("PairDevice");
            bool isPaired = false;
            DevicePairingResult _devicePairingResult = null;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //Try to pair for 30 seconds then fails
            while (!isPaired && stopwatch.ElapsedMilliseconds < 30000)
            {
                BleEndpoint.DebugWrite($"Pairing...");
                if (Globals.bluetoothLeDevice != null)
                {
                    ReleaseAndClearBluetoothLEObjects();
                }
                Globals.bluetoothLeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (Globals.bluetoothLeDevice == null)
                    break;
                Globals.bluetoothLeDevice.DeviceInformation.Pairing.Custom.PairingRequested += Custom_PairingRequested;
                _devicePairingResult = await Globals.bluetoothLeDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
                Globals.bluetoothLeDevice.DeviceInformation.Pairing.Custom.PairingRequested -= Custom_PairingRequested;
                BleEndpoint.DebugWrite($"Pairing Result: {_devicePairingResult.Status}");
                if (_devicePairingResult.Status == DevicePairingResultStatus.Paired)
                {
                    isPaired = true;
                }
            }
            stopwatch.Stop();
            stopwatch = null;
            return _devicePairingResult;
        }

        public bool IsPaired()
        {
            if (Globals.bluetoothLeDevice != null)
            {
                return Globals.bluetoothLeDevice.DeviceInformation.Pairing.IsPaired;
            }

            return false;
        }

        public void CloseApplication()
        {
            StopAllBluetoothConnections();
        }

        private async void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (isBusy)
                return;

            if (!isBusy)
            {
                if (!string.IsNullOrWhiteSpace(args.Advertisement.LocalName))
                {
                    BleEndpoint.DebugWrite($"Advertisement Discovered: {args.Advertisement.LocalName}");
                }
            }
            if (args.Advertisement.LocalName != AdvertisementName)
                return;
            isBusy = true;

            BleEndpoint.DebugWrite("Advertisement Pairing...");

            ReleaseAndClearBluetoothLEObjects();
            var bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
            if (bleDevice == null || Globals.deviceId != null && bleDevice.DeviceId != Globals.deviceId)
            {
                isBusy = false;
                BleEndpoint.DebugWrite("No or wrong device trying to connect");
                return;
            }

            Globals.bluetoothLeDevice = bleDevice;

            bool isPaired = await UnpairDevice(args.BluetoothAddress);
            DevicePairingResult result = null;
            if (isPaired == false)
            {
                result = await PairDevice(args.BluetoothAddress);
            }
            if (result != null && result.Status == DevicePairingResultStatus.Paired)
            {
                Debug.WriteLine($"Paired to {args.BluetoothAddress}");
                Globals.bluetoothLeDevice.ConnectionStatusChanged += ConnectionStatusChangeHandler;
                Globals.mWatcher.Stop();
                isBusy = false;
                await ConfigureSensorService(Globals.bluetoothLeDevice.DeviceId);
            }
            else
            {
                //Failed to connect - Disconnect gracefully
                Debug.WriteLine("Failed to paire");
                StopAllBluetoothConnections();
                isBusy = false;
            }
        }

        private async Task ConfigureSensorService(string deviceId)
        {
            BleEndpoint.DebugWrite("ConfigureSensorService");
            try
            {
                GattCharacteristicProperties NotifyOrIndicate = GattCharacteristicProperties.Notify;
                GattClientCharacteristicConfigurationDescriptorValue NotifyOrIndicateValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
                Globals.bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(deviceId);
                if (Globals.bluetoothLeDevice != null)
                {
                    Globals.deviceId = deviceId;
                    var result = await Globals.bluetoothLeDevice.GetGattServicesForUuidAsync(Guid.Parse(ServiceUuid));
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        var services = result.Services;
                        await Task.Delay(SleepTimer);
                        BleEndpoint.DebugWrite($"=>Services Count {result.Services.Count}", true);
                        Globals._service = result.Services[0];
                        services = null;
                        BleEndpoint.DebugWrite($"Service {Globals._service.Uuid} found and accessed!");
                        var serviceAccess = await Globals._service.RequestAccessAsync();
                        GattCharacteristicsResult characteristicResultAllValues = await Globals._service.GetCharacteristicsForUuidAsync(Guid.Parse(CombinedUuid));
                        if (characteristicResultAllValues.Status == GattCommunicationStatus.Success)
                        {
                            if (characteristicResultAllValues.Characteristics.Count == 0)
                            {
                                BleEndpoint.DebugWrite($"No characteristics available in UUID {Guid.Parse(CombinedUuid)}");
                                return;
                            }
                            Globals.characteristic = characteristicResultAllValues.Characteristics[0];
                            characteristicResultAllValues = null;
                            BleEndpoint.DebugWrite($"Characteristic AllValues {Globals.characteristic.Uuid} found and accessed!");

                            GattCharacteristicProperties properties = Globals.characteristic.CharacteristicProperties;

                            List<GattCharacteristicProperties> gattCharacteristicProperties = new List<GattCharacteristicProperties>();
                            foreach (GattCharacteristicProperties property in Enum.GetValues(typeof(GattCharacteristicProperties)))
                            {
                                if (properties.HasFlag(property))
                                {
                                    gattCharacteristicProperties.Add(property);
                                }
                            }

                            if (gattCharacteristicProperties.Any(x => x == NotifyOrIndicate))
                            {
                                BleEndpoint.DebugWrite("Subscribing to the AllValues Indication/Notification");
                                Globals.characteristic.ValueChanged += Oncharacteristic_ValueChanged_Combined;

                                int loopCounter = 5;
                                GattCommunicationStatus status = GattCommunicationStatus.ProtocolError;
                                try
                                {
                                    while (status != GattCommunicationStatus.Success && loopCounter-- > 0)
                                    {
                                        BleEndpoint.DebugWrite($"Enter function WriteClientCharacteristicConfigurationDescriptorAsync AllValues");
                                        status = await Globals.characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(NotifyOrIndicateValue);
                                        if (status == GattCommunicationStatus.Success)
                                            await Task.Delay(SleepTimer);
                                    }
                                    if (status != GattCommunicationStatus.Success)
                                    {
                                        BleEndpoint.DebugWrite($"Function WriteClientCharacteristicConfigurationDescriptorAsync failed - Disconnect");
                                        StopAllBluetoothConnections();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    BleEndpoint.DebugWrite($"Exception from AllValues   WriteClientCharacteristicConfigurationDescriptorAsync {ex.Message}          { ex.ToString()}");
                                    StopAllBluetoothConnections();

                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }


        /// <summary>
        /// Stops listening for advertisements
        /// </summary>
        private void StopListening()
        {
            BleEndpoint.DebugWrite("StopListening");
            lock (Globals.mThreadLock)
            {
                BleEndpoint.DebugWrite("Unhook event Watcher_Received");
                isBusy = false;
                // Clear any devices
                //Globals.mDiscoveredDevices.Clear();
                BleEndpoint.DebugWrite("Stop listening");
                StoppedListening();     // Fire event
            }
        }

        private void Custom_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            BleEndpoint.DebugWrite("Custom_PairingRequested");
            args.Accept();
        }

        private static void WatcherStartedListening()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Started listening");
        }

        //private static void WatcherDeviceTimeout(BLEDevice device)
        //{
        //    Console.ForegroundColor = ConsoleColor.Red;
        //    Console.WriteLine($"Device timeout: {device}");
        //}

        private static void WatcherStoppedListening()
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Stopped listening");
        }

        //private static void WatcherNewDeviceDiscovered(BLEDevice device)
        //{
        //    Console.ForegroundColor = ConsoleColor.Green;
        //    Console.WriteLine($"New device: {device}");
        //}

        //private static void WatcherDeviceNameChanged(BLEDevice device)
        //{
        //    Console.ForegroundColor = ConsoleColor.Yellow;
        //    Console.WriteLine($"Device name changed: {device}");
        //}

        private static void HookEvents(BleAdvertisementWatcher watcher)
        {
            // Hook into events
            watcher.StartedListening += WatcherStartedListening;
            watcher.StoppedListening += WatcherStoppedListening;
            //watcher.NewDeviceDiscovered += WatcherNewDeviceDiscovered;
            //watcher.DeviceNameChanged += WatcherDeviceNameChanged;
            //watcher.DeviceTimeout += WatcherDeviceTimeout;
        }

        //private static void UnhookEvents(BleAdvertisementWatcher watcher)
        //{
        //    watcher.StartedListening -= WatcherStartedListening;
        //    watcher.StoppedListening -= WatcherStoppedListening;
        //    watcher.NewDeviceDiscovered -= WatcherNewDeviceDiscovered;
        //    watcher.DeviceNameChanged -= WatcherDeviceNameChanged;
        //    watcher.DeviceTimeout -= WatcherDeviceTimeout;
        //}

        /// <summary>
        /// Fired when the bluetooth watcher stops listening
        /// </summary>
        private event Action StoppedListening = () => { };

        /// <summary>
        /// Fired when the bluetooth watcher starts listening
        /// </summary>
        private event Action StartedListening = () => { };

        /// <summary>
        /// Fired when a device is discovered
        /// </summary>
        //private event Action<BLEDevice> DeviceDiscovered = (device) => { };

        /// <summary>
        /// Fired when a new device is discovered
        /// </summary>
        //private event Action<BLEDevice> NewDeviceDiscovered = (device) => { };

        /// <summary>
        /// Fired when a device name changes
        /// </summary>
        //private event Action<BLEDevice> DeviceNameChanged = (device) => { };


        /// <summary>
        /// Fired when a device is removed for timing out
        /// </summary>
        //private event Action<BLEDevice> DeviceTimeout = (device) => { };

        ///// <summary>
        ///// Prune any timed out devices that we have not heard off
        ///// </summary>
        //private void CleanupTimeouts()
        //{
        //    lock (Globals.mThreadLock)
        //    {
        //        // The date in time that if less than means a device has timed out
        //        var threshold = DateTime.UtcNow - TimeSpan.FromSeconds(30);

        //        // Any devices that have not sent a new broadcast within the heartbeat time
        //        Globals.mDiscoveredDevices.Where(f => f.Value.BroadcastTime < threshold).ToList().ForEach(device =>
        //        {
        //            // Remove device
        //            Globals.mDiscoveredDevices.Remove(device.Key);

        //            // Inform listeners
        //            // Raising a public event inside a lock is a really bad idea.
        //            DeviceTimeout(device.Value);
        //        });
        //    }
        //}

        /// <summary>
        /// We will receive 28 byte array and parse it to a message string and send to front end.
        /// 
        /// Byte array order:
        /// Byte 0: PPS
        /// Byte 1: Area
        /// Byte 2-21: Conductivity contains 5 float packages ( 5 * 4 bytes)
        /// byte 22-25: MeanRiseTime, 1 float package (4 bytes)
        /// byte 26: NerveBlock 1 byte
        /// byte 27: Bad signal 1 byte
        /// 
        /// The message will look like: "PPS:5|Area:21|SkinCond:[0.68343097,0.59316385,0.11316252,0.34425306,0.01528877]|MeanRiseTime:0.5188683|NerveBlock:6|BadSignal:0"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private void SendMessageToClient(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] bArray = new byte[args.CharacteristicValue.Length];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(bArray);
            byte ppsValue = bArray[0];
            byte areaValue = bArray[1];
            double[] ConductivityItems = new double[NumOfCondItems];
            for (int i = 0; i < NumOfCondItems; i++)
            {
                byte[] condItemBytes = new byte[4];
                Array.Copy(bArray, 2 + i * NumBytesFloats, condItemBytes, 0, NumBytesFloats);
                double floatToDouble = (double)(new decimal(BitConverter.ToSingle(condItemBytes, 0)));
                ConductivityItems[i] = floatToDouble;
            }

            float meanRiseTimeValue = BitConverter.ToSingle(bArray, 22);
            byte nerveBlockValue = bArray[26];
            byte badSignalValue = bArray[27];

            MeasurementEventArgs measurementsArgs = new MeasurementEventArgs(ppsValue, areaValue, nerveBlockValue, ConductivityItems, badSignalValue, meanRiseTimeValue);

            Debug.WriteLine(measurementsArgs.Message);
            NewMeasurement?.Invoke(this, measurementsArgs);
        }

        public async Task DeviceDisconnected()
        {
            BleEndpoint.DebugWrite("DeviceDisconnected");
            if (_context != null && _context.Clients != null)
            {
                await _context.Clients.All.SendAsync("DeviceDisconnected");
            }
        }
        private bool ReleaseAndClearBluetoothLEObjects()
        {
            BleEndpoint.DebugWrite("ReleaseAndClearBluetoothLEObjects");
            if (Globals.characteristic != null)
            {
                Globals.characteristic.ValueChanged -= Oncharacteristic_ValueChanged_Combined;
                Globals.characteristic = null;
            }
            if (Globals._service != null)
            {
                Globals._service.Dispose();
                Globals._service = null;
            }
            if (Globals.bluetoothLeDevice != null)
            {
                Globals.bluetoothLeDevice.Dispose();
                Globals.bluetoothLeDevice = null;
            }
            return true;
        }
        private void StopAllBluetoothConnections()
        {
            BleEndpoint.DebugWrite("StopAllBluetoothConnections");
            if (Globals.bluetoothLeDevice != null)
            {
                Globals.bluetoothLeDevice.ConnectionStatusChanged -= ConnectionStatusChangeHandler;
            }
            ReleaseAndClearBluetoothLEObjects();
        }

        public async void ConnectionStatusChangeHandler(BluetoothLEDevice bluetoothLEDevice, Object o)
        {

            if (Globals.bluetoothLeDevice == null)
                return;

            StopAllBluetoothConnections();
            await DeviceDisconnected();
            BleEndpoint.DebugWrite($"Enter ConnectionStatusChangeHandler  The device is now disconnected");

            //Try reconnect
            StartScanningForPainSensors();
            await _context.Clients.All.SendAsync("ReconnectDevice");
        }

        public void Oncharacteristic_ValueChanged_Combined(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            SendMessageToClient(sender, args);
        }
    }
}
