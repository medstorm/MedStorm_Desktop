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
using System.Threading;

namespace PSSApplication.Core
{
    public class BleHub : Hub
    {
        IHubContext<BleEndpoint> m_hubContext;
        AdvertisementHandler m_advHandler;

        // This constructor is called everytime the client page is reloaded
        public BleHub(IHubContext<BleEndpoint> context, string advertisementName)
        {
            m_hubContext = context;
            m_advHandler = AdvertisementHandler.CreateAdvertisementHandler(this, advertisementName);
        }

        public void CloseApplication()
        {
            Debug.WriteLine("CloseApplication");
            m_advHandler.CloseApplication();
        }

        public void StartScanningForPainSensors()
        {
            BleEndpoint.DebugWrite("BleHub: StartScanningForPainSensors");
            m_advHandler.StartScanningForPainSensors();
        }

        public void StopScanningForPainSensors()
        {
            BleEndpoint.DebugWrite("BleHub: StopScanningForPainSensors");
            m_advHandler.StopScanningForPainSensors();
        }
    }

    public class AdvertisementHandler
    {
        public event EventHandler<MeasurementEventArgs> NewMeasurement;

        static readonly string ServiceUuid = "264eaed6-c1da-4436-b98c-db79a7cc97b5";
        static readonly string CombinedUuid = "14abde20-31ed-4e0a-bdcf-7efc40f3fffb";

        readonly object m_ThreadLock = new object();
        //BluetoothLEDevice m_bluetoothLeDevice = null;
        GattDeviceService m_service = null;
        GattCharacteristic m_characteristic = null;
        //string m_deviceId = null;
        BluetoothLEAdvertisementWatcher m_Watcher; // The underlying bluetooth watcher class

        const int NumOfCondItems = 5;
        const int NumBytesFloats = 4;

        readonly string AdvertisementName;

        bool m_isBusy = false;
        static BleHub m_bleHub;
        public bool Listening => m_Watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;
        public int SleepTimer = 1000;
        public static bool IsRunning { get; private set; }

        ulong? m_bluetoothAddress = null;
        Timer m_timer;
        async Task<BluetoothLEDevice> GetBluetoothLEDevice()
        {
            if (m_bluetoothAddress == null)
            {
                Debug.WriteLine("AdvertisementHandler.GetBluetoothLEDevice: No available m_bluetoothAddress");
                return null;
            }
            var bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(m_bluetoothAddress.Value);
            if (bleDevice == null)
            {
                m_isBusy = false;
                BleEndpoint.DebugWrite("AdvertisementHandler.GetBluetoothLEDevice: Not able to get bleDevice");
                return null;
            }
            return bleDevice;
        }

        DateTime lastReceivedData = DateTime.MinValue;
        static AdvertisementHandler()
        {
            IsRunning = false;
        }

        public static AdvertisementHandler m_advertisementHandlerSingleton;
        public static AdvertisementHandler AdvertisementMgr
        {
            get => m_advertisementHandlerSingleton; private set => m_advertisementHandlerSingleton = value;
        }

        public static AdvertisementHandler CreateAdvertisementHandler(BleHub bleHub, string advertisementName)
        {
            if (m_advertisementHandlerSingleton == null)
                m_advertisementHandlerSingleton = new AdvertisementHandler(bleHub, advertisementName);

            return m_advertisementHandlerSingleton;
        }
        // This constructor is called everytime the client page is reloaded
        private AdvertisementHandler(BleHub bleHub, string advertisementName)
        {
            m_bleHub = bleHub;
            AdvertisementName = advertisementName;
            BleEndpoint.DebugWrite("AdvertisementHandler.ctor - New watcher created");
            m_Watcher = new BluetoothLEAdvertisementWatcher();
            m_Watcher.Received += Watcher_Received;
            m_Watcher.Stopped += Watcher_Stopped;
            //m_advHandler.StartedListening += WatcherStartedListening;
            //m_advHandler.StoppedListening += WatcherStoppedListening;
            HookEvents(this);
            if (string.IsNullOrWhiteSpace(advertisementName))
                throw new ArgumentException("Cannot retrieve AdvertisingName from appsettings.json");

            Timer timer = new Timer(CheckStatus, null, 30000, 10000);    // wait 30 seconds, and then check every 10th. seconds
        }

