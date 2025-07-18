using EOTReminder.Models;
using EOTReminder.Utilities;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Timers;
using ExcelDataReader; // Ensure this NuGet package is installed
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Net;
using System.Windows; // For Application.Current.Dispatcher.Invoke and MessageBox

namespace EOTReminder.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        // TimeSlots will always hold all 4 EO times
        public ObservableCollection<TimeSlot> TimeSlots { get; set; } = new ObservableCollection<TimeSlot>();
        // TopSlots will hold the single highlighted EO time
        public ObservableCollection<TimeSlot> TopSlots { get; } = new ObservableCollection<TimeSlot>();
        // BottomSlots will hold the other 3 EO times when one is highlighted
        public ObservableCollection<TimeSlot> BottomSlots { get; } = new ObservableCollection<TimeSlot>();

        private bool _isAlertActive;

        public bool IsAlertActive // Controls visibility of normal 2x2 grid vs. alert layout
        {
            get => _isAlertActive;
            set { _isAlertActive = value; OnPropertyChanged(); }
        }

        private bool _isAlertNotActive;
        public bool IsAlertNotActive // Controls visibility of normal 2x2 grid vs. alert layout
        {
            get => _isAlertNotActive;
            set { _isAlertNotActive = value; OnPropertyChanged(); }
        }

        public string TodayDate => DateTime.Now.ToString("dd/MM/yyyy");
        public string CurrentTime => DateTime.Now.ToString("HH:mm:ss");

        // Private DateTime fields to hold the actual time values for calculations
        private DateTime _internalSunriseTime;
        private DateTime _internalMiddayTime;
        private DateTime _internalSunsetTime;
        private string _hebrewDateString; // Private field for Hebrew date string

        // Public string properties for UI binding
        public string HebrewDate
        {
            get => _hebrewDateString;
            private set { _hebrewDateString = value; OnPropertyChanged(); }
        }
        public string Sunrise
        {
            get => _internalSunriseTime == DateTime.MinValue ? "N/A" : _internalSunriseTime.ToString("HH:mm:ss");
            private set { /* Setter is not used as _internalSunriseTime is set directly */ }
        }
        public string Midday
        {
            get => _internalMiddayTime == DateTime.MinValue ? "N/A" : _internalMiddayTime.ToString("HH:mm:ss");
            private set { /* Setter is not used as _internalMiddayTime is set directly */ }
        }
        public string Sunset
        {
            get => _internalSunsetTime == DateTime.MinValue ? "N/A" : _internalSunsetTime.ToString("HH:mm:ss");
            private set { /* Setter is not used as _internalSunsetTime is set directly */ }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private Timer _timer;
        private string _currentLang = "he"; // Default to Hebrew as per original code

        private readonly Dictionary<string, Dictionary<string, string>> _translations =
            new Dictionary<string, Dictionary<string, string>>()
            {
                ["en"] = new Dictionary<string, string>()
                {
                    ["EOS1"] = "End of Shema 1", // Added numbers for clarity
                    ["EOS2"] = "End of Shema 2",
                    ["EOT1"] = "End of Prayer 1",
                    ["EOT2"] = "End of Prayer 2",
                    ["Passed"] = "Passed"
                },
                ["he"] = new Dictionary<string, string>()
                {
                    ["EOS1"] = "סו\"ז קר\"ש מג\"א",
                    ["EOS2"] = "סו\"ז קר\"ש תניא גר\"א",
                    ["EOT1"] = "סו\"ז תפילה מג\"א",
                    ["EOT2"] = "סו\"ז תפילה תניא גר\"א",
                    ["Passed"] = "עבר זמנו", // Corrected key to "Passed"
                }
            };
        
        public MainViewModel()
        {
            // Required for ExcelDataReader to handle older Excel formats
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            LoadFromExcel();
            InitTimer();
        }

        public void InitializeData()
        {
            
        }

        private void InitTimer()
        {
            _timer = new Timer(1000); // Tick every 1 second
            _timer.Elapsed += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() => // Ensure UI updates happen on the UI thread
                {
                    foreach (var slot in TimeSlots)
                    {
                        slot.Countdown = slot.Time - DateTime.Now; // Update countdown

                        if (!slot.IsPassed && slot.Countdown <= TimeSpan.Zero)
                        {
                            // Time has just passed
                            slot.Highlight = false;
                            slot.IsPassed = true;
                            slot.CountdownText = ""; // Clear countdown
                            slot.ShowSandClock = false;
                            slot.IsIn30MinAlert = false; // Reset alert state
                            // Reset alert flags for this slot
                            slot.AlertFlags["30"] = false;
                            slot.AlertFlags["10"] = false;
                            slot.AlertFlags["3"] = false;

                            IsAlertActive = false;
                        }
                        else if (!slot.IsPassed)
                        {
                            // Time is still upcoming
                            if (slot.Countdown.TotalMinutes <= 30 && !slot.AlertFlags["30"])
                            {
                                IsAlertActive = true;
                                // 30-minute alert trigger
                                slot.IsIn30MinAlert = true; // This will trigger the UI layout change
                                slot.Highlight = true;
                                slot.ShowSandClock = true;
                                slot.AlertFlags["30"] = true;
                                // No MessageBox for 30min visual alert, just the UI change
                            }
                            else if (slot.Countdown.TotalMinutes > 30 && slot.AlertFlags["30"])
                            {
                                IsAlertActive = false;
                                // If it was in 30min alert but now it's outside, reset
                                slot.IsIn30MinAlert = false;
                                slot.Highlight = false;
                                slot.ShowSandClock = false;
                                slot.AlertFlags["30"] = false; // Allow re-trigger if time is reset/reloaded
                            }

                            // Update countdown text for all active slots
                            slot.CountdownText = string.Format("{0:D2}:{1:D2}",
                                (int)Math.Floor(slot.Countdown.TotalMinutes),
                                slot.Countdown.Seconds);

                            // NEW: Lines 142-152 - Use settings for alert thresholds
                            if (Properties.Settings.Default.FirstAlertMinutes > 0 &&
                                slot.Countdown.TotalMinutes <= Properties.Settings.Default.FirstAlertMinutes &&
                                slot.Countdown.TotalMinutes > (Properties.Settings.Default.FirstAlertMinutes - 1) && // Ensure it fires once per minute
                                !slot.AlertFlags["10"])
                            {
                                if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday || Properties.Settings.Default.AlertOnShabbos)
                                    PlayAlert(slot.Id, "10"); // Still pass "10" to choose the WAV file
                                slot.AlertFlags["10"] = true;
                            }

                            if (Properties.Settings.Default.SecondAlertMinutes > 0 &&
                                slot.Countdown.TotalMinutes <= Properties.Settings.Default.SecondAlertMinutes &&
                                slot.Countdown.TotalMinutes > (Properties.Settings.Default.SecondAlertMinutes - 1) && // Ensure it fires once per minute
                                !slot.AlertFlags["3"])
                            {
                                if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday || Properties.Settings.Default.AlertOnShabbos)
                                   PlayAlert(slot.Id, "3"); // Still pass "3" to choose the WAV file
                                
                                slot.AlertFlags["3"] = true;
                            }
                        }
                    }

                    IsAlertNotActive = !IsAlertActive;
                    UpdateSlotCollections(); // Update the TopSlots/BottomSlots based on alert state
                    OnPropertyChanged(nameof(CurrentTime)); // Update current time in footer
                    // HebrewDate update is less frequent, can be done daily or on language switch
                    // OnPropertyChanged(nameof(HebrewDate)); // Uncomment if you want it to refresh every second
                });
            };
            _timer.Start();
        }

        private void LoadFromExcel()
        {
            string path = Properties.Settings.Default.ExcelFilePath;
          
            if (!File.Exists(path))
            {
                Logger.LogWarning($"Excel file '{path}' not found. Loading mock data.");
                LoadMock();
                return;
            }

            try
            {
                // Ensure ExcelDataReader is configured for the correct encoding
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
                {
                    // Auto-detect the file type (Excel 97-2003 vs. XLSX)
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = true // Assuming the first row is a header row
                            }
                        });

                        var table = dataSet.Tables[0]; // Get the first sheet

                        if (table == null)
                        {
                            Logger.LogWarning("No data tables found in the Excel file. Loading mock data.");
                            LoadMock();
                            return;
                        }

                        var today = DateTime.Today;
                        DataRow todayRow = null;

                        // Find the "Date" column index dynamically
                        int dateColumnIndex = -1;
                        for (int i = 0; i < table.Columns.Count; i++)
                        {
                            if (table.Columns[i].ColumnName.Equals("Date", StringComparison.OrdinalIgnoreCase))
                            {
                                dateColumnIndex = i;
                                break;
                            }
                        }

                        if (dateColumnIndex == -1)
                        {
                            Logger.LogWarning("'Date' column not found in Excel. Loading mock data.");
                            LoadMock();
                            return;
                        }

                        // Iterate through rows to find today's date
                        foreach (DataRow row in table.Rows)
                        {
                            if (row[dateColumnIndex] != DBNull.Value && DateTime.TryParse(row[dateColumnIndex].ToString(), out DateTime excelDate))
                            {
                                if (excelDate.Date == today.Date)
                                {
                                    todayRow = row;
                                    break;
                                }
                            }
                        }

                        if (todayRow == null)
                        {
                            Logger.LogWarning($"No entry found for today's date ({today.ToShortDateString()}) in '{path}'. Loading mock data.");
                            LoadMock();
                            return;
                        }

                        // Get column indices for other data
                        int GetColumnIndex(string columnName)
                        {
                            for (int i = 0; i < table.Columns.Count; i++)
                            {
                                if (table.Columns[i].ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                                {
                                    return i;
                                }
                            }
                            return -1; // Column not found
                        }

                        // Parse time from a cell value
                        DateTime ParseTimeFromCell(DataRow row, string columnName)
                        {
                            int colIndex = GetColumnIndex(columnName);
                            if (colIndex != -1 && row[colIndex] != DBNull.Value)
                            {
                                string timeString = row[colIndex].ToString();
                                if (TimeSpan.TryParse(timeString, out TimeSpan timeSpan))
                                {
                                    // Combine today's date with the time from Excel
                                    return today.Add(timeSpan);
                                }
                                else if (DateTime.TryParse(timeString, out DateTime dateTimeFromCell))
                                {
                                    // If the cell already contains a full DateTime, use its TimeOfDay
                                    return today.Add(dateTimeFromCell.TimeOfDay);
                                }
                            }
                            return DateTime.MinValue; // Indicate parsing error or missing data
                        }

                        // Clear existing slots before adding new ones from Excel
                        TimeSlots.Clear();

                        // Add EOS/EOT slots
                        AddSlot("EOS1", ParseTimeFromCell(todayRow, "EOS1"));
                        AddSlot("EOS2", ParseTimeFromCell(todayRow, "EOS2"));
                        AddSlot("EOT1", ParseTimeFromCell(todayRow, "EOT1"));
                        AddSlot("EOT2", ParseTimeFromCell(todayRow, "EOT2"));

                        TimeSlots.OrderByDescending(s => s.Id);

                        // Set special times to internal DateTime fields
                        _internalSunriseTime = ParseTimeFromCell(todayRow, "Sunrise");
                        _internalMiddayTime = ParseTimeFromCell(todayRow, "Midday");
                        _internalSunsetTime = ParseTimeFromCell(todayRow, "Sunset");

                        // Notify UI for header times (public string properties will now reflect these)
                        OnPropertyChanged(nameof(Sunrise));
                        OnPropertyChanged(nameof(Midday));
                        OnPropertyChanged(nameof(Sunset));

                        // Set Hebrew Date (can be read from Excel or calculated)
                        // Example if HebrewDate column exists:
                        // int hebrewDateColIndex = GetColumnIndex("HebrewDate");
                        // if (hebrewDateColIndex != -1 && todayRow[hebrewDateColIndex] != DBNull.Value)
                        // {
                        //     HebrewDate = todayRow[hebrewDateColIndex].ToString();
                        // }
                        // else
                        // {
                        HebrewDate = GetHebrewJewishDateString(today, false); // Calculate if not in Excel
                        // }
                        OnPropertyChanged(nameof(HebrewDate));

                        // Check for any parsing errors using the internal DateTime fields
                        if (TimeSlots.Any(s => s.Time == DateTime.MinValue) ||
                            _internalSunriseTime == DateTime.MinValue || _internalMiddayTime == DateTime.MinValue || _internalSunsetTime == DateTime.MinValue)
                        {
                            Logger.LogWarning("Some times could not be parsed from Excel. Using mock data for missing values.");
                            // Optionally, you could try to fill in only the missing values with mock data here
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"An error occurred while reading the Excel file: {ex.Message}\nLoading mock data instead.");
                LoadMock();
            }

            // Initialize alert triggers after data is set (either from Excel or mock)
            foreach (var slot in TimeSlots)
            {
                slot.AlertFlags = new Dictionary<string, bool>() { ["30"] = false, ["10"] = false, ["3"] = false };
            }
        }

        private void LoadMock()
        {
            TimeSlots.Clear(); // Clear existing slots before adding mock data
            var now = DateTime.Now;
            AddSlot("EOS1", now.AddMinutes(5).AddSeconds(1));
            AddSlot("EOS2", now.AddMinutes(10).AddSeconds(1));
            AddSlot("EOT1", now.AddMinutes(20).AddSeconds(1));
            AddSlot("EOT2", now.AddMinutes(30).AddSeconds(1));

            // Set internal DateTime fields for mock data
            _internalSunriseTime = now.Date.AddHours(6).AddMinutes(0);
            _internalMiddayTime = now.Date.AddHours(12).AddMinutes(0);
            _internalSunsetTime = now.Date.AddHours(19).AddMinutes(30);

            HebrewDate = GetHebrewJewishDateString(now, false);

            // Notify UI for header times
            OnPropertyChanged(nameof(Sunrise));
            OnPropertyChanged(nameof(Midday));
            OnPropertyChanged(nameof(Sunset));
            OnPropertyChanged(nameof(HebrewDate));
        }

        private void AddSlot(string id, DateTime time)
        {
            TimeSlots.Add(new TimeSlot
            {
                Id = id,
                Description = _translations[_currentLang][id],
                PassedText = _translations[_currentLang]["Passed"],
                Time = time,
                IsPassed = false,
                CountdownText = "",
                ShowSandClock = false,
                Highlight = false,
                IsIn30MinAlert = false,
                AlertFlags = new Dictionary<string, bool>() { ["30"] = false, ["10"] = false, ["3"] = false }
            });
        }

        private void PlayAlert(string slotId, string minutesBefore)
        {
            // Option 1: Play from embedded resource (preferred)
            string fileName = String.Empty;
            string extFileName = String.Empty;
            if (slotId == "EOS1" &&
                minutesBefore == Properties.Settings.Default.FirstAlertMinutes.ToString() &&
                !string.IsNullOrEmpty(Properties.Settings.Default.EOS1FirstAlertPath))
                extFileName = Properties.Settings.Default.EOS1FirstAlertPath;
            else if (slotId == "EOS1" &&
                     minutesBefore == Properties.Settings.Default.SecondAlertMinutes.ToString() &&
                     !string.IsNullOrEmpty(Properties.Settings.Default.EOS1SecondAlertPath))
                extFileName = Properties.Settings.Default.EOS1SecondAlertPath;
            else if (slotId == "EOS2" &&
                     minutesBefore == Properties.Settings.Default.SecondAlertMinutes.ToString() &&
                     !string.IsNullOrEmpty(Properties.Settings.Default.EOS2FirstAlertPath))
                extFileName = Properties.Settings.Default.EOS2FirstAlertPath;
            else if (slotId == "EOS2" &&
                     minutesBefore == Properties.Settings.Default.SecondAlertMinutes.ToString() &&
                     !string.IsNullOrEmpty(Properties.Settings.Default.EOS2SecondAlertPath))
                extFileName = Properties.Settings.Default.EOS2SecondAlertPath;
            else
                fileName = $"alert{slotId}_{minutesBefore}.wav";
            try
            {
                SoundPlayer player = null;
                if (!string.IsNullOrEmpty(extFileName))
                {
                    player = new SoundPlayer(extFileName);
                    System.Diagnostics.Debug.WriteLine($"Playing resource from settings");
                }
                else if (!string.IsNullOrEmpty(fileName))
                {
                    // Get the resource name without extension, as it's typically how Resources.resx stores them
                    string resourceKey = Path.GetFileNameWithoutExtension(fileName);
                    Stream stream = Properties.Resources.ResourceManager.GetStream(resourceKey);

                    if (stream != null)
                    {
                        player = new SoundPlayer(stream);
                    }
                    System.Diagnostics.Debug.WriteLine($"Playing resource from Resources.resx: {resourceKey}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Resource not found in Resources.resx. and settings not set for {slotId} alert {minutesBefore}");
                    return;
                }
                player.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing embedded sound: {ex.Message}");
            }
        }

        private void UpdateSlotCollections()
        {
            // Find the first upcoming slot that is in 30-minute alert mode
            var alertSlot = TimeSlots.FirstOrDefault(slot => slot.IsIn30MinAlert && !slot.IsPassed);

            TopSlots.Clear();
            BottomSlots.Clear();

            if (alertSlot != null)
            {
                IsAlertActive = true; // Activate alert UI layout
                TopSlots.Add(alertSlot);
                foreach (var slot in TimeSlots.Where(s => s != alertSlot).OrderByDescending(s => s.Time)) // Order remaining slots
                {
                    BottomSlots.Add(slot);
                }
            }
            else
            {
                IsAlertActive = false; // Deactivate alert UI layout
                // When no alert is active, the main ItemsControl bound to TimeSlots will display all.
                // TopSlots and BottomSlots should remain empty or cleared.
            }

            // Notify UI that these collections have changed
            OnPropertyChanged(nameof(TopSlots));
            OnPropertyChanged(nameof(BottomSlots));
            // IsAlertActive is already notified when set
        }

        internal void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null; // Set to null to prevent re-use of disposed timer
                Logger.LogInfo("Timer stopped and disposed.");
            }
        }

        private string GetHebrewJewishDateString(DateTime anyDate, bool addDayOfWeek)
        {
            StringBuilder stringBuilder = new StringBuilder();
            CultureInfo cultureInfo = CultureInfo.CreateSpecificCulture("he-IL");
            cultureInfo.DateTimeFormat.Calendar = new HebrewCalendar();
            if (addDayOfWeek)
            {
                stringBuilder.Append(anyDate.ToString("dddd", cultureInfo) + " ");
            }
            stringBuilder.Append(anyDate.ToString("dd", cultureInfo) + " ");
            stringBuilder.Append(anyDate.ToString("y", cultureInfo) ?? "");
            return stringBuilder.ToString();
        }

        public void SwitchLanguage(string lang)
        {
            _currentLang = lang;
            foreach (var slot in TimeSlots)
            {
                if (_translations[lang].TryGetValue(slot.Id, out var trans))
                    slot.Description = trans;
            }
            // Update the "Passed" text for already passed items
            foreach (var slot in TimeSlots.Where(s => s.IsPassed))
            {
                // Trigger PropertyChanged for IsPassed to re-evaluate the Visibility of the "Passed" TextBlock
                // A simpler way is to just set the text directly if not using a converter for the text itself.
                // In this XAML, "Passed" text is hardcoded, so we need to ensure the converter for Visibility works.
                // If you want "Passed" to be translated, you'd bind its Text property to a translated string.
                // For now, the XAML uses a StaticResource for "Passed", so we'd need to update that resource.
                // Let's add a StaticResource for the "Passed" text itself in XAML and update it here.
            }
            OnPropertyChanged(nameof(TimeSlots)); // Notify that TimeSlots have changed (descriptions updated)
            // Also update header/footer texts if they are language-dependent
            // For now, Sunrise/Midday/Sunset are Hebrew in XAML, but their values are times.
            // The HebrewDate string is already dynamic.
            // If you want "Select Language:" to be translated, you'd need to bind it.
        }

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}