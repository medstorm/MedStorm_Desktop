//using System;
//using System.Collections.Generic;
//using System.Text;
//using Windows.Devices.Bluetooth.GenericAttributeProfile;

//namespace PSSApplication.Core
//{
//    /// <summary>
//    /// Information about a BLE device
//    /// </summary>
//    public class BLEDevice
//    {
//        public DateTimeOffset BroadcastTime { get; }

//        public ulong Address { get; }

//        public string Name { get; }

//        public short SignalStrengthInDB { get; }

//        public bool Connected { get; }

//        public string DeviceId { get; }

//        public List<GattDeviceService> GattDeviceServices { get; set; }


//        public BLEDevice() { }
//        /// <summary>
//        /// Default constructor
//        /// </summary>
//        public BLEDevice(
//            ulong address,
//            string name,
//            short rssi,
//            DateTimeOffset broadcastTime,
//            bool connected,
//            string deviceId,
//            List<GattDeviceService> gattDeviceServices
//            )
//        {
//            Address = address;
//            Name = name;
//            SignalStrengthInDB = rssi;
//            BroadcastTime = broadcastTime;
//            Connected = connected;
//            DeviceId = deviceId;
//            GattDeviceServices = gattDeviceServices;
//        }

//        /// <summary>
//        /// User friendly ToString
//        /// </summary>
//        /// <returns></returns>
//        public override string ToString()
//        {
//            return $"{ (string.IsNullOrEmpty(Name) ? "[No Name]" : Name) } [{DeviceId}] ({SignalStrengthInDB})";
//        }
//    }
//}
