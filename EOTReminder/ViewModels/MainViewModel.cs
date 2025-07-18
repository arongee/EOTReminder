using EOTReminder.Models;
using EOTReminder.Utilities;
using ExcelDataReader; // Ensure this NuGet package is installed
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;
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
        private DateTime _lastExcelReloadDate = DateTime.MinValue;
        private bool _hasReloadedForCurrentSunriseCycle = false;
        // Stores the sunrise time for which data is currently loaded
        private DateTime _currentSunriseForReloadCheck = DateTime.MinValue;

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
                    ["a2EOS1"] = "End of Shema 1", // Added numbers for clarity
                    ["a1EOS2"] = "End of Shema 2",
                    ["b2EOT1"] = "End of Prayer 1",
                    ["b1EOT2"] = "End of Prayer 2",
                    ["Passed"] = "Passed"
                },
                ["he"] = new Dictionary<string, string>()
                {
                    ["a2EOS1"] = "סו\"ז קר\"ש מג\"א",
                    ["a1EOS2"] = "סו\"ז קר\"ש תניא גר\"א",
                    ["b2EOT1"] = "סו\"ז תפילה מג\"א",
                    ["b1EOT2"] = "סו\"ז תפילה תניא גר\"א",
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

                        int firstAlertMin = Properties.Settings.Default.FirstAlertMinutes;
                        int secondAlertMin = Properties.Settings.Default.SecondAlertMinutes;
                        int visualAlertMin = Properties.Settings.Default.VisualAlertMinutes;

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
                            if (slot.Countdown.TotalMinutes <= visualAlertMin && !slot.AlertFlags["30"])
                            {
                                IsAlertActive = true;
                                // 30-minute alert trigger
                                slot.IsIn30MinAlert = true; // This will trigger the UI layout change
                                slot.Highlight = true;
                                slot.ShowSandClock = true;
                                slot.AlertFlags["30"] = true;
                                // No MessageBox for 30min visual alert, just the UI change
                            }
                            else if (slot.Countdown.TotalMinutes > visualAlertMin && slot.AlertFlags["30"])
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
                            if (firstAlertMin > 0 &&
                                slot.Countdown.TotalMinutes <= firstAlertMin &&
                                slot.Countdown.TotalMinutes > (firstAlertMin - 1) && // Ensure it fires once per minute
                                !slot.AlertFlags["10"])
                            {
                                if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday || Properties.Settings.Default.AlertOnShabbos)
                                    PlayAlert(slot.Id, "10"); // Still pass "10" to choose the WAV file
                                slot.AlertFlags["10"] = true;
                            }

                            if (secondAlertMin > 0 &&
                                slot.Countdown.TotalMinutes <= secondAlertMin &&
                                slot.Countdown.TotalMinutes > (secondAlertMin - 1) && // Ensure it fires once per minute
                                !slot.AlertFlags["3"])
                            {
                                if (DateTime.Today.DayOfWeek != DayOfWeek.Saturday || Properties.Settings.Default.AlertOnShabbos)
                                   PlayAlert(slot.Id, "3"); // Still pass "3" to choose the WAV file
                                
                                slot.AlertFlags["3"] = true;
                            }

                            // Step 1: Ensure _internalSunriseTime is always updated for the current Gregorian day.
                            // This is crucial if the application runs continuously past midnight,
                            // as _internalSunriseTime would otherwise remain from the previous day.
                            if (_internalSunriseTime.Date != DateTime.Today)
                            {
                                // It's a new Gregorian day, or _internalSunriseTime hasn't been updated for today yet.
                                // Reload Excel data to get the correct sunrise time for today.
                                _hasReloadedForCurrentSunriseCycle = false; // Reset the flag for the new day's cycle
                                _currentSunriseForReloadCheck = _internalSunriseTime; // Store this sunrise time as the basis for the current cycle
                                Logger.LogInfo($"New Gregorian day detected. Excel data reloaded to update current day's times. Sunrise: {_internalSunriseTime:HH:mm:ss}");
                            }

                            // Now, _internalSunriseTime is guaranteed to be for DateTime.Today.
                            // Step 2: Calculate the specific reload trigger time for today's sunrise.
                            DateTime reloadTriggerTime = _internalSunriseTime.Subtract(TimeSpan.FromMinutes(72));

                            // Step 3: Check if it's time to perform the scheduled daily reload (72 minutes before sunrise).
                            // This condition ensures:
                            // 1. The current time is past the calculated trigger time.
                            // 2. We haven't already reloaded for *this specific sunrise cycle*.
                            //    (We use _hasReloadedForCurrentSunriseCycle to prevent multiple reloads within the same cycle).
                            if (DateTime.Now >= reloadTriggerTime && !_hasReloadedForCurrentSunriseCycle)
                            {
                                Logger.LogInfo($"Triggering scheduled daily Excel reload. Current Time: {DateTime.Now:HH:mm:ss}, Reload Trigger Time: {reloadTriggerTime:HH:mm:ss}");
                                LoadFromExcel(); // Perform the actual scheduled reload
                                _hasReloadedForCurrentSunriseCycle = true; // Mark that reload has happened for this cycle
                                _currentSunriseForReloadCheck = _internalSunriseTime; // Update the marker to the new sunrise time after reload
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
                        AddSlot("a1EOS2", ParseTimeFromCell(todayRow, "EOS2"));
                        AddSlot("a2EOS1", ParseTimeFromCell(todayRow, "EOS1"));
                        AddSlot("b1EOT2", ParseTimeFromCell(todayRow, "EOT2"));
                        AddSlot("b2EOT1", ParseTimeFromCell(todayRow, "EOT1"));

                        TimeSlots.OrderBy(s => s.Id);
                        //TimeSlots = TimeSlots.Reverse();

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
            AddSlot("a2EOS1", DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture));
            AddSlot("a1EOS2", DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture));
            AddSlot("b2EOT1", DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture));
            AddSlot("b1EOT2", DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture));

            // Set internal DateTime fields for mock data
            _internalSunriseTime = DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture);
            _internalMiddayTime =  DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture);
            _internalSunsetTime = DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture);

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
            if (slotId == "a2EOS1" &&
                minutesBefore == Properties.Settings.Default.FirstAlertMinutes.ToString() &&
                !string.IsNullOrEmpty(Properties.Settings.Default.EOS1FirstAlertPath))
                extFileName = Properties.Settings.Default.EOS1FirstAlertPath;
            else if (slotId == "a2EOS1" &&
                     minutesBefore == Properties.Settings.Default.SecondAlertMinutes.ToString() &&
                     !string.IsNullOrEmpty(Properties.Settings.Default.EOS1SecondAlertPath))
                extFileName = Properties.Settings.Default.EOS1SecondAlertPath;
            else if (slotId == "a1EOS2" &&
                     minutesBefore == Properties.Settings.Default.SecondAlertMinutes.ToString() &&
                     !string.IsNullOrEmpty(Properties.Settings.Default.EOS2FirstAlertPath))
                extFileName = Properties.Settings.Default.EOS2FirstAlertPath;
            else if (slotId == "a1EOS2" &&
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

            ObservableCollection<TimeSlot> temp = new ObservableCollection<TimeSlot>();
            if (alertSlot != null)
            {
                IsAlertActive = true; // Activate alert UI layout
                TopSlots.Add(alertSlot);
                foreach (var slot in TimeSlots.Where(s => s != alertSlot)) // Order remaining slots
                {
                    temp.Add(slot);
                }
                foreach (var slot in temp.OrderByDescending(s => s.Time))
                {
                    BottomSlots.Add(slot);
                }
                //BottomSlots.Concat(temp.OrderByDescending(s => s.Time));
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