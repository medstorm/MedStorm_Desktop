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
using Serilog;

namespace PSSApplication.Core
{
    public class PainSensorAdvertisementHandler
    {
        public event EventHandler<MeasurementEventArgs> NewMeasurement;
        public static BLEMeasurement LatestMeasurement { get; private set; } = new BLEMeasurement(0, 0, 0, new double[5], 0);

        static readonly string ServiceUuid = "264eaed6-c1da-4436-b98c-db79a7cc97b5";
        static readonly string CombinedUuid = "14abde20-31ed-4e0a-bdcf-7efc40f3fffb";

        readonly object m_ThreadLock = new object();
        BluetoothLEDevice m_bleDevice = null;
        GattDeviceService m_service = null;
        GattCharacteristic m_characteristic = null;
        BluetoothLEAdvertisementWatcher m_Watcher; // The underlying bluetooth watcher class

        const int NumOfCondItems = 5;
        const int NumBytesFloats = 4;

        const string AdvertisementName = "PainSensor";

        bool m_isBusy = false;
        public bool Listening => m_Watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started;
        public int SleepTimer = 1000;
        public static bool IsRunning { get; private set; }

        ulong? m_bluetoothAddress = null;

        async Task<BluetoothLEDevice> GetBluetoothLEDevice()
        {
            if (m_bluetoothAddress == null)
            {
                Log.Error("AdvertisementHandler.GetBluetoothLEDevice: Sensor not available m_bluetoothAddress==null");
                return null;
            }
            m_bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(m_bluetoothAddress.Value);
            if (m_bleDevice == null)
            {
                m_isBusy = false;
                Log.Error("AdvertisementHandler.GetBluetoothLEDevice: Not able to get bleDevice (Sensor)");
                return null;
            }
            Log.Debug($"AdvertisementHandler.GetBluetoothLEDevice: Connection status= {m_bleDevice.ConnectionStatus}, m_bleDevice.ID={m_bleDevice.GetHashCode()}");
            return m_bleDevice;
        }

        DateTime lastReceivedData = DateTime.MinValue;
        static PainSensorAdvertisementHandler()
        {
            IsRunning = false;
        }

        public static PainSensorAdvertisementHandler m_advertisementHandlerSingleton = null;
        public static PainSensorAdvertisementHandler AdvertisementMgr
        {
            get => m_advertisementHandlerSingleton; private set => m_advertisementHandlerSingleton = value;
        }

        public static PainSensorAdvertisementHandler CreateAdvertisementHandler()
        {
            if (m_advertisementHandlerSingleton == null)
                m_advertisementHandlerSingleton = new PainSensorAdvertisementHandler();

            return m_advertisementHandlerSingleton;
        }
        
        private PainSensorAdvertisementHandler()
        {
            Log.Debug("AdvertisementHandler.ctor - createing new watcher");
            m_Watcher = new BluetoothLEAdvertisementWatcher();
            m_Watcher.Received += Watcher_Received;
            m_Watcher.Stopped += Watcher_Stopped;
        }

        public void CheckStatus(Object stateInfo)
        {
            if (IsRunning && lastReceivedData < (DateTime.Now - TimeSpan.FromSeconds(10)))
            {
                Log.Error($"Expected datastream dosn't work. lastReceivedData={lastReceivedData.ToLocalTime()} Restarting connection to sensor");
                StopScanningForPainSensors();
                StartScanningForPainSensors();
            }
        }

        private void Watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            Log.Debug($"AdvertisementHandler.Watcher_Stopped: status={args.Error}");
        }

