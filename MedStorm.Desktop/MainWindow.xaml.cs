using Plot;
using PSSApplication.Core;
using System;
using System.Collections.Generic;
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
        //public int PainNociceptiveValue
        //{
        //    get { return (int)GetValue(PainNociceptiveValueProperty); }
        //    set { SetValue(PainNociceptiveValueProperty, value); }
        //}
        //public static readonly DependencyProperty PainNociceptiveValueProperty =
        //    DependencyProperty.Register("PainNociceptiveValue", typeof(int), typeof(MainWindow), new PropertyMetadata(0));


        //public int AwakeningValue
        //{
        //    get { return (int)GetValue(AwakeningValueProperty); }
        //    set { SetValue(AwakeningValueProperty, value); }
        //}
        //public static readonly DependencyProperty AwakeningValueProperty =
        //    DependencyProperty.Register("AwakeningValue", typeof(int), typeof(MainWindow), new PropertyMetadata(0));


        //public int NerveBlockValue
        //{
        //    get { return (int)GetValue(NerveBlockValueProperty); }
        //    set { SetValue(NerveBlockValueProperty, value); }
        //}

        // Using a DependencyProperty as the backing store for NerveBlockValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NerveBlockValueProperty =
            DependencyProperty.Register("NerveBlockValue", typeof(int), typeof(MainWindow), new PropertyMetadata(0));



        private BleEndpoint m_bleEndpoint = new BleEndpoint();
        AdvertisementHandler m_advHandler;
        private static BLEMeasurement LatestMeasurement { get; set; } = new BLEMeasurement(0, 0, 0, new double[5], 0);
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            m_advHandler = AdvertisementHandler.CreateAdvertisementHandler(null, "PainSensor");
            m_advHandler.NewMeasurement += AddMeasurement;
        }

        private void AddMeasurement(object? sender,MeasurementEventArgs e)
        {
            LatestMeasurement = e.Measurement;
            DateTime now =  DateTime.Now;

            if (m_bleEndpoint.IsAcceptedRange(e.Measurement) && e.Message != "")
            {
                DataExportObject dataExportObject = new DataExportObject(now.ToString(), e.Measurement.PSS, e.Measurement.AUC, e.Measurement.NBV, e.Measurement.BS, e.Measurement.SC);
                DataExporter.AddData(dataExportObject);
            }

            skinPlot.AddData(new Measurement { Value = e.Measurement.SC[0], TimeStamp = now });
            PainNociceptive.AddData(new Measurement { Value = e.Measurement.PSS, TimeStamp = now });
            Awakening.AddData(new Measurement { Value = e.Measurement.AUC, TimeStamp = now });
            NerveBlock.AddData(new Measurement { Value = e.Measurement.NBV, TimeStamp = now });

            Dispatcher.Invoke(new Action(() =>
            {
                PainNociceptiveValue.Text = e.Measurement.PSS.ToString();
                AwakeningValue.Text = e.Measurement.AUC.ToString();
                NerveBlockValue.Text = e.Measurement.NBV.ToString();
            }));
        }

        private void connect_diconnect_Click(object sender, RoutedEventArgs e)
        {
            m_advHandler.StartScanningForPainSensors();
        }
    }
}
