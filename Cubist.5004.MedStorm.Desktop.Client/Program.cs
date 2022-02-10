using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace PSSApplication.Client
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddJsonFile("certificate.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"certificate.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
                .Build();

            var certificateSettings = config.GetSection("certificateSettings");
            string certificateFileName = certificateSettings.GetValue<string>("filename");
            string certificatePassword = certificateSettings.GetValue<string>("password");
            var certificate = new X509Certificate2();
            try { 
                certificate = new X509Certificate2(certificateFileName, certificatePassword);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Find file certificate");
                Log.Information("File name : " + certificateFileName);
                Log.CloseAndFlush();
                return;
            }
            try
            {
                Log.Information("Starting up");
                CreateHostBuilder(args, config, certificate).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application start-up failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args, IConfiguration config, X509Certificate2 certificate) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()

                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(
                        options =>
                        {
                            options.AddServerHeader = false;
                            options.Listen(IPAddress.Loopback, 5001, listenOptions =>
                            {
                                listenOptions.UseHttps(certificate);
                            });
                        });
                    webBuilder.UseConfiguration(config);
                    webBuilder.UseContentRoot(Directory.GetCurrentDirectory());
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("https://medstorm:5001");
                    
                });
        

    }
}
