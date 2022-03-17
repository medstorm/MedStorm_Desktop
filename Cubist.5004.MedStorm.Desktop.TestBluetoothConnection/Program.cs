using PSSApplication.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using PSSApplication.Core.PatientMonitor;
using System.Threading.Tasks;

namespace Testing
{
	/// <summary>
    ///   This applicatioin is used to test the bluetooth connection 
    ///   with the sensor/mock android without be depended on a working client application
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            try
            {
                //var watcher = new BleHub(configuration.GetValue<string>("AdvertisingName"));
                var watcher =  AdvertisementHandler.CreateAdvertisementHandler("");
                var monitor = new MonitorHandler(configuration);
                Console.WriteLine("Enter 's' to start monitor");
                Console.WriteLine("Enter 't' to stop monitor");
                Console.WriteLine("Enter 'p' to start monitor philips monitor");
                Console.WriteLine("Enter 'q' to quit");

                while (true)
                {

                    DateTime beginWait = DateTime.Now;
                    while (!Console.KeyAvailable && DateTime.Now.Subtract(beginWait).TotalSeconds < 5)
                        Thread.Sleep(1000);

                    if (Console.KeyAvailable)
                    {
                        switch (Console.ReadKey().KeyChar)
                        {
                            case 's'://Restart listening
                                watcher.StartScanningForPainSensors();
                                break;
                            case 't':
                                watcher.StopScanningForPainSensors();
                                break;
                            case 'p':
                                monitor.ConnectToMonitor();
                                break;
                            case 'x':
                            case 'q':
                                watcher.StopScanningForPainSensors();
                                Thread.Sleep(2000);
                                watcher = null;
                                return;
                            default:
                                break;

                        }

                    }

                }
            }
            finally
            {

            }
        }
        
    }
}
