using BezierCurve;
using PSSApplication.Core;
using Serilog;
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
using System.Windows.Threading;

namespace Plot
{
    /// <summary>
    /// Interaction logic for RealTimePlotUC.xaml
    /// </summary>
    public partial class RealTimePlotUC : UserControl
    {
        public string PlotTitle
        {
            get { return (string)GetValue(PlotTitleProperty); }
            set { SetValue(PlotTitleProperty, value); }
        }
        public static readonly DependencyProperty PlotTitleProperty =
            DependencyProperty.Register("PlotTitle", typeof(string), typeof(RealTimePlotUC), new PropertyMetadata(""));

        public Brush FillAreaBrush { get; set; }
        public bool HasFixedVerticalLabels { get; set; } = true;
        public double NoOfSecondsToShow { get; set; } = 15;
        public bool PlotCurveWithArea { get; set; }
        public int NoOfHorizontalGridLines { get; set; } = 5;
        public int MaxValue { get; set; } = 10;
        public int LowerLimit { get; set; } = 1;
        public int NoOfVerticalGridLines { get; set; } = 3;
        public List<Measurement> DataPoints { get; private set; }
        public string HorizontalAxisLabels { get; set; } = ""; //0, 1, 3, 5, 7, 8, 10

        public int UpperLimit
        {
            get { return (int)GetValue(UpperLimitProperty); }
            set { SetValue(UpperLimitProperty, value); }
        }
        public static readonly DependencyProperty UpperLimitProperty =
            DependencyProperty.Register("UpperLimit", typeof(int), typeof(RealTimePlotUC),
                new FrameworkPropertyMetadata(4, UpperLimitChanged));

        private static void UpperLimitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as RealTimePlotUC;
            if (instance != null)
                instance.PlotUserControl_SizeChanged(null, null);
        }

        double m_maxValue = 2.5;
        double m_minValue = 0;
        double m_valueToPixel = 1;
        double m_pixelHeight = 0;
        double m_pixelWidth = 0;
        double m_secondToPixel = 0;
        object m_threadLock = new object();
        PathSegmentCollection m_pathSegments = new PathSegmentCollection();
        Path m_path = new Path();
        Path m_badSignalPath = new Path();
        CurveGenerator m_curveGenerator;
        DateTime m_startTime;
        List<int> m_horizontalAxisValues = new List<int>();
        Brush m_upperLowerLimitBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#055d7a"));//#FF03303F"));
        Brush m_whiteBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c5e3ee"));
        Brush m_badSignalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B9DA1"));
        public RealTimePlotUC()
        {
            InitializeComponent();
            FillAreaBrush = Brushes.Green;
            m_curveGenerator = new CurveGenerator(m_path);
            DataPoints = new List<Measurement>();
        }

        const int MinuteSeconds = 60;
        const int HourSeconds = 3600;  //60sec * 60 min; 
        const int TreeHourSeconds = HourSeconds * 3;
        const int NineHourSeconds = HourSeconds * 9;
        public void IncrementTimeToShow()
        {

            switch (NoOfSecondsToShow)
            {
                case MinuteSeconds:
                    NoOfSecondsToShow = HourSeconds;
                    break;

                case HourSeconds:
                    NoOfSecondsToShow = TreeHourSeconds;
                    break;

                case TreeHourSeconds:
                    NoOfSecondsToShow = NineHourSeconds;
                    break;

                default:
                    break;
            }
            Init();
            AddGrid();
        }

        public void DecrementTimeToShow()
        {

            switch (NoOfSecondsToShow)
            {
                case HourSeconds:
                    NoOfSecondsToShow = MinuteSeconds;
                    break;

                case TreeHourSeconds:
                    NoOfSecondsToShow = HourSeconds;
                    break;

                case NineHourSeconds:
                    NoOfSecondsToShow = TreeHourSeconds;
                    break;

                default:
                    break;
            }
            Init();
            AddGrid();
        }

