﻿using Microsoft.Extensions.Configuration;
using Plot;
using PSSApplication.Common;
using PSSApplication.Core;
using PSSApplication.Core.PatientMonitor;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MedStorm.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Visibility PainNociceptiveVisibility
        {
            get { return (Visibility)GetValue(PainNociceptiveVisibilityProperty); }
            set { SetValue(PainNociceptiveVisibilityProperty, value); }
        }
        public static readonly DependencyProperty PainNociceptiveVisibilityProperty =
            DependencyProperty.Register("PainNociceptiveVisibility", typeof(Visibility), typeof(MainWindow), new PropertyMetadata(Visibility.Collapsed));

        public GridLength PainNociceptiveRow
        {
            get { return (GridLength)GetValue(PainNociceptiveRowProperty); }
            set { SetValue(PainNociceptiveRowProperty, value); }
        }
        public static readonly DependencyProperty PainNociceptiveRowProperty =
            DependencyProperty.Register("PainNociceptiveRow", typeof(GridLength), typeof(MainWindow), new PropertyMetadata(GridLength.Auto));


        public Visibility AwakeningVisibility
        {
            get { return (Visibility)GetValue(AwakeningVisibilityProperty); }
            set { SetValue(AwakeningVisibilityProperty, value); }
        }
        public static readonly DependencyProperty AwakeningVisibilityProperty =
            DependencyProperty.Register("AwakeningVisibility", typeof(Visibility), typeof(MainWindow), new PropertyMetadata(Visibility.Collapsed));

        public GridLength AwakeningRow
        {
            get { return (GridLength)GetValue(AwakeningRowProperty); }
            set { SetValue(AwakeningRowProperty, value); }
        }
        public static readonly DependencyProperty AwakeningRowProperty =
            DependencyProperty.Register("AwakeningRow", typeof(GridLength), typeof(MainWindow), new PropertyMetadata(GridLength.Auto));


        public Visibility NerveBlockVisibility
        {
            get { return (Visibility)GetValue(NerveBlockVisibilityProperty); }
            set { SetValue(NerveBlockVisibilityProperty, value); }
        }
        public static readonly DependencyProperty NerveBlockVisibilityProperty =
            DependencyProperty.Register("NerveBlockVisibility", typeof(Visibility), typeof(MainWindow), new PropertyMetadata(Visibility.Collapsed));

        public GridLength NerveBlockRow
        {
            get { return (GridLength)GetValue(NerveBlockRowProperty); }
            set { SetValue(NerveBlockRowProperty, value); }
        }
        public static readonly DependencyProperty NerveBlockRowProperty =
            DependencyProperty.Register("NerveBlockRow", typeof(GridLength), typeof(MainWindow), new PropertyMetadata(GridLength.Auto));

        const string ConnectSensor = "Connect Sensor";
        const string DisconnectSensor = "Disconnect Sensor";

        const string ConnectMonitor = "Connect Monitor";
        const string DisconnectMonitor = "Disconnect Monitor";

        string? Application => ((ComboBoxItem)ApplicationsComboBox.SelectedItem)?.Content?.ToString();
        bool IsRunning => ConnectSensorButton.Content.ToString() == DisconnectSensor;
        static BLEMeasurement LatestMeasurement { get; set; } = new BLEMeasurement(0, 0, 0, new double[5], 0);
        BleAdvertisementHandler? m_advHandler = null;
        MonitorHandler m_monitor;
        IConfigurationRoot m_configuration;
        bool m_isWaitingForPatientId = false;
        RawDataStorage m_rawDataStorage;
        string m_logFileWithPath = "";
        public MainWindow()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json");
                m_configuration = builder.Build();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wrong configuration (appsettings.json)!\n {ex.Message}");
                Close();
                return;
            }

            bool machineNameIsOk = false;
            string machineName = Environment.MachineName.ToLower();

            var keySection = m_configuration.GetSection("Keys");   //i.e. Machine names crypted
            if (machineName.Contains("medstorm") || keySection.Value == null)
            {
                machineNameIsOk = true;
            }
            else
            {
                var keys = keySection?.GetChildren()?.Select(x => x.Value)?.ToList<string>();
                MedStormCrypto crypto = new MedStormCrypto();
                if (keys != null)
                {
                    foreach (var cryptedMachineName in keys)
                    {
                        string? decryptedMachineName = crypto.DecryptString(cryptedMachineName);
                        if (!string.IsNullOrEmpty(decryptedMachineName) && decryptedMachineName?.ToLower() == machineName)
                        {
                            machineNameIsOk = true;
                            break;
                        }
                    }
                }
            }

            if (!machineNameIsOk)
            {
                Log.Information($"Computer {Environment.MachineName} is not certified for running this application");
                Log.CloseAndFlush();
                MessageBox.Show($"Computer {Environment.MachineName} si not certified for running this application\n" +
                                $"Please contact MedStorm on: https://med-storm.com/","Fatal Error", MessageBoxButton.OK);
                Close();
            }

            string logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PSS Application");
            m_logFileWithPath = System.IO.Path.Combine(logPath, "PainsSensor.log");

            // Logging file: C:\ProgramData\PSS Application\PainsSensoryyyyMMdd.log
            Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(m_configuration)
                        .WriteTo.File(m_logFileWithPath, rollingInterval: RollingInterval.Day)
                        .WriteTo.Debug()
                        .CreateLogger();

            Log.Information($"Running on computer= {Environment.MachineName} ");

            Log.Information("Starting MedStrom.Desktop..........................................");

            m_rawDataStorage = new RawDataStorage();
           

            InitializeComponent();
            ConnectMonitorButton.Content = ConnectMonitor;
            ConnectSensorButton.Content = ConnectSensor;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            m_advHandler = BleAdvertisementHandler.CreateAdvertisementHandler();
            m_advHandler.NewMeasurement += AddMeasurement;
            m_monitor = new MonitorHandler(m_advHandler);  
            ApplicationsComboBox.SelectedIndex = 0;
            PatientIdPopUp.Closed += PatientIdPopUp_Closed;
        }

        private void AddMeasurement(object? sender, MeasurementEventArgs eventArgs)
        {
            m_rawDataStorage.InsertDataPackage(eventArgs.Measurement);
            LatestMeasurement = eventArgs.Measurement;
            DateTime now = DateTime.Now;

            if (eventArgs.IsAcceptedRange() && eventArgs.Message != "")
            {
                PainSensorData dataExportObject = new PainSensorData(now.ToString(), eventArgs.Measurement.PSS, eventArgs.Measurement.AUC, eventArgs.Measurement.NBV, eventArgs.Measurement.BS, eventArgs.Measurement.SC);
            }

            bool isBadSignal = eventArgs.Measurement.BS != 0;

            skinPlot.AddData(new Measurement { Value = eventArgs.Measurement.SC[0], TimeStamp = now, IsBadSignal = isBadSignal });
            PainNociceptive.AddData(new Measurement { Value = eventArgs.Measurement.PSS, TimeStamp = now, IsBadSignal = isBadSignal });
            Awakening.AddData(new Measurement { Value = eventArgs.Measurement.AUC, TimeStamp = now, IsBadSignal = isBadSignal });
            NerveBlock.AddData(new Measurement { Value = eventArgs.Measurement.NBV, TimeStamp = now, IsBadSignal = isBadSignal });

            Dispatcher.Invoke(new Action(() =>
            {
                if (isBadSignal)
                {
                    PainNociceptiveValue.ValueText = "--";
                    AwakeningValue.ValueText = "--";
                    NerveBlockValue.ValueText = "--";
                }
                else
                {
                    PainNociceptiveValue.ValueText = eventArgs.Measurement.PSS.ToString();
                    AwakeningValue.ValueText = eventArgs.Measurement.AUC.ToString();
                    NerveBlockValue.ValueText = eventArgs.Measurement.NBV.ToString();
                }
            }));
        }

        private void PatientIdPopUp_Closed(object? sender, EventArgs e)
        {
            if (m_isWaitingForPatientId)
            {
                m_isWaitingForPatientId = false;
                m_rawDataStorage.SaveRawDataFile(PatientIdTextBox.Text);
            }
        }
        private void connect_diconnect_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRunning)
            {
                try
                {
                    CommentTextBox.Text = "";
                    PatientIdTextBox.Text = "";
                    Log.Debug("--------------------------------------------------------------------");
                    Log.Debug("MainWindow: connect-Click, creating Excel-File");
                    m_rawDataStorage.CreateRawDataFile();
                    m_advHandler?.StartScanningForPainSensors();
                    ConnectSensorButton.Content = DisconnectSensor;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Not able to Connect to sensor, error={ex.Message}";
                    MessageBox.Show(errorMsg, "Connection Error", MessageBoxButton.OK);
                    ConnectSensorButton.Content = ConnectSensor;
                    Log.Error($"MainWindow: Error={errorMsg}");
                }
            }
            else // Disconnect
            {
                try
                {
                    Log.Debug("MainWindow: diconnect-Click");
                    Log.Debug("--------------------------------------------------------------------");
                    m_advHandler?.StopScanningForPainSensors();
                    m_isWaitingForPatientId = true;
                    PatientIdPopUp.IsOpen = true;
                    ConnectSensorButton.Content = ConnectSensor;
                }
                catch (Exception ex)
                {
                    string errMsg = $"Not able to Disconnect to sensor!\n error={ex.Message}";
                    MessageBox.Show(errMsg, "Connection Error", MessageBoxButton.OK);
                    ConnectSensorButton.Content = ConnectSensor;
                    Log.Error($"MainWindow: diconnect-Click, error={errMsg}");
                }
            }
        }

        private void Switch(bool on, PlotType plotType)
        {
            if (on)
            {
                switch (plotType)
                {
                    case PlotType.PainNociceptive:
                        PainNociceptiveVisibility = Visibility.Visible;
                        PainNociceptiveRow = new GridLength(1, GridUnitType.Star);
                        break;

                    case PlotType.Awakening:
                        AwakeningVisibility = Visibility.Visible;
                        AwakeningRow = new GridLength(1, GridUnitType.Star);
                        break;

                    case PlotType.NerveBlock:
                        NerveBlockVisibility = Visibility.Visible;
                        NerveBlockRow = new GridLength(1, GridUnitType.Star);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (plotType)
                {
                    case PlotType.PainNociceptive:
                        PainNociceptiveVisibility = Visibility.Collapsed;
                        PainNociceptiveRow = new GridLength(0);
                        break;

                    case PlotType.Awakening:
                        AwakeningVisibility = Visibility.Collapsed;
                        AwakeningRow = new GridLength(0);
                        break;

                    case PlotType.NerveBlock:
                        NerveBlockVisibility = Visibility.Collapsed;
                        NerveBlockRow = new GridLength(0);
                        break;
                    default:
                        break;
                }
            }
        }
        private void ApplicationsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PainNociceptiveTextBox.Text = "Pain - Nociceptive"; // Default
            PainNociceptive.UpperLimit = 3;
            switch (Application)
            {
                case "Anaesthesia":
                    Log.Debug("anaesthesia");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: true, PlotType.Awakening);
                    Switch(on: true, PlotType.NerveBlock);
                    break;

                case "PostOperative":
                    PainNociceptive.UpperLimit = 5;
                    Log.Debug("postOperative");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: false, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "ICU":
                    Log.Debug("icu");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: true, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "Infants":
                    Log.Debug("infants");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: false, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "Withdrawal":
                    Log.Debug("withdrawal");
                    PainNociceptiveTextBox.Text = "Withdrawal";
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: false, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "NeuralBlock":
                    Log.Debug("neuralBlock");
                    Switch(on: false, PlotType.PainNociceptive);
                    Switch(on: false, PlotType.Awakening);
                    Switch(on: true, PlotType.NerveBlock);
                    break;

                default:
                    break;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsRunning)
            {
                m_rawDataStorage?.SaveRawDataFile("?");
            }
            Close();
        }

        private void ConnectMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ConnectMonitorButton.Content.ToString() == ConnectMonitor)
                {
                    bool connectionSuccessful = m_monitor.ConnectToMonitor();
                    if (connectionSuccessful)
                    {
                        Log.Debug("MainWindow: Connected to monitor");
                        ConnectMonitorButton.Content = DisconnectMonitor;
                    }
                    else
                    {
                        MessageBox.Show("Could not connect to monitor!", "Error", MessageBoxButton.OK);
                    }
                }
                else
                {
                    m_monitor.DisconnectMonitor();
                    ConnectMonitorButton.Content = ConnectMonitor;
                    Log.Debug("MainWindow: Disconnected from monitor");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection to monitor failed! \n {ex.Message}", "Error", MessageBoxButton.OK);
            }
        }

        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            CommentPopUp.IsOpen = true;
        }

        private void CancelCommentButton_Click(object sender, RoutedEventArgs e)
        {
            CommentPopUp.IsOpen = false;
        }

        private void SaveCommentButton_Click(object sender, RoutedEventArgs e)
        {
            CommentPopUp.IsOpen = false;
            m_rawDataStorage.AddComment(DateTime.Now, CommentTextBox.Text);
            Log.Debug($"Comment added={CommentTextBox.Text}");
            CommentTextBox.Text = "";
        }

        private void CanselPatientIdButton_Click(object sender, RoutedEventArgs e)
        {
            PatientIdPopUp.IsOpen = false;
        }

        private void SavePatientIdButton_Click(object sender, RoutedEventArgs e)
        {
            PatientIdPopUp.IsOpen = false;
        }

        private void PatientIdButton_Click(object sender, RoutedEventArgs e)
        {
            PatientIdPopUp.IsOpen = true;
        }

        private void MedstormLogo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            AboutPopUp.IsOpen = true;
            Assembly assemblyName = Assembly.GetExecutingAssembly();
            Type? gitVersionInformationType = assemblyName.GetType("GitVersionInformation");
            FieldInfo[]? fields = gitVersionInformationType?.GetFields();
            StringBuilder sb = new StringBuilder();
            List<string> tagsToPrint = new List<string> { "SemVer", "CommitDate" };
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    if (tagsToPrint.Contains(field.Name))
                        sb.AppendLine($"{field.Name}: {field.GetValue(null)}");
                }
                sb.AppendLine("LogFile:");
                sb.AppendLine(m_logFileWithPath);
            }
            VersionInfoTextBox.Text = sb.ToString();

        }

        private void AboutPopUp_OK_Button_Click(object sender, RoutedEventArgs e)
        {
            AboutPopUp.IsOpen = false;
        }

        private void DecreaseTimeScaleButton_Click(object sender, RoutedEventArgs e)
        {
            PainNociceptive.DecrementTimeToShow();
            Awakening.DecrementTimeToShow();
            NerveBlock.DecrementTimeToShow();
        }

        private void IncreaseTimeScaleButton_Click(object sender, RoutedEventArgs e)
        {
            PainNociceptive.IncrementTimeToShow();
            Awakening.IncrementTimeToShow();
            NerveBlock.IncrementTimeToShow();
        }
    }

    public enum PlotType
    {
        PainNociceptive, Awakening, NerveBlock
    };
}

