using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MedStorm.Desktop
{
    public class ParamValue : INotifyPropertyChanged
    {
        public string Name { get; set; }
        string _value;
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }
        public ParamValue(string n) { Name = n; Value = ""; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public partial class QALogWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<string> Interventions { get; }
        public string SelectedIntervention { get; set; }
        public ObservableCollection<ParamValue> InputParameterValues { get; }
        public ObservableCollection<ParamValue> OutcomeParameterValues { get; }

        public QALogWindow(QALogTemplate template)
        {
            Interventions = new ObservableCollection<string>(template.Interventions);
            SelectedIntervention = Interventions.FirstOrDefault();
            InputParameterValues = new ObservableCollection<ParamValue>(
                template.InputParameters.Select(p => new ParamValue(p))
            );
            OutcomeParameterValues = new ObservableCollection<ParamValue>(
                template.OutcomeParameters.Select(p => new ParamValue(p))
            );
            DataContext = this;
            InitializeComponent();
        }

        public (string intervention, (string name, string value)[] inputs, (string name, string value)[] outcomes) GetResult()
        {
            return (
                SelectedIntervention ?? "",
                InputParameterValues.Select(p => (p.Name, p.Value)).ToArray(),
                OutcomeParameterValues.Select(p => (p.Name, p.Value)).ToArray()
            );
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            // Validate numbers
            bool allValid = InputParameterValues.Concat(OutcomeParameterValues).All(x => string.IsNullOrWhiteSpace(x.Value) || double.TryParse(x.Value, out _));
            if (!allValid)
            {
                MessageBox.Show("All parameter values must be numbers or left blank.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Back button event: closes this window and can be customized to reopen use case selection
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // Signal parent to re-show use case selection
            Close();
        }

        // Allow only digits, dot, comma (for decimals)
        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsNumericInput(e.Text);
        }
        private bool IsNumericInput(string input)
        {
            foreach (char c in input)
                if (!char.IsDigit(c) && c != '.' && c != ',') return false;
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
