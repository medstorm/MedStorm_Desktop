// See https://aka.ms/new-console-template for more information

using System;
using System.Device.Gpio;
using System.Threading;
using System.IO.Ports;
using PSSApplication.Core.PatientMonitor;
using Serilog;
using Microsoft.Extensions.Configuration;
using PSSApplication.Core;
using PSSApplication.Common;

MonitorHandler m_monitorHandler;

while (true)
{
    try
    {
        IConfigurationRoot configuration;

        var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json");
        configuration = builder.Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Debug()
            .CreateLogger();

        Console.WriteLine($"Running on computer= {Environment.MachineName} ");
        Console.WriteLine("Starting MedStrom Conectivty box");

        DongleSensorAdvertisementHandler dongleHandler = new DongleSensorAdvertisementHandler();
        m_monitorHandler = new MonitorHandler(dongleHandler);
        m_monitorHandler.ConnectToMonitor();
        dongleHandler.StartScanningForPainSensors();
    }
    catch (Exception ex)
    {
        Log.Error($"Outer: {ex.Message}");
        Thread.Sleep(5000);
    }
}