        public void CheckStatus(Object stateInfo)
        {
            if (IsRunning && lastReceivedData < (DateTime.Now-TimeSpan.FromSeconds(10)))
            {
                BleEndpoint.DebugWrite("Expected datastream dosn't work. Restarting connection to sensor");
                m_timer.Change(30000, 10000);
                StopScanningForPainSensors();
                StartScanningForPainSensors();
            }
        }

        private void Watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            BleEndpoint.DebugWrite("AdvertisementHandler.Watcher_Stopped");
            StopListening();
        }

        public void StartScanningForPainSensors()
        {
            BleEndpoint.DebugWrite("AdvertisementHandler.StartScanningForPainSensors");
            IsRunning = true;
            if (m_Watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                return;

            m_Watcher.Start();
            StartedListening(); // Inform listeners
            lastReceivedData = DateTime.Now;    // To reset status check timer
        }

        public async void StopScanningForPainSensors()
        {
            if (m_Watcher != null)
                m_Watcher.Stop();

            BleEndpoint.DebugWrite("AdvertisementHandler.StopScanningForPainSensors");
            IsRunning = false;

            await StopAllBluetoothConnections();
            await UnpairDevice();
        }

        private async Task<DeviceUnpairingResult> UnpairDevice()
        {
            Debug.WriteLine("AdvertisementHandler.UnpairDevice:");
            BluetoothLEDevice bleDevice = await GetBluetoothLEDevice();
            if (bleDevice == null)
            {
                Debug.WriteLine("AdvertisementHandler.UnpairDevice: Not able to get bleDevice ");
                return null;
            }

            DeviceUnpairingResult unpairResult = null;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //Try to unpair for 30 seconds, then fail
            do
            {
                unpairResult = await bleDevice.DeviceInformation.Pairing.UnpairAsync();
                BleEndpoint.DebugWrite($"AdvertisementHandler.UnpairDevice: Pairing Result= {unpairResult.Status}");
            }
            while (unpairResult.Status != DeviceUnpairingResultStatus.Unpaired
                   && unpairResult.Status != DeviceUnpairingResultStatus.AlreadyUnpaired
                   && stopwatch.ElapsedMilliseconds < 30000);

            stopwatch.Stop();

            Debug.WriteLine($"AdvertisementHandler.UnpairDevice: unpairResult={unpairResult?.Status}");
            return unpairResult;
        }

        private async Task<DevicePairingResult> PairDevice()
        {
            Debug.WriteLine("PairDevice:");
            Debug.WriteLine("AdvertisementHandler.UnpairDevice:");
            BluetoothLEDevice bleDevice = await GetBluetoothLEDevice();
            if (bleDevice == null)
            {
                Debug.WriteLine("AdvertisementHandler.PairDevice: Not able to get bleDevice ");
                return null;
            }

            try
            {
                DevicePairingResult devicePairingResult = null;
                bool isPaired = false;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                //Try to pair for 30 seconds then fails
                while (!isPaired && stopwatch.ElapsedMilliseconds < 30000)
                {
                    BleEndpoint.DebugWrite($"AdvertisementHandler.PairDevice: Pairing...");
                    bleDevice.DeviceInformation.Pairing.Custom.PairingRequested += Custom_PairingRequested;
                    devicePairingResult = await bleDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
                    bleDevice.DeviceInformation.Pairing.Custom.PairingRequested -= Custom_PairingRequested;
                    BleEndpoint.DebugWrite($"AdvertisementHandler.PairDevice: result: {devicePairingResult.Status}");

                    //if (m_bluetoothLeDevice.DeviceInformation.Pairing.IsPaired)
                    if (devicePairingResult.Status == DevicePairingResultStatus.Paired)
                        isPaired = true;

                }
                stopwatch.Stop();
                stopwatch = null;
                return devicePairingResult;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"xxxx PairDevice: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> IsPaired()
        {
            Debug.WriteLine("AdvertisementHandlerIsPaired:");
            BluetoothLEDevice bleDevice = await GetBluetoothLEDevice();
            if (bleDevice == null)
            {
                Debug.WriteLine("AdvertisementHandler.AdvertisementHandler: Not able to get bleDevice ");
                return false;
            }

            return bleDevice.DeviceInformation.Pairing.IsPaired;
        }

        public void CloseApplication()
        {
            StopScanningForPainSensors();
        }

        private async void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            Debug.WriteLine($"AdvertisementHandler.Watcher_Received: m_busy={m_isBusy}, BluetoothAddress={args.BluetoothAddress} ");
            if (m_isBusy)
                return;

            if (!string.IsNullOrWhiteSpace(args.Advertisement.LocalName))
                BleEndpoint.DebugWrite($"AdvertisementHandler.Watcher_Received: Advertisement Discovered from {args.Advertisement.LocalName}");

            try
            {
                if (args.Advertisement.LocalName != AdvertisementName)
                    return;

                m_isBusy = true;
                m_Watcher.Stop();

                BleEndpoint.DebugWrite("AdvertisementHandler.Watcher_Received: pairing...");
                m_bluetoothAddress = args.BluetoothAddress;
                BluetoothLEDevice bleDevice = await GetBluetoothLEDevice();

                if (bleDevice == null)
                {
                    Debug.WriteLine("AdvertisementHandler.Watcher_Received: Not able to get bleDevice ");
                    m_isBusy = false;
                    m_Watcher.Start();
                    return;
                }

                BleEndpoint.DebugWrite($"AdvertisementHandler.Watcher_Received: attached BluetoothAddress={bleDevice.BluetoothAddress}");

                // Start connection
                Debug.WriteLine($"AdvertisementHandler.Watcher_Received: Connection={bleDevice.ConnectionStatus}");

                bleDevice = await BluetoothLEDevice.FromIdAsync(bleDevice.DeviceId);
                bleDevice.ConnectionStatusChanged += ConnectionStatusChangeHandler;
                Debug.WriteLine($"AdvertisementHandler.Watcher_Received: after FromIdAsync - Connection={bleDevice.ConnectionStatus}");

                DeviceUnpairingResult unpairingResult = null;
                if (bleDevice.DeviceInformation.Pairing.IsPaired)
                {
                    unpairingResult = await UnpairDevice();
                }

                DevicePairingResult result = await PairDevice();
                if (result.Status == DevicePairingResultStatus.Paired)
                {
                    Debug.WriteLine($"AdvertisementHandler.Watcher_Received: Paired to {args.BluetoothAddress}, connectionOk= {bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected}");
                    m_isBusy = false;
                }
                else
                {
                    //Failed to connect - Disconnect gracefully
                    Debug.WriteLine("AdvertisementHandler.Watcher_Received: Failed to paire");
                    //StopAllBluetoothConnections();
                    m_isBusy = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"xxxxx AdvertisementHandler.Watcher_Received: ex.Message={ex.Message}");
                //await UnpairDevice();
                m_Watcher.Start();
                m_isBusy = false;
            }
        }

        private async Task ConfigureSensorService(BluetoothLEDevice bleDevice)
        {
            BleEndpoint.DebugWrite($"AdvertisementHandler.ConfigureSensorService Connection={bleDevice?.ConnectionStatus}");
            try
            {
                var Notify = GattCharacteristicProperties.Notify;
                var NotifValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;

                if (bleDevice != null)
                {
                    var result = await bleDevice.GetGattServicesForUuidAsync(Guid.Parse(ServiceUuid));
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        var services = result.Services;
                        await Task.Delay(SleepTimer);
                        BleEndpoint.DebugWrite($"AdvertisementHandler.ConfigureSensorService: Services Count= {result.Services.Count}", true);
                        m_service = result.Services[0];

                        BleEndpoint.DebugWrite($"AdvertisementHandler.ConfigureSensorService: Service { m_service.Uuid} found and accessed!");
                        var serviceAccess = await m_service.RequestAccessAsync();
                        GattCharacteristicsResult characteristicResultAllValues = await m_service.GetCharacteristicsForUuidAsync(Guid.Parse(CombinedUuid));
                        if (characteristicResultAllValues.Status == GattCommunicationStatus.Success)
                        {
                            if (characteristicResultAllValues.Characteristics.Count == 0)
                            {
                                BleEndpoint.DebugWrite($"AdvertisementHandler.ConfigureSensorService: No characteristics available in UUID {Guid.Parse(CombinedUuid)}");
                                return;
                            }
                            m_characteristic = characteristicResultAllValues.Characteristics[0];
                            //characteristicResultAllValues = null;
                            BleEndpoint.DebugWrite($"AdvertisementHandler.ConfigureSensorService: Characteristic AllValues { m_characteristic.Uuid} found and accessed!");

                            GattCharacteristicProperties properties = m_characteristic.CharacteristicProperties;

                            List<GattCharacteristicProperties> gattCharacteristicProperties = new List<GattCharacteristicProperties>();
                            foreach (GattCharacteristicProperties property in Enum.GetValues(typeof(GattCharacteristicProperties)))
                            {
                                if (properties.HasFlag(property))
                                {
                                    gattCharacteristicProperties.Add(property);
                                }
                            }

                            if (gattCharacteristicProperties.Any(x => x == Notify))
                            {
                                BleEndpoint.DebugWrite("AdvertisementHandler.ConfigureSensorService: Subscribing to the AllValues Indication/Notification");
                                m_characteristic.ValueChanged += Oncharacteristic_ValueChanged_Combined;

                                int loopCounter = 5;
                                GattCommunicationStatus status = GattCommunicationStatus.ProtocolError;
                                try
                                {
                                    while (status != GattCommunicationStatus.Success && loopCounter-- > 0)
                                    {
                                        if (bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                                        {
                                            BleEndpoint.DebugWrite($"AdvertisementHandler.ConfigureSensorService: CCCD Notify");
                                            status = await m_characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(NotifValue);
                                            if (status != GattCommunicationStatus.Success)
                                                await Task.Delay(SleepTimer);
                                        }
                                        else
                                            await Task.Delay(SleepTimer);
                                    }
                                    if (status != GattCommunicationStatus.Success)
                                    {
                                        BleEndpoint.DebugWrite($"AdvertisementHandler.ConfigureSensorService: CCCD Notify failed - Disconnecting");
                                        //StopAllBluetoothConnections();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    BleEndpoint.DebugWrite($"xxxx ConfigureSensorService: Exception c {ex.Message}");
                                    //StopAllBluetoothConnections();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BleEndpoint.DebugWrite("ConfigureSensorService Error: " + ex.Message);
            }
        }


        /// <summary>
        /// Stops listening for advertisements
        /// </summary>
        private void StopListening()
        {
            BleEndpoint.DebugWrite("StopListening");
            lock (m_ThreadLock)
            {
                m_isBusy = false;
                BleEndpoint.DebugWrite("StopListening: -> StoppedListening() - fireing event");
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
            Console.WriteLine("WatcherStartedListening");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
        }

        //private static void WatcherDeviceTimeout(BLEDevice device)
        //{
        //    Console.ForegroundColor = ConsoleColor.Red;
        //    Console.WriteLine($"Device timeout: {device}");
        //}

        private static void WatcherStoppedListening()
        {
            BleEndpoint.DebugWrite("WatcherStoppedListening");
            Console.ForegroundColor = ConsoleColor.Gray;
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

        private static void HookEvents(AdvertisementHandler advHandler)
        {
            Debug.WriteLine("HookEvents:");
            // Hook into events
            //watcher.NewDeviceDiscovered += WatcherNewDeviceDiscovered;
            //watcher.DeviceNameChanged += WatcherDeviceNameChanged;
            //watcher.DeviceTimeout += WatcherDeviceTimeout;
        }

        //private static void UnhookEvents(BleHub watcher)
        //{
        //    watcher.StartedListening -= WatcherStartedListening;
        //    watcher.StoppedListening -= WatcherStoppedListening;
        //    //watcher.NewDeviceDiscovered -= WatcherNewDeviceDiscovered;
        //    //watcher.DeviceNameChanged -= WatcherDeviceNameChanged;
        //    //watcher.DeviceTimeout -= WatcherDeviceTimeout;
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
        //    lock ( mThreadLock)
        //    {
        //        // The date in time that if less than means a device has timed out
        //        var threshold = DateTime.UtcNow - TimeSpan.FromSeconds(30);

        //        // Any devices that have not sent a new broadcast within the heartbeat time
        //         mDiscoveredDevices.Where(f => f.Value.BroadcastTime < threshold).ToList().ForEach(device =>
        //        {
        //            // Remove device
        //             mDiscoveredDevices.Remove(device.Key);

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
            if (m_bleHub != null && m_bleHub.Clients != null)
            {
                await m_bleHub.Clients.All.SendAsync("DeviceDisconnected");
            }
        }
        private async Task StopAllBluetoothConnections()
        {
            BleEndpoint.DebugWrite("AdvertisementHandler.StopAllBluetoothConnections");
            BluetoothLEDevice bleDevice = await GetBluetoothLEDevice();
            if (bleDevice != null)
            {
                bleDevice.ConnectionStatusChanged -= ConnectionStatusChangeHandler;
            }
            else
            {
                Debug.WriteLine("AdvertisementHandler.StopAllBluetoothConnections no bleDevice to Stop");
            }


            if (m_characteristic != null)
            {
                m_characteristic.ValueChanged -= Oncharacteristic_ValueChanged_Combined;
                //m_characteristic = null;
            }
            if (m_service != null)
            {
                m_service.Dispose();
                //m_service = null;
            }
            if (bleDevice != null)
            {
                bleDevice.Dispose();
                bleDevice = null;
                GC.Collect();
            }
        }

        public async void ConnectionStatusChangeHandler(BluetoothLEDevice bleDevice, Object o)
        {
            m_bluetoothAddress = bleDevice.BluetoothAddress;
            if (bleDevice == null)
            {
                BleEndpoint.DebugWrite($"ConnectionStatusChangeHandler: m_bluetoothLeDevice != bluetoothLEDevice ConnectionStatus={bleDevice?.ConnectionStatus} on bluetoothLEDevice={bleDevice?.GetHashCode()}");
                return;
            }

            BleEndpoint.DebugWrite($"ConnectionStatusChangeHandler: ConnectionStatus={bleDevice.ConnectionStatus} on BluetoothAddress={bleDevice.BluetoothAddress}");
            BleEndpoint.DebugWrite($"ConnectionStatusChangeHandler: IsPaired={bleDevice.DeviceInformation.Pairing.IsPaired}");

            if (bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                m_Watcher.Stop();
                await ConfigureSensorService(bleDevice);
            }

            if (bleDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                //StopAllBluetoothConnections();
                //await DeviceDisconnected();
                BleEndpoint.DebugWrite($"ConnectionStatusChangeHandler: after disconneced on BluetoothAddress={bleDevice.BluetoothAddress}");

                //Try reconnect
                StartScanningForPainSensors();
                if (m_bleHub.Clients != null)
                    await m_bleHub.Clients.All.SendAsync("ReconnectDevice");    // Fire signals to clients
            }
        }

        public void Oncharacteristic_ValueChanged_Combined(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            lastReceivedData = DateTime.Now;
            SendMessageToClient(sender, args);
        }
    }
}
