using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32; // For OpenFileDialog
using EOTReminder.Utilities;

namespace EOTReminder.ViewModels
{
    public class OptionsViewModel : INotifyPropertyChanged
    {
        // Existing Settings
        private int _firstAlertMinutes;
        public int FirstAlertMinutes
        {
            get => _firstAlertMinutes;
            set
            {
                _firstAlertMinutes = value; OnPropertyChanged();
                Properties.Settings.Default.FirstAlertMinutes = value;
                Properties.Settings.Default.Save();
            }
        }

        private int _secondAlertMinutes;
        public int SecondAlertMinutes
        {
            get => _secondAlertMinutes;
            set
            {
                _secondAlertMinutes = value; OnPropertyChanged();
                Properties.Settings.Default.SecondAlertMinutes = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _excelFilePath;
        public string ExcelFilePath
        {
            get => _excelFilePath;
            set { _excelFilePath = value; OnPropertyChanged(); }
        }

        // NEW: Audio Alert Paths
        private string _eos1FirstAlertPath;
        public string EOS1FirstAlertPath
        {
            get => _eos1FirstAlertPath;
            set
            {
                _eos1FirstAlertPath = value; OnPropertyChanged();
                Properties.Settings.Default.EOS1FirstAlertPath = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _eos1SecondAlertPath;
        public string EOS1SecondAlertPath
        {
            get => _eos1SecondAlertPath;
            set
            {
                _eos1SecondAlertPath = value; OnPropertyChanged();
                Properties.Settings.Default.EOS1SecondAlertPath = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _eos2FirstAlertPath;
        public string EOS2FirstAlertPath
        {
            get => _eos2FirstAlertPath;
            set
            {
                _eos2FirstAlertPath = value; OnPropertyChanged();
                Properties.Settings.Default.EOS2FirstAlertPath = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _eos2SecondAlertPath;
        public string EOS2SecondAlertPath
        {
            get => _eos2SecondAlertPath;
            set
            {
                _eos2SecondAlertPath = value; OnPropertyChanged();
                Properties.Settings.Default.EOS2SecondAlertPath = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _eot1FirstAlertPath;
        public string EOT1FirstAlertPath
        {
            get => _eot1FirstAlertPath;
            set { _eot1FirstAlertPath = value; OnPropertyChanged(); }
        }

        private string _eot1SecondAlertPath;
        public string EOT1SecondAlertPath
        {
            get => _eot1SecondAlertPath;
            set { _eot1SecondAlertPath = value; OnPropertyChanged(); }
        }

        private string _eot2FirstAlertPath;
        public string EOT2FirstAlertPath
        {
            get => _eot2FirstAlertPath;
            set
            {
                _eot2FirstAlertPath = value; OnPropertyChanged();
                Properties.Settings.Default.EOT2FirstAlertPath = value;
                Properties.Settings.Default.Save();
            }
        }

        private string _eot2SecondAlertPath;
        public string EOT2SecondAlertPath
        {
            get => _eot2SecondAlertPath;
            set
            {
                _eot2SecondAlertPath = value; OnPropertyChanged();
                Properties.Settings.Default.EOT2SecondAlertPath = value;
                Properties.Settings.Default.Save();
            }
        }

        // NEW: Visual Alert Minutes
        private int _visualAlertMinutes;
        public int VisualAlertMinutes
        {
            get => _visualAlertMinutes;
            set
            {
                _visualAlertMinutes = value; OnPropertyChanged();
                Properties.Settings.Default.VisualAlertMinutes = value;
                Properties.Settings.Default.Save();
            }
        }

        // NEW: Alert on Shabbos
        private bool _alertOnShabbos;
        public bool AlertOnShabbos
        {
            get => _alertOnShabbos;
            set
            {
                _alertOnShabbos = value; OnPropertyChanged();
                Properties.Settings.Default.AlertOnShabbos = value;
                Properties.Settings.Default.Save();
            }
        }

        // Commands
        public ICommand SaveSettingsCommand { get; }
        public ICommand CloseApplicationCommand { get; }
        public ICommand MinimizeApplicationCommand { get; }
        public ICommand CloseSettingsCommand { get; } // NEW: Command for closing settings window
        public ICommand BrowseExcelCommand { get; }
        // NEW: Browse Commands for Audio Paths
        public ICommand BrowseEOS1FirstAlertCommand { get; }
        public ICommand BrowseEOS1SecondAlertCommand { get; }
        public ICommand BrowseEOS2FirstAlertCommand { get; }
        public ICommand BrowseEOS2SecondAlertCommand { get; }
        public ICommand BrowseEOT1FirstAlertCommand { get; }
        public ICommand BrowseEOT1SecondAlertCommand { get; }
        public ICommand BrowseEOT2FirstAlertCommand { get; }
        public ICommand BrowseEOT2SecondAlertCommand { get; }


        public OptionsViewModel()
        {
            LoadSettings();
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            CloseApplicationCommand = new RelayCommand(CloseApplication);
            MinimizeApplicationCommand = new RelayCommand(MinimizeApplication);
            CloseSettingsCommand = new RelayCommand(CloseSettings); // Initialize new command
            BrowseExcelCommand = new RelayCommand(BrowseExcelFile);
            // NEW: Initialize Browse Commands for Audio Paths
            BrowseEOS1FirstAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOS1FirstAlertPath)));
            BrowseEOS1SecondAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOS1SecondAlertPath)));
            BrowseEOS2FirstAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOS2FirstAlertPath)));
            BrowseEOS2SecondAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOS2SecondAlertPath)));
            BrowseEOT1FirstAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOT1FirstAlertPath)));
            BrowseEOT1SecondAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOT1SecondAlertPath)));
            BrowseEOT2FirstAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOT2FirstAlertPath)));
            BrowseEOT2SecondAlertCommand = new RelayCommand(param => BrowseAudioFile(nameof(EOT2SecondAlertPath)));
        }

        private void LoadSettings()
        {
            FirstAlertMinutes = Properties.Settings.Default.FirstAlertMinutes;
            SecondAlertMinutes = Properties.Settings.Default.SecondAlertMinutes;
            ExcelFilePath = Properties.Settings.Default.ExcelFilePath;

            // NEW: Load new settings
            EOS1FirstAlertPath = Properties.Settings.Default.EOS1FirstAlertPath;
            EOS1SecondAlertPath = Properties.Settings.Default.EOS1SecondAlertPath;
            EOS2FirstAlertPath = Properties.Settings.Default.EOS2FirstAlertPath;
            EOS2SecondAlertPath = Properties.Settings.Default.EOS2SecondAlertPath;
            VisualAlertMinutes = Properties.Settings.Default.VisualAlertMinutes;
            AlertOnShabbos = Properties.Settings.Default.AlertOnShabbos;

            Logger.LogInfo("Application settings loaded.");
        }

        private void SaveSettings(object parameter)
        {
            Properties.Settings.Default.FirstAlertMinutes = FirstAlertMinutes;
            Properties.Settings.Default.SecondAlertMinutes = SecondAlertMinutes;
            Properties.Settings.Default.ExcelFilePath = ExcelFilePath;

            // NEW: Save new settings
            Properties.Settings.Default.EOS1FirstAlertPath = EOS1FirstAlertPath;
            Properties.Settings.Default.EOS1SecondAlertPath = EOS1SecondAlertPath;
            Properties.Settings.Default.EOS2FirstAlertPath = EOS2FirstAlertPath;
            Properties.Settings.Default.EOS2SecondAlertPath = EOS2SecondAlertPath;
            // Properties.Settings.Default.EOT1FirstAlertPath = EOT1FirstAlertPath;
            // Properties.Settings.Default.EOT1SecondAlertPath = EOT1SecondAlertPath;
            // Properties.Settings.Default.EOT2FirstAlertPath = EOT2FirstAlertPath;
            // Properties.Settings.Default.EOT2SecondAlertPath = EOT2SecondAlertPath;
            Properties.Settings.Default.VisualAlertMinutes = VisualAlertMinutes;
            Properties.Settings.Default.AlertOnShabbos = AlertOnShabbos;

            Properties.Settings.Default.Save();
            Logger.LogInfo("Application settings saved successfully.");

            if (parameter is Window window)
            {
                window.Close();
            }
            // Removed direct window close, now handled by specific CloseSettings command
        }

        // NEW: CloseSettings method
        private void CloseSettings(object parameter)
        {
            Logger.LogInfo("Settings window close requested.");
            if (parameter is Window window)
            {
                window.Close();
            }
        }

        private void CloseApplication(object parameter)
        {
            Logger.LogInfo("Application close requested from options window.");
            if (parameter is Window window)
            {
                window.Close();
            }
            Application.Current.Shutdown();
        }

        private void MinimizeApplication(object parameter)
        {
            Logger.LogInfo("Application minimize requested from options window.");
            if (parameter is Window window)
            {
                window.Close();
            }
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        private void BrowseExcelFile(object parameter)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*";
            openFileDialog.InitialDirectory = GetInitialDirectory(ExcelFilePath);

            try
            {
                if (openFileDialog.ShowDialog() == true)
                {
                    ExcelFilePath = openFileDialog.FileName;
                    Logger.LogInfo($"Excel file path set to: {ExcelFilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error opening file dialog for Excel file: {ex.Message}", ex);
            }
        }

        // NEW: Generic BrowseAudioFile method
        private void BrowseAudioFile(string propertyName)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "WAV Audio Files (*.wav)|*.wav|All Files (*.*)|*.*";

            string currentPath = GetPropertyValue(propertyName) as string;
            openFileDialog.InitialDirectory = GetInitialDirectory(currentPath);

            try
            {
                if (openFileDialog.ShowDialog() == true)
                {
                    SetPropertyValue(propertyName, openFileDialog.FileName);
                    Logger.LogInfo($"Audio file path for {propertyName} set to: {openFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error opening file dialog for audio file ({propertyName}): {ex.Message}", ex);
            }
        }

        // Helper to get initial directory for file dialogs
        private string GetInitialDirectory(string currentPath)
        {
            if (!string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
            {
                return Path.GetDirectoryName(currentPath);
            }
            if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
            {
                return currentPath;
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        // Helper to get property value by name (for dynamic binding)
        private object GetPropertyValue(string propertyName)
        {
            return GetType().GetProperty(propertyName)?.GetValue(this);
        }

        // Helper to set property value by name (for dynamic binding)
        private void SetPropertyValue(string propertyName, object value)
        {
            GetType().GetProperty(propertyName)?.SetValue(this, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Basic RelayCommand implementation (if you don't have one already)
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}