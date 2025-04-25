using System.Collections.Generic;
using System.Windows;

namespace MedStorm.Desktop
{
    public partial class QALogWindow : Window
    {
        private Dictionary<string, string> qaLogEntries = new Dictionary<string, string>();

        public QALogWindow()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            qaLogEntries.Clear();
            qaLogEntries[ParameterTextBox.Text] = ValueTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public Dictionary<string, string> GetLogEntries() => qaLogEntries;
    }
}
