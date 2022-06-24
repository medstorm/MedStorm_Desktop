using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MedStorm.Desktop
{
    public class TimeCheckTextBlock : TextBox
    {
        DateTime m_lastTimeStamp = DateTime.MinValue;

        public string ValueText
        {
            get { return (string)GetValue(ValueTextProperty); }
            set
            {
                SetValue(ValueTextProperty, value);
                NewValuTextChanged(value.ToString());
            }
        }
        public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register("ValueText", typeof(string), typeof(TimeCheckTextBlock), new PropertyMetadata("--"));

        public void NewValuTextChanged(string newValue)
        {
            m_lastTimeStamp = DateTime.UtcNow;
            Text = newValue;
        }

        public TimeCheckTextBlock()
        {
            this.Loaded += TimeCheckTextBlock_Loaded;
            IsReadOnly = true;
            Background = Brushes.Transparent;
            BorderBrush = Brushes.Transparent;
            Text = "--";
        }

        private void TimeCheckTextBlock_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            DispatcherTimer checkForValidDataTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(2) };
            checkForValidDataTimer.Tick += CheckForValidDataTimer_Tick;
            checkForValidDataTimer.Start();
        }

        private void CheckForValidDataTimer_Tick(object? sender, EventArgs e)
        {
            if (m_lastTimeStamp < DateTime.UtcNow - TimeSpan.FromSeconds(2.0))
                Text = "--";
        }
    }
}
