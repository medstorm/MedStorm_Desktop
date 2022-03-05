using BezierCurve;
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
using System.Windows.Threading;

namespace Plot
{
    /// <summary>
    /// Interaction logic for RealTimePlotUC.xaml
    /// </summary>
    public partial class RealTimePlotUC : UserControl
    {
        public string PlotTitle { get; set; } = string.Empty;
        public bool HasFixedVerticalLabels { get; set; } = true;
        public double NoOfSecondsToShow { get; set; } = 15;
        public bool PlotCurveWithArea { get; set; }
        public int NoOfHorizontalGridLines { get; set; } = 5;
        public double MaxValue { get; set; } = 10;
        public int NoOfVerticalGridLines { get; set; } = 5;
        public List<Measurement> DataPoints { get; private set; }

        public Brush FillAreaBrush { get; set; }

        double m_maxValue = 1;
        double m_minValue = 0;
        double m_valueToPixel = 1;
        double m_pixelHeight = 0;
        double m_pixelWidth = 0;
        double m_secondToPixel = 0;
        PathSegmentCollection m_pathSegments = new PathSegmentCollection();
        Path m_path = new Path();
        CurveGenerator m_curveGenerator;
        public RealTimePlotUC()
        {
            InitializeComponent();
            FillAreaBrush = Brushes.Green;
            m_curveGenerator = new CurveGenerator(m_path);
            DataPoints = new List<Measurement>();
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
            DispatcherTimer animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(15);
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }

        public void AddData(Measurement sample)
        {
            DataPoints.Add(sample);

            // Remove old data
            DateTime startTime = DateTime.Now - TimeSpan.FromSeconds(NoOfSecondsToShow);
            for (int i = DataPoints.Count - 1; i >= 0; i--)
            {
                if (DataPoints[i].TimeStamp < (startTime - TimeSpan.FromSeconds(1)))
                    DataPoints.RemoveAt(i);
            }
        }

        private void PlotUserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Init();
            AddGrid();
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            m_path = new Path();
            m_curveGenerator = new CurveGenerator(m_path);

            if (PlotCurveWithArea)
                PlotWithArea();
            else
                Plot();


            plotCanvas.Children.Clear();
            plotCanvas.Children.Add(m_path);

            // Add value-labels if they are not fixed
            if (!HasFixedVerticalLabels)
            {
                valueLabels.Children.Clear();
                string valueLabelText="";
                double valueRange = m_maxValue - m_minValue;
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
                        Foreground = Brushes.White,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 0, 0, (m_pixelHeight / NoOfHorizontalGridLines) - 12)
                    });
                }
            }
        }

        private void AddGrid()
        {
            gridCanvas.Children.Clear();

            // Draw axis
            Path axsisPath = new Path() { Stroke = Brushes.White, StrokeThickness = 2.5 };
            PathFigure axisFigure = MakePathFigure(axsisPath);
            axisFigure.StartPoint = new Point(x: 0, y: -10);
            axisFigure.Segments.Add(new LineSegment(new Point(x: 0, y: m_pixelHeight + 10), isStroked: true));
            axisFigure.Segments.Add(new LineSegment(new Point(x: -10, y: m_pixelHeight), isStroked: false));
            axisFigure.Segments.Add(new LineSegment(new Point(x: m_pixelWidth + 10, y: m_pixelHeight), isStroked: true));
            gridCanvas.Children.Add(axsisPath);

            // Draw GridLines
            Path gridPath = new Path() { Stroke = Brushes.White, StrokeThickness = 0.5 };
            PathFigure gridFigure = MakePathFigure(gridPath);
            gridFigure.StartPoint = new Point(-10, m_pixelHeight);

            // Add horizontal lines
            double step = m_pixelHeight / NoOfHorizontalGridLines;
            for (int i = NoOfHorizontalGridLines; i >= 0; i--)
            {
                gridFigure.Segments.Add(new LineSegment(new Point(x: -10, y: i * step), isStroked: false));
                gridFigure.Segments.Add(new LineSegment(new Point(x: m_pixelWidth + 10, y: i * step), isStroked: true));
            }

            // Add value-labels if they ar fixed
            if (HasFixedVerticalLabels)
            {
                valueLabels.Children.Clear();
                double valueStep = MaxValue / NoOfHorizontalGridLines;
                for (int i = 0; i < NoOfHorizontalGridLines; i++)
                {
                    //Control.HorizontalContentAlignment=
                    valueLabels.Children.Add(new TextBlock
                    {
                        Text = (valueStep * (NoOfHorizontalGridLines - i)).ToString(),
                        FontFamily = new FontFamily("Courier"),
                        FontSize = 10,
                        Foreground = Brushes.White,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 0, 0, (m_pixelHeight / NoOfHorizontalGridLines)-12)
                    });
                }
            }

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
            int secondStep = (int)NoOfSecondsToShow / (int)(NoOfVerticalGridLines + 1);
            for (int i = NoOfVerticalGridLines; i >= 0; i--)
            {
                timeLabels.Children.Add(new TextBlock
                {
                    Text = (secondStep * i).ToString(),
                    FontFamily = new FontFamily("Courier"),
                    FontSize = 10,
                    Foreground = Brushes.White,
                    Width = 30,
                    Margin = new Thickness(0, 0, m_pixelWidth / NoOfVerticalGridLines - 30, 0)
                });
            }
        }

        private static PathFigure MakePathFigure(Path gridPath)
        {
            PathGeometry pathGeometry = new PathGeometry();
            gridPath.Data = pathGeometry;
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
                    List<Point> valuePoints = MakeValuePoints();
                    Point[] result_points = m_curveGenerator.MakeCurvePoints(valuePoints, 0.4);
                    m_pathSegments = m_curveGenerator.MakeBezierPath(result_points, isClosed: true);
                    m_path.Stroke = Brushes.White;
                    m_path.Fill = FillAreaBrush;
                    m_path.Opacity = 0.7;
                    m_path.StrokeThickness = 1.0;

                    // Make the area under the curve
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.Last().X, valuePoints.Last().Y), false));
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.Last().X, m_pixelHeight), false));
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.First().X, m_pixelHeight), false));
                    m_pathSegments.Add(new LineSegment(new Point(valuePoints.First().X, valuePoints.First().Y), false));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
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
                    m_path.Stroke = Brushes.White;
                    m_path.StrokeThickness = 1.0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private List<Point> MakeValuePoints()
        {
            if (HasFixedVerticalLabels)
            {
                m_maxValue = MaxValue;
                m_minValue = 0;
            }
            else
            {
                m_maxValue = 1;
                DataPoints.ForEach(x => { if (x.Value > m_maxValue) m_maxValue = x.Value; });

                m_minValue = m_maxValue;
                DataPoints.ForEach(x => { if (x.Value < m_minValue) m_minValue = x.Value; });
            }
            m_valueToPixel = m_pixelHeight / m_maxValue;

            DateTime startTime = DateTime.Now - TimeSpan.FromSeconds(NoOfSecondsToShow);
            List<Point> valuePoints = new List<Point>();
            foreach (var item in DataPoints)
            {
                TimeSpan timeSpan = item.TimeStamp - startTime;
                double x = timeSpan.TotalSeconds * m_secondToPixel;
                double y = m_pixelHeight - item.Value * m_valueToPixel;
                valuePoints.Add(new Point(x, y));
            }

            return valuePoints;
        }
    }
}
