using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace MedStorm.Desktop
{
    public partial class QALogWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<string> Interventions { get; }
        public string SelectedIntervention { get; set; }
        public ObservableCollection<ParamValue> InputParameterValues { get; }
        public ObservableCollection<ParamValue> OutcomeParameterValues { get; }
        public string Header { get; }

        // Static config for drugs
        public static readonly Dictionary<string, string[]> DrugOptions = new()
        {
            { "Analgesic drug", new[] { "Morphine", "Sufentanil", "Tramadol", "Acetaminophen", "Ketamine" } },
            { "Hypnotic drug", new[] { "Midazolam", "Propofol", "Alfa2 agonists", "Isoflurane", "Sevoflurane" } }
        };

        public static bool IsDrugParameter(string paramName) => DrugOptions.ContainsKey(paramName);

        public QALogWindow(QALogTemplate template)
        {
            // Add "Select..." to interventions
            Interventions = new ObservableCollection<string>(new[] { "Select..." }.Concat(template.Interventions));
            SelectedIntervention = Interventions.FirstOrDefault();

            InputParameterValues = new ObservableCollection<ParamValue>(
                template.InputParameters.Select(p => new ParamValue(p))
            );
            OutcomeParameterValues = new ObservableCollection<ParamValue>(
                template.OutcomeParameters.Select(p => new ParamValue(p))
            );
            Header = template.Header;
            DataContext = this;
            InitializeComponent();
        }

        public (string intervention, (string name, string value)[] inputs, (string name, string value)[] outcomes) GetResult()
        {
            return (
                SelectedIntervention ?? "",
                InputParameterValues.Select(p => (p.Name, p.IsDrug ? $"{p.SelectedDrug}:{p.Value}" : p.Value)).ToArray(),
                OutcomeParameterValues.Select(p => (p.Name, p.Value)).ToArray()
            );
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            // Check for "Select..." in interventions
            if (SelectedIntervention == "Select..." || string.IsNullOrWhiteSpace(SelectedIntervention))
            {
                MessageBox.Show("Please select an intervention.", "Missing Intervention", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Check for "Select..." in drug dropdowns
            foreach (var p in InputParameterValues.Where(x => x.IsDrug))
            {
                if (string.IsNullOrWhiteSpace(p.SelectedDrug) || p.SelectedDrug == "Select...")
                {
                    MessageBox.Show($"Please select a drug for parameter \"{p.Name}\".", "Missing Drug", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            // Validate numbers for all value fields (including drug dose)
            bool allValid = InputParameterValues.Concat(OutcomeParameterValues).All(x =>
                string.IsNullOrWhiteSpace(x.Value) || IsValidNumber(x.Value));
            if (!allValid)
            {
                MessageBox.Show("All parameter values must be numbers (use . or , for decimals) or left blank.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            this.Tag = "Back"; // Mark for parent to handle
            this.DialogResult = false; // Signal parent to re-show use case selection
            this.Close();
        }

        // Numeric input filtering
        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsAllowedNumberInput(e.Text, ((TextBox)sender).Text);
        }
        private void NumberOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string pasteText = (string)e.DataObject.GetData(typeof(string));
                if (!IsAllowedNumberInput(pasteText, ((TextBox)sender).Text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
        private static bool IsAllowedNumberInput(string newText, string currentText)
        {
            foreach (char c in newText)
            {
                if (!char.IsDigit(c) && c != '.' && c != ',')
                    return false;
            }
            string combined = currentText + newText;
            if (combined.Count(x => x == ',') > 1) return false;
            if (combined.Count(x => x == '.') > 1) return false;
            return true;
        }
        private static bool IsValidNumber(string value)
        {
            // Accept "2.5", "2,5", "2", "3" etc (dot or comma, but not both)
            return Regex.IsMatch(value, @"^\d+([.,]?\d*)?$");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class ParamValue : INotifyPropertyChanged
    {
        public string Name { get; set; }
        string _value;
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        public bool IsDrug { get; }
        public string[] DrugOptions { get; }
        string _selectedDrug;
        public string SelectedDrug
        {
            get => _selectedDrug;
            set { _selectedDrug = value; OnPropertyChanged(nameof(SelectedDrug)); }
        }

        public ParamValue(string n)
        {
            Name = n;
            Value = "";
            IsDrug = QALogWindow.IsDrugParameter(n);
            if (IsDrug)
            {
                DrugOptions = new[] { "Select..." }.Concat(QALogWindow.DrugOptions[n]).ToArray();
                SelectedDrug = DrugOptions.FirstOrDefault();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class ParamValueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NumericParameterTemplate { get; set; }
        public DataTemplate DrugParameterTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var param = item as ParamValue;
            if (param != null && param.IsDrug)
                return DrugParameterTemplate;
            return NumericParameterTemplate;
        }
    }
}
