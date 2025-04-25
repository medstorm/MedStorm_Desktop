using Microsoft.Extensions.Configuration;
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
using System.Runtime.InteropServices;
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

using NHapi.Base.Parser;
using NHapi.Model.V251.Message;
using NHapi.Model.V251.Segment;
using NHapi.Base.Model;

using System.Net;
using System.Net.Sockets;



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

        public string PatientIdButtonText
        {
            get { return (string)GetValue(PatientIdButtonTextProperty); }
            set { SetValue(PatientIdButtonTextProperty, value); }
        }
        public static readonly DependencyProperty PatientIdButtonTextProperty =
            DependencyProperty.Register("PatientIdButtonText", typeof(string), typeof(MainWindow), new PropertyMetadata("Enter Patient ID"));

        public int RSSI => (m_advHandler != null) ? m_advHandler.RSSI : 0;

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
        IPainSensorAdvertisementHandler? m_advHandler = null;
        MonitorHandler m_monitor;
        IConfigurationRoot m_configuration;
        //bool m_isWaitingSaveRecordingDialog = false;
        RawDataStorage m_rawDataStorage;
        string m_logFileWithPath = "";
        string m_patientId = "";
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
            if (machineName.Contains("medstorm") || keySection == null)
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

            // Check if we are running on a MedStorm certified machine
            if (!machineNameIsOk)
            {
                Log.Information($"Computer {Environment.MachineName} is not certified for running this application");
                Log.CloseAndFlush();
                MessageBox.Show($"Computer {Environment.MachineName} si not certified for running this application\n" +
                                $"Please contact MedStorm on: https://med-storm.com/", "Fatal Error", MessageBoxButton.OK);
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
            string? UseUSBdongleString = m_configuration.GetSection("UseUSBdongle")?.Value;

            if (!string.IsNullOrEmpty(UseUSBdongleString) && UseUSBdongleString.ToLower() == "true" )
                m_advHandler = DongleSensorAdvertisementHandler.CreateAdvertisementHandler();
            else
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

            Dispatcher.Invoke(() =>
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
            });

            // Create and send HL7 message
            var hl7Message = CreateHL7ORUMessage(eventArgs.Measurement);
            SendHL7Message(hl7Message);
        }

        private ORU_R01 CreateHL7ORUMessage(BLEMeasurement measurement)
        {
            var oruMessage = new ORU_R01();

            // Populate MSH segment
            var msh = oruMessage.MSH;
            msh.FieldSeparator.Value = "|";
            msh.EncodingCharacters.Value = "^~\\&";
            msh.SendingApplication.NamespaceID.Value = "MedStormDesktopApp";
            msh.SendingFacility.NamespaceID.Value = "MedStormTablet";
            msh.ReceivingApplication.NamespaceID.Value = "HospitalEMRorMirthConnect"; // Adjust as needed
            msh.DateTimeOfMessage.Time.Value = DateTime.Now.ToString("yyyyMMddHHmmss");
            msh.MessageType.MessageCode.Value = "ORU";
            msh.MessageType.TriggerEvent.Value = "R01";
            msh.MessageControlID.Value = Guid.NewGuid().ToString();
            msh.ProcessingID.ProcessingID.Value = "P";
            msh.VersionID.VersionID.Value = "2.5.1";

            // Add this line after setting MSH.VersionID.VersionID
            msh.GetCharacterSet(0).Value = "UTF-8";


            // Get the patient result group
            var patientResult = oruMessage.GetPATIENT_RESULT();

            // Populate PID segment
            var pid = patientResult.PATIENT.PID;
            pid.SetIDPID.Value = "1";
            pid.PatientID.IDNumber.Value = m_patientId;

            // Populate OBR segment
            var orderObservation = patientResult.GetORDER_OBSERVATION();
            var obr = orderObservation.OBR;
            obr.SetIDOBR.Value = "1";
            obr.UniversalServiceIdentifier.Identifier.Value = "PainSensorMeasurement";
            obr.UniversalServiceIdentifier.Text.Value = "Pain Sensor Measurement";
            obr.ObservationDateTime.Time.Value = DateTime.Now.ToString("yyyyMMddHHmmss");

            // Populate OBX segments
            var observations = orderObservation;

            // Helper method to add OBX segment
            void AddObx(string setId, string identifier, string text, string value, string units)
            {
                var obx = observations.AddOBSERVATION().OBX;
                obx.SetIDOBX.Value = setId;
                obx.ValueType.Value = "NM";
                obx.ObservationIdentifier.Identifier.Value = identifier;
                obx.ObservationIdentifier.Text.Value = text;
                obx.GetObservationValue(0).Data = new NHapi.Model.V251.Datatype.NM(oruMessage)
                {
                    Value = value
                };
                obx.Units.Identifier.Value = units;
                obx.ObservationResultStatus.Value = "F";
            }

            // Add OBX segments for each measurement
            AddObx("1", "PSS", "Pain Index", measurement.PSS.ToString(), "Score");
            AddObx("2", "AUC", "Awakening Index", measurement.AUC.ToString(), "Score");
            AddObx("3", "NBV", "Nerve Block Value", measurement.NBV.ToString(), "Score");
            AddObx("4", "BS", "Bad Signal", measurement.BS.ToString(), "Flag");
            // Using "uS" as units
            AddObx("5", "SC", "Skin Conductance", measurement.SC[0].ToString(), "uS");

            // Or, using "microS" as units
            // AddObx("5", "SC", "Skin Conductance", measurement.SC[0].ToString(), "microS");


            return oruMessage;
        }

        private async void SendHL7Message(ORU_R01 message)
        {
            try
            {
                // Encode the HL7 message
                PipeParser parser = new PipeParser();
                string encodedMessage = parser.Encode(message);

                // Add Start Block and End Block characters (as per MLLP protocol)
                string mllpMessage = $"\u000b{encodedMessage}\u001c\u000d";

                // Retrieve IP and port from configuration with error handling
                string ip = m_configuration["HL7Settings:ReceiverIP"] ?? "127.0.0.1";
                int port;
                if (!int.TryParse(m_configuration["HL7Settings:ReceiverPort"], out port))
                {
                    port = 6661; // Default port
                }

                // Log the IP and port being used
                Log.Information($"Sending HL7 message to {ip}:{port}");

                // Send the message over TCP
                await Task.Run(() =>
                {
                    using (TcpClient client = new TcpClient())
                    {
                        client.Connect(ip, port);
                        using (NetworkStream stream = client.GetStream())
                        {
                            byte[] messageBytes = Encoding.UTF8.GetBytes(mllpMessage);
                            stream.Write(messageBytes, 0, messageBytes.Length);
                            stream.Flush();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"Error sending HL7 message: {ex.Message}");
            }
        }

        // Event handler for QA-log button click
        private void QALogButton_Click(object sender, RoutedEventArgs e)
        {
            // Instantiate and open QA-log entry window
            var qaLogWindow = new QALogWindow();
            qaLogWindow.Owner = this;

            if (qaLogWindow.ShowDialog() == true)
            {
                // User confirmed and saved QA-log
                var logEntries = qaLogWindow.GetLogEntries();

                // Construct and send HL7 message with QA-log data
                var qaHL7Message = CreateQAHL7Message(logEntries);
                SendHL7Message(qaHL7Message);

                Log.Information("QA-log data sent via HL7.");
            }
        }

        // Method to create HL7 message specifically for QA-log data
        private ORU_R01 CreateQAHL7Message(Dictionary<string, string> qaLogEntries)
        {
            var oruMessage = new ORU_R01();

            // Populate MSH segment
            var msh = oruMessage.MSH;
            msh.FieldSeparator.Value = "|";
            msh.EncodingCharacters.Value = "^~\\&";
            msh.SendingApplication.NamespaceID.Value = "MedStormDesktopApp";
            msh.SendingFacility.NamespaceID.Value = "MedStormTablet";
            msh.ReceivingApplication.NamespaceID.Value = "HospitalEMRorMirthConnect";
            msh.DateTimeOfMessage.Time.Value = DateTime.Now.ToString("yyyyMMddHHmmss");
            msh.MessageType.MessageCode.Value = "ORU";
            msh.MessageType.TriggerEvent.Value = "R01";
            msh.MessageControlID.Value = Guid.NewGuid().ToString();
            msh.ProcessingID.ProcessingID.Value = "P";
            msh.VersionID.VersionID.Value = "2.5.1";
            msh.GetCharacterSet(0).Value = "UTF-8";

            // PID Segment
            var patientResult = oruMessage.GetPATIENT_RESULT();
            var pid = patientResult.PATIENT.PID;
            pid.SetIDPID.Value = "1";
            pid.PatientID.IDNumber.Value = m_patientId;

            // OBR Segment
            var orderObservation = patientResult.GetORDER_OBSERVATION();
            var obr = orderObservation.OBR;
            obr.SetIDOBR.Value = "1";
            obr.UniversalServiceIdentifier.Identifier.Value = "QALogData";
            obr.UniversalServiceIdentifier.Text.Value = "QA Log Data";
            obr.ObservationDateTime.Time.Value = DateTime.Now.ToString("yyyyMMddHHmmss");

            // Add OBX segments for QA-log entries
            int obxCounter = 1;
            foreach (var entry in qaLogEntries)
            {
                var obx = orderObservation.AddOBSERVATION().OBX;
                obx.SetIDOBX.Value = obxCounter.ToString();
                obx.ValueType.Value = "ST"; // String type for QA-log
                obx.ObservationIdentifier.Identifier.Value = entry.Key.Replace(" ", "");
                obx.ObservationIdentifier.Text.Value = entry.Key;
                obx.GetObservationValue(0).Data = new NHapi.Model.V251.Datatype.ST(oruMessage)
                {
                    Value = entry.Value
                };
                obx.Units.Identifier.Value = "N/A";
                obx.ObservationResultStatus.Value = "F";
                obxCounter++;
            }

            return oruMessage;
        }



        private void PatientIdPopUp_Closed(object? sender, EventArgs e)
        {
        }
        private void connect_diconnect_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRunning) // Connect
            {
                try
                {
                    //CommentTextBox.Text = "";
                    //PatientIdTextBox.Text = "";
                    Log.Debug("--------------------------------------------------------------------");
                    Log.Debug("MainWindow: connect-Click, creating Excel-File");
                    m_rawDataStorage.CreateRawDataFile(m_patientId);
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
                    //m_isWaitingSaveRecordingDialog = true;
                    SaveRecordingPatientIdTextBox.Text = m_patientId;
                    SaveRecordingPopUp.IsOpen = true;
                    ConnectSensorButton.Content = ConnectSensor;
                }
                catch (Exception ex)
                {
                    string errMsg = $"Not able to Disconnect to sensor!\n error={ex.Message}";
                    MessageBox.Show(errMsg, "Disconnect Error", MessageBoxButton.OK);
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
            m_advHandler?.Close();
            if (IsRunning)
            {
                m_rawDataStorage?.SaveRawDataFile("");
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

        private void ClearPatientInfo()
        {
            // Reset patient information
            PatientIdTextBox.Text = "";
            CommentTextBox.Text = "";
            m_patientId = "";
            SetNameOnPatientIdButton();
        }

        private void SavePatientIdButton_Click(object sender, RoutedEventArgs e)
        {
            m_patientId = PatientIdTextBox.Text;
            SetNameOnPatientIdButton();
            PatientIdPopUp.IsOpen = false;
        }

        private void CancelPatientIdButton_Click(object sender, RoutedEventArgs e)
        {
            PatientIdTextBox.Text = m_patientId;
            PatientIdPopUp.IsOpen = false;
            SetNameOnPatientIdButton();
        }

        private void SaveRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRecordingPopUp.IsOpen = false;
            m_patientId = SaveRecordingPatientIdTextBox.Text;
            m_rawDataStorage.UpdatePatientId(m_patientId);
            m_rawDataStorage.SaveRawDataFile(m_patientId);

            ClearPatientInfo();
        }

        private void NoSaveRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            SaveRecordingPopUp.IsOpen = false;
            m_rawDataStorage.DeleteRawDataFile();
            ClearPatientInfo();
        }

        private void SetNameOnPatientIdButton()
        {
            if (string.IsNullOrEmpty(m_patientId))
            {
                PatientIdButton.Content = "Enter PatientId";
                PatientIdTextBox.Text = "";
            }
            else
            {
                PatientIdButton.Content = $"PatientID: {m_patientId}";
                PatientIdTextBox.Text = m_patientId;
            }
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
            PainNociceptive.IncrementTimeToShow();
            Awakening.IncrementTimeToShow();
            NerveBlock.IncrementTimeToShow();
        }

        private void IncreaseTimeScaleButton_Click(object sender, RoutedEventArgs e)
        {
            PainNociceptive.DecrementTimeToShow();
            Awakening.DecrementTimeToShow();
            NerveBlock.DecrementTimeToShow();
        }
    }

    public enum PlotType
    {
        PainNociceptive, Awakening, NerveBlock
    };
}

