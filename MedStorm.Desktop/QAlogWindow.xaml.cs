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

    public class QALogData
    {
        public string Intervention { get; set; } = "";
        public ObservableCollection<LogEntry> InputEntries { get; } = new();
        public ObservableCollection<LogEntry> OutcomeEntries { get; } = new();
        public string Comments { get; set; } = "";
    }

    public class QALogConfig
    {
        public ObservableCollection<string> Interventions { get; set; } = new();
        public ObservableCollection<string> AvailableParameters { get; set; } = new();
    }

    public partial class QALogWindow : Window
    {
        const string CONFIG_FILE = "qa-config.json";

        // ← new, to survive beyond constructor
        readonly string _configDir;
        readonly string _configPath;

        readonly QALogConfig _cfg;
        readonly QALogData _result = new();

        public ObservableCollection<string> Interventions => _cfg.Interventions;
        public ObservableCollection<string> AvailableParameters => _cfg.AvailableParameters;

        public string SelectedIntervention
        {
            get => _result.Intervention;
            set => _result.Intervention = value ?? "";
        }

        public ObservableCollection<LogEntry> InputEntries => _result.InputEntries;
        public ObservableCollection<LogEntry> OutcomeEntries => _result.OutcomeEntries;

        public QALogWindow()
        {
            InitializeComponent();

            // set up config paths once
            _configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MedStorm"
            );
            _configPath = Path.Combine(_configDir, CONFIG_FILE);

            // load or start empty
            if (File.Exists(_configPath))
            {
                _cfg = JsonConvert
                    .DeserializeObject<QALogConfig>(File.ReadAllText(_configPath))!;
            }
            else
            {
                _cfg = new QALogConfig();
            }

            DataContext = this;
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            _result.Comments = CommentsBox.Text.Trim();

            // persist any new intervention
            if (!string.IsNullOrWhiteSpace(_result.Intervention)
                && !_cfg.Interventions.Contains(_result.Intervention))
            {
                _cfg.Interventions.Add(_result.Intervention);
            }

            // persist any new parameters
            foreach (var row in InputEntries)
            {
                if (!string.IsNullOrWhiteSpace(row.Parameter)
                    && !_cfg.AvailableParameters.Contains(row.Parameter))
                {
                    _cfg.AvailableParameters.Add(row.Parameter);
                }
            }
            foreach (var row in OutcomeEntries)
            {
                if (!string.IsNullOrWhiteSpace(row.Parameter)
                    && !_cfg.AvailableParameters.Contains(row.Parameter))
                {
                    _cfg.AvailableParameters.Add(row.Parameter);
                }
            }

            // write out config
            Directory.CreateDirectory(_configDir);
            File.WriteAllText(
                _configPath,
                JsonConvert.SerializeObject(_cfg, Formatting.Indented)
            );

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// After ShowDialog()==true call this to get the user’s entries.
        /// </summary>
        public QALogData GetResult() => _result;
    }
}
