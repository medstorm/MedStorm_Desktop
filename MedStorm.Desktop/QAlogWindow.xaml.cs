using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows.Media;
using System;

namespace MedStorm.Desktop
{
    public partial class QALogWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<string> Interventions { get; }
        public string SelectedIntervention { get; set; }
        public ObservableCollection<ParamValue> InputParameterValues { get; }
        public ObservableCollection<ParamValue> OutcomeParameterValues { get; }
        public string Header { get; }

        // Static config for drugs with units
        public static readonly Dictionary<string, string[]> DrugOptions = new()
        {
            { "Analgesic drug", new[] {
                "Morphine (mg)", "Sufentanil (mcg)", "Tramadol (mg)", "Acetaminophen (mg)", "Ketamine (mg)"
            }},
            { "Hypnotic drug", new[] {
                "Midazolam (mg)", "Propofol (mg)", "Alfa2 agonists (mcg)", "Isoflurane (dose)", "Sevoflurane (dose)"
            }}
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

        public class QALogExportRow
        {
            public string DateTime { get; set; }
            public string UseCase { get; set; }
            public string Intervention { get; set; }
            public string Parameter { get; set; }
            public string Value { get; set; }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            // Validation checks as before...
            if (SelectedIntervention == "Select..." || string.IsNullOrWhiteSpace(SelectedIntervention))
            {
                MessageBox.Show("Please select an intervention.", "Missing Intervention", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            foreach (var p in InputParameterValues.Where(x => x.IsDrug))
            {
                if (string.IsNullOrWhiteSpace(p.SelectedDrug) || p.SelectedDrug == "Select...")
                {
                    MessageBox.Show($"Please select a drug for parameter \"{p.Name}\".", "Missing Drug", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            bool allValid = InputParameterValues.Concat(OutcomeParameterValues).All(x =>
                string.IsNullOrWhiteSpace(x.Value) || IsValidNumber(x.Value));
            if (!allValid)
            {
                MessageBox.Show("All parameter values must be numbers (use . or , for decimals) or left blank.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- EXPORT DATA TO CSV ---
            try
            {
                // 1. Prepare export data
                var rows = new List<QALogExportRow>();
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string useCase = Header; // Or save QALogUseCase as string if available

                foreach (var param in InputParameterValues)
                {
                    string paramName = param.Name;
                    string value = param.IsDrug ? $"{param.SelectedDrug}:{param.Value}" : param.Value;
                    rows.Add(new QALogExportRow
                    {
                        DateTime = now,
                        UseCase = useCase,
                        Intervention = SelectedIntervention,
                        Parameter = paramName,
                        Value = value
                    });
                }
                foreach (var param in OutcomeParameterValues)
                {
                    string paramName = param.Name;
                    string value = param.Value;
                    rows.Add(new QALogExportRow
                    {
                        DateTime = now,
                        UseCase = useCase,
                        Intervention = SelectedIntervention,
                        Parameter = paramName,
                        Value = value
                    });
                }

                // 2. Save file (in Documents\MedStormQALog)
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MedStormQALog");
                if (!System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);
                string fileName = $"QALog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = System.IO.Path.Combine(folder, fileName);

                CsvExporter.ExportToCsv(rows, fullPath);

                // 3. Show notification
                MessageBox.Show(
                    $"Data saved to:\n{fullPath}\n\nYou can find all your QA Log data in this folder.",
                    "QA Log Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // (Optionally: also trigger EMR export or flag)
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save data.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // ---

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

        // Enter: jump to next input field!
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var tbox = sender as TextBox;
                if (tbox == null) return;

                // Search all TextBoxes in both input and outcome parameters
                var allTextBoxes = FindVisualChildren<TextBox>(this).Where(tb => tb.IsEnabled && tb.Visibility == Visibility.Visible).ToList();

                int currIdx = allTextBoxes.IndexOf(tbox);
                if (currIdx != -1 && allTextBoxes.Count > 1)
                {
                    int nextIdx = (currIdx + 1) % allTextBoxes.Count;
                    var nextBox = allTextBoxes[nextIdx];
                    nextBox.Focus();
                    nextBox.SelectAll();
                    e.Handled = true;
                }
            }
        }
        // Helper to enumerate all visual children of a given type
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) yield return t;
                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
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