        private void Init()
        {
            m_pixelHeight = plotCanvas.ActualHeight;
            m_pixelWidth = plotCanvas.ActualWidth;
            m_secondToPixel = m_pixelWidth / NoOfSecondsToShow;
        }
        private void PlotUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Init();

            m_horizontalAxisValues.Clear();
            string[] labels = HorizontalAxisLabels.Split(',');
            foreach (var label in labels)
            {
                if (int.TryParse(label, out int labelValue))
                {
                    m_horizontalAxisValues.Add(labelValue);
                }
            }

            DispatcherTimer animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(15);
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        public void AddData(Measurement sample)
        {
            m_startTime = DateTime.Now - TimeSpan.FromSeconds(NoOfSecondsToShow);
            lock (m_threadLock)
            {
                DataPoints.Add(sample);

                // Remove old data
                for (int i = DataPoints.Count - 1; i >= 0; i--)
                {
                    if (DataPoints[i].TimeStamp < (m_startTime - TimeSpan.FromSeconds(NineHourSeconds)))
                        DataPoints.RemoveAt(i);
                }
            }
        }

        private void PlotUserControl_SizeChanged(object? sender, SizeChangedEventArgs? e)
        {
            Init();
            AddGrid();
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            lock (m_threadLock)
            {
                try
                {
                    m_startTime = DateTime.Now - TimeSpan.FromSeconds(NoOfSecondsToShow);
                    m_path = new Path();
                    m_curveGenerator = new CurveGenerator(m_path);

                    if (PlotCurveWithArea)
                        PlotWithArea();
                    else
                        Plot();

                    plotCanvas.Children.Clear();
                    plotCanvas.Children.Add(m_path);
                    plotCanvas.Children.Add(m_badSignalPath);

                    // Add value-labels if they are not fixed
                    if (!HasFixedVerticalLabels)
                    {
                        valueLabels.Children.Clear();
                        string valueLabelText = "";
                        double valueRange = m_maxValue - m_minValue;
                        //if (valueRange < 5) valueRange = 5;
                        double valueStep = valueRange / NoOfHorizontalGridLines;
                        for (int i = 0; i <= NoOfHorizontalGridLines; i++)
                        {
                            double labelValue = valueStep * (NoOfHorizontalGridLines - i) + m_minValue;
                            if (valueRange <= 10)
                                valueLabelText = string.Format("{0:f1}", labelValue);
                            else
                                valueLabelText = string.Format("{0:f0}", labelValue);

                            valueLabels.Children.Add(new TextBlock
                            {
                                Text = valueLabelText,
                                FontFamily = new FontFamily("Courier"),
                                FontSize = 10,
                                Foreground = m_whiteBrush,
                                TextAlignment = TextAlignment.Right,
                                Margin = new Thickness(0, 0, 0, (m_pixelHeight / NoOfHorizontalGridLines) - 12)
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"AnimationTimer_Tick: {ex.Message}");
                }
            }
        }

        private void AddGrid()
        {
            gridCanvas.Children.Clear();

            // Draw axis
            Path axsisPath = new Path() { Stroke = m_whiteBrush, StrokeThickness = 1.5 };
            PathFigure axisFigure = MakePathFigure(axsisPath);
            axisFigure.StartPoint = new Point(x: 0, y: -10);
            axisFigure.Segments.Add(new LineSegment(new Point(x: 0, y: m_pixelHeight + 10), isStroked: true));
            axisFigure.Segments.Add(new LineSegment(new Point(x: -10, y: m_pixelHeight), isStroked: false));
            axisFigure.Segments.Add(new LineSegment(new Point(x: m_pixelWidth + 10, y: m_pixelHeight), isStroked: true));
            gridCanvas.Children.Add(axsisPath);

            // Draw GridLines
            Path gridPath = new Path() { Stroke = m_whiteBrush, StrokeThickness = 0.5 };
            PathFigure gridFigure = MakePathFigure(gridPath);
            gridFigure.StartPoint = new Point(-10, m_pixelHeight);

            // Add horizontal lines
            double step;
            if (m_horizontalAxisValues.Count > 0)
            {
                step = m_pixelHeight / MaxValue;
                foreach (var i in m_horizontalAxisValues)
                {
                    double y = m_pixelHeight - i * step;
                    gridFigure.Segments.Add(new LineSegment(new Point(x: -10, y), isStroked: false));
                    gridFigure.Segments.Add(new LineSegment(new Point(x: m_pixelWidth + 10, y: y), isStroked: true));
                }
            }
            else
            {
                step = m_pixelHeight / NoOfHorizontalGridLines;
                for (int i = NoOfHorizontalGridLines; i >= 0; i--)
                {
                    gridFigure.Segments.Add(new LineSegment(new Point(x: -10, y: i * step), isStroked: false));
                    gridFigure.Segments.Add(new LineSegment(new Point(x: m_pixelWidth + 10, y: i * step), isStroked: true));
                }
            }

            // Add value-labels if they are fixed
            AddValueLabelsForAreaPlots();

            // Add vertical lines
            step = m_pixelWidth / NoOfVerticalGridLines;
            for (int i = 0; i <= NoOfVerticalGridLines; i++)
            {
                gridFigure.Segments.Add(new LineSegment(new Point(x: i * step, y: -10), isStroked: false));
                gridFigure.Segments.Add(new LineSegment(new Point(x: i * step, y: m_pixelHeight + 10), isStroked: true));
            }
            gridCanvas.Children.Add(gridPath);

            // Add time-labels
            timeLabels.Children.Clear();
            int noOfSecondsToShow = (int)NoOfSecondsToShow;
            int timeStep = (int)NoOfSecondsToShow / (int)(NoOfVerticalGridLines);
            if (NoOfSecondsToShow > 3600)  // More than an hour?
            {
                timeStep = timeStep / 3600; 
                TimeUnitTextBlock.Text = "Hour";
            }
            else if (NoOfSecondsToShow > 60) // More than a minute?
            {
                timeStep = timeStep / 60;
                TimeUnitTextBlock.Text = "Min.";
            }
            else
                TimeUnitTextBlock.Text = "Sec.";

            for (int i = NoOfVerticalGridLines; i >= 0; i--)
            {
                timeLabels.Children.Add(new TextBlock
                {
                    Text = (timeStep * -i).ToString(),
                    FontFamily = new FontFamily("Courier"),
                    FontSize = 14,
                    Foreground = m_whiteBrush,
                    Width = 30,
                    Margin = new Thickness(0, 0, m_pixelWidth / NoOfVerticalGridLines - 30, 0)
                });
            }

            // Add limit sections
            if (PlotCurveWithArea)
            {
                Path limitPath = new Path() { Stroke = m_whiteBrush, StrokeThickness = 1.0, Fill = m_upperLowerLimitBrush, Opacity = 0.5 };
                PathFigure limitFigure = MakePathFigure(limitPath);
                limitFigure.IsClosed = true;

                double valueToPixel = m_pixelHeight / MaxValue;
                double yLower = m_pixelHeight - LowerLimit * valueToPixel;
                double yUpper = m_pixelHeight - UpperLimit * valueToPixel;
                limitFigure.StartPoint = new Point(0, yLower);
                limitFigure.Segments.Add(new LineSegment(new Point(x: 0, yUpper), isStroked: false));
                limitFigure.Segments.Add(new LineSegment(new Point(x: m_pixelWidth, y: yUpper), isStroked: true));
                limitFigure.Segments.Add(new LineSegment(new Point(x: m_pixelWidth, y: yLower), isStroked: false));
                limitFigure.Segments.Add(new LineSegment(new Point(x: 0, y: yLower), isStroked: true));

                gridCanvas.Children.Add(limitPath);
            }
        }

        private void AddValueLabelsForAreaPlots()
        {
            if (!HasFixedVerticalLabels)
                return;

            valueLabels.Children.Clear();

            if (m_horizontalAxisValues.Count == 0)
            {
                double valueStep = MaxValue / NoOfHorizontalGridLines;
                for (int i = 0; i < NoOfHorizontalGridLines; i++)
                {
                    valueLabels.Children.Add(new TextBlock
                    {
                        Text = (valueStep * (NoOfHorizontalGridLines - i)).ToString(),
                        FontFamily = new FontFamily("Courier"),
                        FontSize = 10,
                        Foreground = m_whiteBrush,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 0, 0, (m_pixelHeight / NoOfHorizontalGridLines) - 12)
                    });
                }
            }
            else // Use predefined labels
            {
                double step = m_pixelHeight / MaxValue;
                for (int i = MaxValue; i >= 0; i--)
                {
                    string text;
                    if (m_horizontalAxisValues.Contains(i))
                        text = (i).ToString();
                    else
                        text = " ";

                    valueLabels.Children.Add(new TextBlock
                    {
                        Text = text,
                        Height = 0.7 * m_pixelHeight / m_horizontalAxisValues.Count,
                        FontFamily = new FontFamily("Courier"),
                        FontSize = 10,
                        Foreground = m_whiteBrush,
                        TextAlignment = TextAlignment.Right,
                    });
                }
            }

        }

        private static PathFigure MakePathFigure(Path path)
        {
            PathGeometry pathGeometry = new PathGeometry();
            path.Data = pathGeometry;
            PathFigure pathFigure = new PathFigure();
            pathGeometry.Figures.Add(pathFigure);
            return pathFigure;
        }

        private void PlotWithArea()
        {
            try
            {
                if (DataPoints.Count > 2)
                {
                    int noOfSamlesToColapse = 1;
                    if (m_valueToPixel < 0)
                        noOfSamlesToColapse = (int)(1.0 / m_valueToPixel);

                    List<Point> valuePoints = MakeValuePoints(noOfSamlesToColapse);
                    if (valuePoints.Count == 0)
                        return;

                    // Connect points with straight lines
                    PathFigure curveFigure = MakePathFigure(m_path);
                    curveFigure.StartPoint = new Point(x: valuePoints.First().X, y: valuePoints.First().Y);
                    m_pathSegments = curveFigure.Segments;
                    foreach (var pixelPoint in valuePoints)
                    {
                        m_pathSegments.Add(new LineSegment(new Point(pixelPoint.X, pixelPoint.Y), true));
                    }

                    m_path.Stroke = m_whiteBrush;
                    m_path.Fill = FillAreaBrush;
                    m_path.Opacity = 0.7;
                    m_path.StrokeThickness = 1.0;

                    // Make the area under the curve
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.Last().X, valuePoints.Last().Y), false));
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.Last().X, m_pixelHeight), false));
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.First().X, m_pixelHeight), false));
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.First().X, valuePoints.First().Y), false));
                }

                // Add paths to indicate bad signals
                m_badSignalPath = new Path() { Stroke = m_badSignalBrush, StrokeThickness = 1.0 };
                PathFigure badSignalFigure = MakePathFigure(m_badSignalPath);
                foreach (var item in DataPoints)
                {
                    if (item.IsBadSignal && item.TimeStamp > (m_startTime - TimeSpan.FromSeconds(1)))
                    {
                        TimeSpan timeSpan = item.TimeStamp - m_startTime;
                        double x = timeSpan.TotalSeconds * m_secondToPixel;
                        double y = m_pixelHeight;
                        badSignalFigure.Segments.Add(new LineSegment(new Point(x, 0), false));  // Move
                        badSignalFigure.Segments.Add(new LineSegment(new Point(x, y), true));  // Draw
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"PlotWithArea: {ex.Message}");
            }
        }
        private void Plot()
        {
            try
            {
                if (DataPoints.Count > 2)
                {
                    List<Point> valuePoints = MakeValuePoints();
                    Point[] result_points = m_curveGenerator.MakeCurvePoints(valuePoints, 0.4);
                    m_pathSegments = m_curveGenerator.MakeBezierPath(result_points);
                    m_path.Stroke = m_whiteBrush;
                    m_path.StrokeThickness = 1.0;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Plot: {ex.Message}");
            }
        }

        private List<Point> MakeValuePoints(int noOfSamplesToCollapse = 1)
        {
            // Do datareduction
            List<Measurement> dataPoints;
            if (noOfSamplesToCollapse > 1)
            {
                List<Measurement> reducedDataPoints = new List<Measurement>();
                for (int i = 0; i < DataPoints.Count; i += noOfSamplesToCollapse)
                {
                    double newValue = 0;
                    double maxValue = 0;
                    bool isBadSignal = false;
                    DateTime timeStamp = DataPoints[i].TimeStamp;
                    for (int ii = 0; ii < noOfSamplesToCollapse && ii + i < DataPoints.Count; ii++)
                    {
                        int index = i + ii;
                        if (!isBadSignal)
                            isBadSignal = DataPoints[index].IsBadSignal;

                        if (DataPoints[index].Value > maxValue)
                        {
                            newValue = DataPoints[index].Value;
                            maxValue = newValue;
                        }
                        reducedDataPoints.Add(new Measurement() { Value = newValue, IsBadSignal = isBadSignal, TimeStamp = DataPoints[i].TimeStamp });
                    }
                }
                dataPoints = reducedDataPoints;
            }
            else
            {
                dataPoints = new List<Measurement>();
                for (int i = 0; i < DataPoints.Count; i++)
                {
                    if (DataPoints[i].TimeStamp > DateTime.Now - TimeSpan.FromSeconds(NoOfSecondsToShow))
                        dataPoints.Add(DataPoints[i]);
                }
            }

            if (HasFixedVerticalLabels)
            {
                m_maxValue = MaxValue;
                m_minValue = 0;
            }
            else
            {
                m_maxValue = 2.5;
                dataPoints.ForEach(x => { if (x.Value > m_maxValue) m_maxValue = x.Value; });

                m_minValue = m_maxValue;
                dataPoints.ForEach(x => { if (x.Value < m_minValue) m_minValue = x.Value; });

                // Done to ensure that we at least have a minum scale of 2.5 
                if (m_maxValue < m_minValue + 1.25)
                {
                    if (m_minValue > 1.25)
                    {
                        m_maxValue = m_minValue + 1.25;
                        m_minValue = m_minValue - 1.25;
                    }
                    else
                    {
                        m_maxValue = m_minValue + 2.5;
                        m_minValue = 0;
                    }
                }
            }

            m_valueToPixel = m_pixelHeight / m_maxValue;
            double valueRange = m_maxValue - m_minValue;
            double scale;
            if (valueRange > 0.001)
                scale = m_maxValue / (m_maxValue - m_minValue);
            else
                scale = 100; // Max. scale

            List<Point> valuePoints = new List<Point>();

            foreach (var measurement in dataPoints)
            {
                if (measurement.TimeStamp > (m_startTime - TimeSpan.FromSeconds(1)))
                {
                    TimeSpan timeSpan = measurement.TimeStamp - m_startTime;
                    double x = timeSpan.TotalSeconds * m_secondToPixel;
                    double y;
                    if (HasFixedVerticalLabels)
                        y = m_pixelHeight - measurement.Value * m_valueToPixel;
                    else
                        y = m_pixelHeight - (measurement.Value - m_minValue) * scale * m_valueToPixel;

                    valuePoints.Add(new Point(x, y));
                }
            }

            return valuePoints;
        }
    }
}
