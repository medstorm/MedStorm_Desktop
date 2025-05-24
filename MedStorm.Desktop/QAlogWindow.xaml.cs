using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace MedStorm.Desktop
{
    public class LogEntry
    {
        public string Parameter { get; set; } = "";
        public string Value { get; set; } = "";
    }

    class QALogConfig
    {
        public ObservableCollection<string> Interventions { get; set; } = new();
        public ObservableCollection<string> AvailableParameters { get; set; } = new();
    }

    public partial class QALogWindow : Window
    {
        const string ConfigFileName = "qa-config.json";
        readonly string _configPath;
        QALogConfig _cfg;

        public ObservableCollection<string> Interventions => _cfg.Interventions;
        public ObservableCollection<string> AvailableParameters => _cfg.AvailableParameters;

        public string SelectedIntervention { get; set; }

        public ObservableCollection<LogEntry> InputEntries { get; } = new();
        public ObservableCollection<LogEntry> OutcomeEntries { get; } = new();

        public QALogWindow()
        {
            InitializeComponent();

            // build path in %AppData%\MedStorm\qa-config.json
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MedStorm"
            );
            Directory.CreateDirectory(dir);
            _configPath = Path.Combine(dir, ConfigFileName);

            // load or start fresh
            if (File.Exists(_configPath))
            {
                var txt = File.ReadAllText(_configPath);
                _cfg = JsonConvert.DeserializeObject<QALogConfig>(txt) ?? new QALogConfig();
            }
            else
            {
                _cfg = new QALogConfig(); // no presets
            }

            DataContext = this;
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            // Update config lists
            if (!string.IsNullOrWhiteSpace(SelectedIntervention)
                && !_cfg.Interventions.Contains(SelectedIntervention))
                _cfg.Interventions.Add(SelectedIntervention);

            foreach (var r in InputEntries)
                if (!string.IsNullOrWhiteSpace(r.Parameter)
                    && !_cfg.AvailableParameters.Contains(r.Parameter))
                    _cfg.AvailableParameters.Add(r.Parameter);

            foreach (var r in OutcomeEntries)
                if (!string.IsNullOrWhiteSpace(r.Parameter)
                    && !_cfg.AvailableParameters.Contains(r.Parameter))
                    _cfg.AvailableParameters.Add(r.Parameter);

            File.WriteAllText(_configPath,
                JsonConvert.SerializeObject(_cfg, Formatting.Indented)
            );

            // NEW: Save CSV to PSS Application folder (MyDocuments)
            try
            {
                string baseFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "PSS Application"
                );
                Directory.CreateDirectory(baseFolder);

                // You can separate files for Input/Outcome, or put all in one file
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                string interventionName = (SelectedIntervention ?? "Unknown").Replace(" ", "_");
                string fileBase = $"{interventionName}_{timestamp}";

                // Save InputEntries
                if (InputEntries.Count > 0)
                    CsvExporter.ExportToCsv(InputEntries, Path.Combine(baseFolder, $"{fileBase}_Inputs.csv"));

                // Save OutcomeEntries
                if (OutcomeEntries.Count > 0)
                    CsvExporter.ExportToCsv(OutcomeEntries, Path.Combine(baseFolder, $"{fileBase}_Outcomes.csv"));

                // Optionally: also save a summary log as a CSV
                var summary = new[]
                {
                    new
                    {
                        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Intervention = SelectedIntervention ?? "",
                        InputParameters = string.Join("; ", InputEntries.Where(e => !string.IsNullOrWhiteSpace(e.Parameter)).Select(e => $"{e.Parameter}: {e.Value}")),
                        OutcomeParameters = string.Join("; ", OutcomeEntries.Where(e => !string.IsNullOrWhiteSpace(e.Parameter)).Select(e => $"{e.Parameter}: {e.Value}")),
                        Comments = CommentsBox.Text?.Trim() ?? ""
                    }
                };
                CsvExporter.ExportToCsv(summary, Path.Combine(baseFolder, $"{fileBase}_Summary.csv"));

                MessageBox.Show(
                    $"All data exported to: \n{baseFolder}",
                    "QA Log Export", MessageBoxButton.OK, MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error saving CSV files: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// Call after ShowDialog()==true to gather user data.
        /// </summary>
        public (
            string intervention,
            ObservableCollection<LogEntry> inputs,
            ObservableCollection<LogEntry> outcomes,
            string comments
        ) GetResult()
        {
            return (
                SelectedIntervention ?? "",
                InputEntries,
                OutcomeEntries,
                CommentsBox.Text.Trim()
            );
        }
    }
}
