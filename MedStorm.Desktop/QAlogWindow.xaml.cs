using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
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
                _cfg = JsonConvert
                       .DeserializeObject<QALogConfig>(txt)
                       ?? new QALogConfig();
            }
            else
            {
                _cfg = new QALogConfig(); // no presets
            }

            DataContext = this;
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            // 1) add any new intervention
            if (!string.IsNullOrWhiteSpace(SelectedIntervention)
                && !_cfg.Interventions.Contains(SelectedIntervention))
            {
                _cfg.Interventions.Add(SelectedIntervention);
            }

            // 2) add any new parameters from both grids
            foreach (var r in InputEntries)
                if (!string.IsNullOrWhiteSpace(r.Parameter)
                    && !_cfg.AvailableParameters.Contains(r.Parameter))
                    _cfg.AvailableParameters.Add(r.Parameter);

            foreach (var r in OutcomeEntries)
                if (!string.IsNullOrWhiteSpace(r.Parameter)
                    && !_cfg.AvailableParameters.Contains(r.Parameter))
                    _cfg.AvailableParameters.Add(r.Parameter);

            // 3) persist
            File.WriteAllText(_configPath,
                JsonConvert.SerializeObject(_cfg, Formatting.Indented)
            );

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
