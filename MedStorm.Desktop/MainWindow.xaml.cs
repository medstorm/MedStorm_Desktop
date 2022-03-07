using Plot;
using PSSApplication.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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


        private BleEndpoint m_bleEndpoint = new BleEndpoint();
        AdvertisementHandler m_advHandler;
        private static BLEMeasurement LatestMeasurement { get; set; } = new BLEMeasurement(0, 0, 0, new double[5], 0);
        string m_temporarComment;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            m_advHandler = AdvertisementHandler.CreateAdvertisementHandler(null, "PainSensor");
            m_advHandler.NewMeasurement += AddMeasurement;
            ApplicationsComboBox.SelectedIndex = 0;
        }

        private void AddMeasurement(object? sender, MeasurementEventArgs e)
        {
            LatestMeasurement = e.Measurement;
            DateTime now = DateTime.Now;

            if (m_bleEndpoint.IsAcceptedRange(e.Measurement) && e.Message != "")
            {
                DataExportObject dataExportObject = new DataExportObject(now.ToString(), e.Measurement.PSS, e.Measurement.AUC, e.Measurement.NBV, e.Measurement.BS, e.Measurement.SC);
                DataExporter.AddData(dataExportObject);
            }

            bool isBadSignal = e.Measurement.BS != 0;

            skinPlot.AddData(new Measurement { Value = e.Measurement.SC[0], TimeStamp = now, IsBadSignal = isBadSignal });
            PainNociceptive.AddData(new Measurement { Value = e.Measurement.PSS, TimeStamp = now, IsBadSignal = isBadSignal });
            Awakening.AddData(new Measurement { Value = e.Measurement.AUC, TimeStamp = now, IsBadSignal = isBadSignal });
            NerveBlock.AddData(new Measurement { Value = e.Measurement.NBV, TimeStamp = now, IsBadSignal = isBadSignal });

            Dispatcher.Invoke(new Action(() =>
            {
                if (isBadSignal)
                {
                    PainNociceptiveValue.Text = "--";
                    AwakeningValue.Text = "--";
                    NerveBlockValue.Text = "--";
                }
                else
                {
                    PainNociceptiveValue.Text = e.Measurement.PSS.ToString();
                    AwakeningValue.Text = e.Measurement.AUC.ToString();
                    NerveBlockValue.Text = e.Measurement.NBV.ToString();
                }
            }));
        }

        private void connect_diconnect_Click(object sender, RoutedEventArgs e)
        {

            if (ConnectDisconnectButton.Content.ToString() == "Connect")
            {
                try
                {
                    m_advHandler.StartScanningForPainSensors();
                    ConnectDisconnectButton.Content = "Disconnect";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Not able to Connect to sensor, error={ex.Message}", "Connection Error", MessageBoxButton.OK);
                    ConnectDisconnectButton.Content = "Connect";
                }
            }
            else // Disconnect
            {
                try
                {
                    m_advHandler.StopScanningForPainSensors();
                    ConnectDisconnectButton.Content = "Connect";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Not able to Disconnect to sensor, error={ex.Message}", "Connection Error", MessageBoxButton.OK);
                    ConnectDisconnectButton.Content = "Connect";
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
            string? application = ((ComboBoxItem)ApplicationsComboBox.SelectedItem)?.Content?.ToString();
            switch (application)
            {
                case "Anaesthesia":
                    Debug.WriteLine("anaesthesia");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: true, PlotType.Awakening);
                    Switch(on: true, PlotType.NerveBlock);
                    break;

                case "PostOperative":
                    PainNociceptive.UpperLimit = 5;
                    Debug.WriteLine("postOperative");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: false, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "Icu":
                    Debug.WriteLine("icu");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: true, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "Infants":
                    Debug.WriteLine("infants");
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: false, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "Withdrawal":
                    Debug.WriteLine("withdrawal");
                    PainNociceptiveTextBox.Text = "Withdrawal";
                    Switch(on: true, PlotType.PainNociceptive);
                    Switch(on: false, PlotType.Awakening);
                    Switch(on: false, PlotType.NerveBlock);
                    break;

                case "NeuralBlock":
                    Debug.WriteLine("neuralBlock");
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
            Close();
        }

        private void ConnectMonitorButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            m_temporarComment= CommentTextBox.Text;
            CommentPopUp.IsOpen = true;
        }

        private void CancelCommentButton_Click(object sender, RoutedEventArgs e)
        {
            CommentTextBox.Text = m_temporarComment;
            CommentPopUp.IsOpen = false;
        }

        private void SaveCommentButton_Click(object sender, RoutedEventArgs e)
        {
            CommentPopUp.IsOpen = false;
        }

        private void CanelPatientIdButton_Click(object sender, RoutedEventArgs e)
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
    }

    public enum PlotType
    {
        PainNociceptive, Awakening, NerveBlock
    };
}

