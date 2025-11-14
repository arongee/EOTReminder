
// Views/MainWindow.xaml.cs

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EOTReminder.Utilities;
using EOTReminder.ViewModels;
using WorkspaceTask;

namespace EOTReminder.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel => DataContext as MainViewModel;

        public MainWindow()
        {
            Logger.LogInfo("");
            Logger.LogInfo("-----------------------------");
            Logger.LogInfo("MainWindow initialized.\n");
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
           // _viewModel.InitializeData();
            InitializeComponent();
        }
        
        // Optional: Language switcher handler if you add ComboBox in XAML later
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems[0] is ComboBoxItem selected)
            {
                string lang = selected.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(lang))
                    _viewModel?.SwitchLanguage(lang);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Ensure the timer is stopped when the window is closing
            _viewModel?.StopTimer();
        }

        private void HiddenOptionsButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // This will open the options page
            OpenOptionsPage();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Now that the main window is loaded, it's safe to check IsFirstRun
            // and open the OptionsWindow if necessary.
            // if (Properties.Settings.Default.IsFirstRun)
            // {
            //     OpenOptionsPage();
            //     Properties.Settings.Default.IsFirstRun = false;
            //     Properties.Settings.Default.Save();
            // }

            // Initialize ViewModel data after settings are potentially loaded/updated
            // This ensures the Excel path from settings is available.
           
        }
        private void OpenOptionsPage()
        {
            OptionsWindow optionsWindow = new OptionsWindow();
            optionsWindow.Owner = this; // Set the main window as the owner
            optionsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            optionsWindow.ShowDialog(); // Show as dialog to block main window until closed
        }
    }
}