        public void StartScanningForPainSensors()
        {
            Log.Debug("AdvertisementHandler.StartScanningForPainSensors");
            if (m_Watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                return;

            Log.Information($"AdvertisementHandler.StartScanningForPainSensors: Starting Watcher");
            m_Watcher.Start();
            lastReceivedData = DateTime.Now;    // To reset status check timer
            IsRunning = true;
        }

        public async void StopScanningForPainSensors()
        {
            Log.Debug("AdvertisementHandler.StopScanningForPainSensors");
            IsRunning = false;

            if (m_Watcher != null)
                m_Watcher.Stop();

            await StopAllBluetoothConnections();
        }

        private async Task<DeviceUnpairingResult> UnpairDevice(bool closeConnection=true)
        {
            Log.Debug("AdvertisementHandler.UnpairDevice:");
            if (m_bleDevice?.DeviceInformation?.Pairing == null)
            {
                Log.Error("AdvertisementHandler.UnpairDevice: Not able to get m_bleDevice.DeviceInformation.Pairing ");
                return null;
            }
            
            DeviceUnpairingResult unpairResult = null;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //Try to unpair for 30 seconds, then fail
            do
            {
                unpairResult = await m_bleDevice.DeviceInformation.Pairing.UnpairAsync();
                Log.Debug($"AdvertisementHandler.UnpairDevice: Pairing Result= {unpairResult.Status}");
            }
            while (unpairResult.Status != DeviceUnpairingResultStatus.Unpaired
                   && unpairResult.Status != DeviceUnpairingResultStatus.AlreadyUnpaired
                   && stopwatch.ElapsedMilliseconds < 30000);

            stopwatch.Stop();
            if (closeConnection && m_bleDevice != null)
            {
                Log.Debug($"AdvertisementHandler.UnpairDevice: Closing connection, m_bleDevice.ID= {m_bleDevice.GetHashCode()}");
                m_bleDevice.Dispose();
                m_bleDevice = null;
            }
            Log.Information($"AdvertisementHandler.UnpairDevice: unpairResult={unpairResult?.Status}");
            return unpairResult;
        }

        private async Task<DevicePairingResult> PairDevice()
        {
            Log.Debug("AdvertisementHandler.PairDevice:");
            if (m_bleDevice == null)
            {
                Log.Error("AdvertisementHandler.PairDevice: Not able to get bleDevice ");
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
                    Log.Debug($"AdvertisementHandler.PairDevice: Pairing...");
                    m_bleDevice.DeviceInformation.Pairing.Custom.PairingRequested += Custom_PairingRequested;
                    devicePairingResult = await m_bleDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
                    m_bleDevice.DeviceInformation.Pairing.Custom.PairingRequested -= Custom_PairingRequested;
                    Log.Debug($"AdvertisementHandler.PairDevice: result: {devicePairingResult.Status}");

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
                Log.Error($"PairDevice error: {ex.Message}");
                return null;
            }
        }

        public bool IsPaired()
        {
            Log.Debug("AdvertisementHandlerIsPaired:");
            if (m_bleDevice == null)
            {
                Log.Debug("AdvertisementHandler.AdvertisementHandler: Not able to get bleDevice ");
                return false;
            }

            return m_bleDevice.DeviceInformation.Pairing.IsPaired;
        }

        private async void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            //Log.Debug($"AdvertisementHandler.Watcher_Received: m_busy={m_isBusy}, BluetoothAddress={args.BluetoothAddress} ");
            if (m_isBusy)
                return;

            if (!string.IsNullOrWhiteSpace(args.Advertisement.LocalName))
                Log.Debug($"AdvertisementHandler.Watcher_Received: Advertisement Discovered from {args.Advertisement.LocalName}");

            if (args.Advertisement.LocalName != AdvertisementName)
                return;

            try
            {
                m_isBusy = true;
                m_Watcher.Stop();

                Log.Debug("AdvertisementHandler.Watcher_Received: Stoped watcher, Starting to pair...");
                m_bluetoothAddress = args.BluetoothAddress;
                await GetBluetoothLEDevice();   // This will also start the connection to the sensor (implicit)

                if (m_bleDevice == null)
                {
                    Log.Error("AdvertisementHandler.Watcher_Received: Not able to get m_bleDevice, Starting watcher ");
                    m_isBusy = false;
                    m_Watcher.Start();
                    return;
                }

                Log.Information($"AdvertisementHandler.Watcher_Received: Attached BluetoothAddress={m_bleDevice.BluetoothAddress}, Connection={m_bleDevice.ConnectionStatus}");

                m_bleDevice.ConnectionStatusChanged += ConnectionStatusChangeHandler;
                Log.Debug($"AdvertisementHandler.Watcher_Received: Added ConnectionStatusChanged-handler to bleDevice.ID={m_bleDevice.GetHashCode()} - Connection={m_bleDevice.ConnectionStatus}");

                DeviceUnpairingResult unpairingResult = null;
                if (m_bleDevice.DeviceInformation.Pairing.IsPaired)
                {
                    Log.Debug($"AdvertisementHandler.Watcher_Received: Unparing Device, IsPaird=true before UnpairDevice()");
                    unpairingResult = await UnpairDevice(closeConnection: false);
                }

                Log.Debug($"AdvertisementHandler.Watcher_Received: Pairing Device");
                DevicePairingResult result = await PairDevice();
                if (result != null && result.Status == DevicePairingResultStatus.Paired)
                {
                    Log.Information($"AdvertisementHandler.Watcher_Received: Paired to {args.BluetoothAddress}, connectionOk= {m_bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected}");
                    m_isBusy = false;
                }
                else
                {
                    //Failed to connect - Disconnect gracefully
                    Log.Error($"AdvertisementHandler.Watcher_Received: Failed to paire, result={result?.Status}");
                    m_isBusy = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AdvertisementHandler.Watcher_Received error: ex.Message={ex.Message},");
                await UnpairDevice();
                Log.Error($"AdvertisementHandler.Watcher_Received: Starting Watcher");
                m_Watcher.Start();
                m_isBusy = false;
            }
        }

        private async Task ConfigureSensorService()
        {
            Log.Debug($"AdvertisementHandler.ConfigureSensorService Connection={m_bleDevice?.ConnectionStatus}");
            try
            {
                var Notify = GattCharacteristicProperties.Notify;
                var NotifValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;

                if (m_bleDevice != null)
                {
                    var result = await m_bleDevice.GetGattServicesForUuidAsync(Guid.Parse(ServiceUuid));
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        var services = result.Services;
                        await Task.Delay(SleepTimer);
                        Log.Debug($"AdvertisementHandler.ConfigureSensorService: Services Count= {result.Services.Count}", true);
                        m_service = result.Services[0];

                        Log.Debug($"AdvertisementHandler.ConfigureSensorService: Service { m_service.Uuid} found and accessed!");
                        var serviceAccess = await m_service.RequestAccessAsync();
                        GattCharacteristicsResult characteristicResultAllValues = await m_service.GetCharacteristicsForUuidAsync(Guid.Parse(CombinedUuid));
                        if (characteristicResultAllValues.Status == GattCommunicationStatus.Success)
                        {
                            if (characteristicResultAllValues.Characteristics.Count == 0)
                            {
                                Log.Debug($"AdvertisementHandler.ConfigureSensorService: No characteristics available in UUID {Guid.Parse(CombinedUuid)}");
                                return;
                            }
                            m_characteristic = characteristicResultAllValues.Characteristics[0];
                            //characteristicResultAllValues = null;
                            Log.Debug($"AdvertisementHandler.ConfigureSensorService: Characteristic AllValues { m_characteristic.Uuid} found and accessed!");

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
                                Log.Debug("AdvertisementHandler.ConfigureSensorService: Subscribing to the AllValues Indication/Notification");
                                m_characteristic.ValueChanged += Oncharacteristic_ValueChanged_Combined;

                                int loopCounter = 5;
                                GattCommunicationStatus status = GattCommunicationStatus.ProtocolError;
                                try
                                {
                                    while (status != GattCommunicationStatus.Success && loopCounter-- > 0)
                                    {
                                        if (m_bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                                        {
                                            Log.Debug($"AdvertisementHandler.ConfigureSensorService: Connected, sending CCCD Notify to receive measurement from PainSensor");
                                            status = await m_characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(NotifValue);
                                            if (status != GattCommunicationStatus.Success)
                                                await Task.Delay(SleepTimer);
                                        }
                                        else
                                            await Task.Delay(SleepTimer);
                                    }
                                    if (status != GattCommunicationStatus.Success)
                                    {
                                        Log.Debug($"AdvertisementHandler.ConfigureSensorService: CCCD Notify failed - Disconnecting");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"xxxx ConfigureSensorService: Exception c {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("ConfigureSensorService Error: " + ex.Message);
            }
        }

        private void Custom_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Log.Debug("AdvertisementHandler.Custom_PairingRequested");
            args.Accept();
        }

        private static void WatcherStoppedListening()
        {
            Log.Debug("WatcherStoppedListening");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

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
        private void SendMessageToClient(GattValueChangedEventArgs args)
        {
            if (!IsRunning)
                return;

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
            LatestMeasurement = measurementsArgs.Measurement;
            Log.Verbose(measurementsArgs.Message);
            NewMeasurement?.Invoke(this, measurementsArgs);
        }

        private async Task StopAllBluetoothConnections()
        {
            Log.Debug("AdvertisementHandler.StopAllBluetoothConnections");
            if (m_bleDevice != null)
            {
                m_bleDevice.ConnectionStatusChanged -= ConnectionStatusChangeHandler;
                Log.Debug($"AdvertisementHandler.StopAllBluetoothConnections removed handler, bleDevice.ID={m_bleDevice.GetHashCode()} ");
                await UnpairDevice();
            }
            else
            {
                Log.Debug("AdvertisementHandler.StopAllBluetoothConnections no bleDevice to Stop");
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
        }

        public async void ConnectionStatusChangeHandler(BluetoothLEDevice bleDevice, Object o)
        {
            m_bluetoothAddress = bleDevice.BluetoothAddress;
            if (bleDevice == null)
            {
                Log.Debug($"AdvertisementHandler.ConnectionStatusChangeHandler: bleDevice == null");
                return;
            }

            Log.Debug($"AdvertisementHandler.ConnectionStatusChangeHandler: ConnectionStatus={bleDevice.ConnectionStatus} on BluetoothAddress={bleDevice.BluetoothAddress}");
            Log.Debug($"AdvertisementHandler.ConnectionStatusChangeHandler: IsPaired={bleDevice.DeviceInformation.Pairing.IsPaired}");
            Log.Debug($"AdvertisementHandler.ConnectionStatusChangeHandler: bleDevice.ID={bleDevice.GetHashCode()}, m_bleDevice.ID ={m_bleDevice.GetHashCode()}");

            if (bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                m_Watcher.Stop();
                await ConfigureSensorService();
            }

            if (bleDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                Log.Debug($"AdvertisementHandler.ConnectionStatusChangeHandler: Disconneced on BluetoothAddress={bleDevice.BluetoothAddress}, trying to restart");
                if (m_bleDevice != null)
                    m_bleDevice.ConnectionStatusChanged -= ConnectionStatusChangeHandler;

                //Try reconnect, by unpairing device and start watcher (other way of reconnecting, dosn't seem to work) 
                if (m_bleDevice?.DeviceInformation?.Pairing != null)
                {
                    await UnpairDevice();
                }

                Log.Debug("AdvertisementHandler.ConnectionStatusChangeHandler: Starting watcher");
                m_isBusy = false;
                m_Watcher.Start();
            }
        }

        public void Oncharacteristic_ValueChanged_Combined(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            lastReceivedData = DateTime.Now;
            SendMessageToClient(args);
        }
    }
}
