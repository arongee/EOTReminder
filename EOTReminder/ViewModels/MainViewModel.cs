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
using System.Windows;
using System.Windows.Media; // For Application.Current.Dispatcher.Invoke and MessageBox

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
        private bool _isAlertNotActive;
        private bool _hasReloadedForCurrentSunriseCycle;
        private Timer _timer;
        private SlideshowService _slideshowService;
        private ImageSource _slideshowImage;
        private bool _isSlideshowActive;
        private string _slideshowHeaderText;
        private TransitionType _slideshowTransition;

        // Private DateTime fields to hold the actual time values for calculations
        private DateTime _internalSunriseTime;
        private DateTime _internalMiddayTime;
        private DateTime _internalSunsetTime;
        private DateTime _reloadTriggerTime;
        
        private string _todaysDate;
        private string _hebrewDateString; // Private field for Hebrew date string
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

        // Controls visibility of normal 2x2 grid vs. alert layout
        public bool IsAlertActive
        {
            get => _isAlertActive;
            set { _isAlertActive = value; OnPropertyChanged(); }
        }
        // Controls visibility of normal 2x2 grid vs. alert layout
        public bool IsAlertNotActive
        {
            get => _isAlertNotActive;
            set { _isAlertNotActive = value; OnPropertyChanged(); }
        }
        public string TodayDate
        {
            get { return _todaysDate; } 
            set { _todaysDate = value; OnPropertyChanged(); }
        } 
        public string CurrentTime => DateTime.Now.ToString("HH:mm:ss");
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


        private int visualAlertMin ;
        private int firstAlertMin  ;
        private int secondAlertMin ;
        private int firstTAlertMin ;
        private int secondTAlertMin;
        private bool SunSet10Alert;
        private bool SunSet3Alert;
        private bool SunSetAlert;

        private bool TenMinSunSet;
        private bool ThreeMinSunSet;
        private bool SunSet;


        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            IsAlertActive = false;
            IsAlertNotActive = true;
            _hasReloadedForCurrentSunriseCycle = false;

            // Required for ExcelDataReader to handle older Excel formats
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            LoadFromExcel();
            InitTimer();

            _reloadTriggerTime = DateTime.Today.Add(new TimeSpan(0, 05, 0));
        }

        private void InitTimer()
        {
            try
            {
                SetSettingsProperties();
                _timer = new Timer(1000); // Tick every 1 second
                _timer.Elapsed += (s, e) =>
                {
                    try
                    {
                        bool hasChanged = false;

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
                                    slot.AlertFlags[Globals.Visual] = false;
                                    slot.AlertFlags[Globals.Shema1] = false;
                                    slot.AlertFlags[Globals.Shema2] = false;
                                    slot.AlertFlags[Globals.Tefila2] = false;

                                    IsAlertActive = false;
                                    hasChanged = true;
                                }
                                else if (!slot.IsPassed)
                                {

                                    // Update countdown text for all active slots
                                    slot.CountdownText = string.Format("{0:D2}:{1:D2}",
                                        (int)Math.Floor(slot.Countdown.TotalMinutes),
                                        slot.Countdown.Seconds);

                                    // Time is still upcoming
                                    if (slot.Countdown.TotalMinutes <= visualAlertMin && !slot.AlertFlags[Globals.Visual])
                                    {
                                        IsAlertActive = true;
                                        // 30-minute alert trigger
                                        slot.IsIn30MinAlert = true; // This will trigger the UI layout change
                                        slot.Highlight = true;
                                        slot.ShowSandClock = true;
                                        slot.AlertFlags[Globals.Visual] = true;
                                        // No MessageBox for 30min visual alert, just the UI change
                                        hasChanged = true;
                                    }
                                    else if (slot.Countdown.TotalMinutes > visualAlertMin && slot.AlertFlags[Globals.Visual])
                                    {
                                        IsAlertActive = false;
                                        // If it was in 30min alert but now it's outside, reset
                                        slot.IsIn30MinAlert = false;
                                        slot.Highlight = false;
                                        slot.ShowSandClock = false;
                                        slot.AlertFlags[Globals.Visual] = false; // Allow re-trigger if time is reset/reloaded
                                        hasChanged = true;
                                    }
                                    else if (slot.Countdown.TotalMinutes >= visualAlertMin &&
                                            slot.Countdown.TotalMinutes <= visualAlertMin + 1)
                                        hasChanged = true;

                                    if (!HebrewDateHelper.IsYomTov(DateTime.Today) || Properties.Settings.Default.AlertOnShabbos)
                                    {
                                        // Use settings for alert thresholds
                                        if (firstAlertMin > 0 &&
                                            slot.Countdown.TotalMinutes <= firstAlertMin &&
                                            slot.Countdown.TotalMinutes > (firstAlertMin - 1) && // Ensure it fires once per minute
                                            !slot.AlertFlags[Globals.Shema1])
                                        {

                                            PlayAlert(slot.Id, Globals.Shema1); // Still pass "10" to choose the WAV file
                                            slot.AlertFlags[Globals.Shema1] = true;
                                            hasChanged = true;
                                        }

                                        if (secondAlertMin > 0 &&
                                            slot.Countdown.TotalMinutes <= secondAlertMin &&
                                            slot.Countdown.TotalMinutes > (secondAlertMin - 1) && // Ensure it fires once per minute
                                            !slot.AlertFlags[Globals.Shema2])
                                        {

                                            PlayAlert(slot.Id, Globals.Shema2); // Still pass "3" to choose the WAV file
                                            slot.AlertFlags[Globals.Shema2] = true;
                                            hasChanged = true;
                                        }

                                        if (secondTAlertMin > 0 &&
                                            slot.Countdown.TotalMinutes <= secondTAlertMin &&
                                            slot.Countdown.TotalMinutes > (secondTAlertMin - 1) && // Ensure it fires once per minute
                                            !slot.AlertFlags[Globals.Tefila2] && (slot.Id == "b2EOT1" || slot.Id == "b1EOT2"))
                                        {
                                            PlayAlert(slot.Id, Globals.Tefila2); // Still pass "30" to choose the WAV file
                                            slot.AlertFlags[Globals.Tefila2] = true;
                                        }


                                    }
                                }
                           
                            }

                            if (DateTime.Now <= _internalSunsetTime.AddMinutes(11) && DateTime.Now >= _internalSunsetTime)
                            {
                                if (DateTime.Now <= _internalSunsetTime.AddSeconds((10 * 60) + 15) && !TenMinSunSet && SunSet10Alert)
                                {
                                    PlayAlert(Globals.SuS, Globals.Sunset10);
                                    TenMinSunSet = true;
                                }
                                else if (DateTime.Now <= _internalSunsetTime.AddSeconds((3 * 60) + 15) && !ThreeMinSunSet && SunSet3Alert)
                                {
                                    PlayAlert(Globals.SuS, Globals.Sunset3);
                                    ThreeMinSunSet = true;
                                }
                                else if (DateTime.Now <= _internalSunsetTime.AddSeconds(15) && !SunSet && SunSetAlert)
                                {
                                    PlayAlert(Globals.SuS, Globals.Sunset3);
                                    SunSet = true;
                                }
                            }

                            // Step 1: Ensure _internalSunriseTime is always updated for the current Gregorian day.
                            // This is crucial if the application runs continuously past midnight,
                            // as _internalSunriseTime would otherwise remain from the previous day.
                            if (_internalSunriseTime.Date != DateTime.Today && _hasReloadedForCurrentSunriseCycle)
                            {
                                Logger.LogInfo($"New Gregorian day detected. Excel data reloaded to update current day's times. Sunrise: {_internalSunriseTime:HH:mm:ss}");
                                // It's a new Gregorian day, or _internalSunriseTime hasn't been updated for today yet.
                                // Reload Excel data to get the correct sunrise time for today.
                                _hasReloadedForCurrentSunriseCycle = false; // Reset the flag for the new day's cycle

                                // Now, _internalSunriseTime is guaranteed to be for DateTime.Today.
                                // Step 2: Calculate the specific reload trigger time for today's sunrise.
                                TimeSpan timeOnly = new TimeSpan(0, 05, 0);
                                _reloadTriggerTime = DateTime.Today.Add(timeOnly);

                                //_reloadTriggerTime = _internalSunriseTime.Subtract(TimeSpan.FromMinutes(72));

                                // Step 3: Check if it's time to perform the scheduled daily reload.
                                // This condition ensures:
                                // 1. The current time is past the calculated trigger time.
                                // 2. We haven't already reloaded for *this specific sunrise cycle*.
                                //    (We use _hasReloadedForCurrentSunriseCycle to prevent multiple reloads within the same cycle).
                                Logger.LogInfo($"Triggering scheduled daily Excel reload. Current Time: {DateTime.Now:HH:mm:ss}, Reload Trigger Time: {_reloadTriggerTime:HH:mm:ss}");
                                LoadFromExcel(); // Perform the actual scheduled reload
                                _hasReloadedForCurrentSunriseCycle = true; // Mark that reload has happened for this cycle
                            }

                            IsAlertNotActive = !IsAlertActive;
                            if(hasChanged)
                                UpdateSlotCollections(); // Update the TopSlots/BottomSlots based on alert state
                            OnPropertyChanged(nameof(CurrentTime)); // Update current time in footer
                                                                    // HebrewDate update is less frequent, can be done daily or on language switch
                                                                    // OnPropertyChanged(nameof(HebrewDate)); // Uncomment if you want it to refresh every second
                        });

                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error In Inner catch - occured while running the main timer thread. exception: {ex.Message}. inner exception: {ex.InnerException}");
                    }
                    finally
                    {
                        Logger.LogError($"Error In finally - occured while running the main timer thread.");
                    }
                };
                _timer.Start();

            }
            catch (Exception ex)
            {
                Logger.LogError($"In outer catch - Error occured while running the main timer thread. exception: {ex.Message}. inner exception: {ex.InnerException}");
            }
        }

        private void LoadFromExcel()
        {
            Logger.LogInfo("Loading from excel file...");
            TimeSlots.Clear(); // Clear existing slots before Loading

            var today = DateTime.Today;
            DataRow todayRow = null;

            TodayDate = DateTime.Now.ToString("dd/MM/yyyy");

            HebrewDate = GetHebrewJewishDateString(today, false); // Calculate if not in Excel

            string path = Properties.Settings.Default.ExcelFilePath;

            if (!File.Exists(path))
            {
                path = @"C:\DailyTimes.xlsx";
                if (!File.Exists(path))
                {
                    Logger.LogWarning($"Excel file '{path}' not found. Loading mock data.");
                    LoadMock();
                    return;
                }
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
                        AddSlot(Globals.EOS2, ParseTimeFromCell(todayRow, "EOS2"));
                        AddSlot(Globals.EOS1, ParseTimeFromCell(todayRow, "EOS1"));
                        AddSlot(Globals.EOT2, ParseTimeFromCell(todayRow, "EOT2"));
                        AddSlot(Globals.EOT1, ParseTimeFromCell(todayRow, "EOT1"));

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

                        SetSettingsProperties();
                        SetSunSetFields();

                        // Check for any parsing errors using the internal DateTime fields
                        if (TimeSlots.Any(s => s.Time == DateTime.MinValue) ||
                            _internalSunriseTime == DateTime.MinValue || 
                            _internalMiddayTime == DateTime.MinValue || 
                            _internalSunsetTime == DateTime.MinValue)
                        {
                            Logger.LogWarning("Some times could not be parsed from Excel. Using mock data for missing values.");
                            // Optionally, you could try to fill in only the missing values with mock data here
                        }
                    }
                    Logger.LogInfo("The Zmanim from today was loaded successfully");
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
                slot.AlertFlags = new Dictionary<string, bool>()
                {
                    [Globals.Visual] = false,
                    [Globals.Shema1] = false,
                    [Globals.Shema2] = false,
                    [Globals.Tefila2] = false
                };
            }
        }

        public void SetSettingsProperties()
        {
            Logger.LogInfo("Set Fields started");
            visualAlertMin = Properties.Settings.Default.VisualAlertMinutes;

            firstAlertMin   = Properties.Settings.Default.FirstAlertMinutes;
            secondAlertMin  = Properties.Settings.Default.SecondAlertMinutes;
            firstTAlertMin  = Properties.Settings.Default.FirstAlertTefilaMinutes;
            secondTAlertMin  = Properties.Settings.Default.SecondAlertTefilaMinutes;

            SunSet10Alert = Properties.Settings.Default.SunSetTenMin;
            SunSet3Alert = Properties.Settings.Default.SunSetThreeMin;
            SunSetAlert = Properties.Settings.Default.SunSet;
        }

        public void SetSunSetFields()
        {
            TenMinSunSet = false;
            ThreeMinSunSet = false;
            SunSet = false;
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
            _internalMiddayTime = DateTime.ParseExact("00:00", "HH:mm", CultureInfo.InvariantCulture);
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
                AlertFlags = new Dictionary<string, bool>() { 
                    [Globals.Visual] = false, 
                    [Globals.Shema1] = false, 
                    [Globals.Shema2] = false,
                    [Globals.Tefila2] = false
                }
            });
        }

        private void PlayAlert(string slotId, string alertFlag)
        {
            // Option 1: Play from embedded resource (preferred)
            string fileName = String.Empty;
            string extFileName = String.Empty;

            if (slotId == Globals.EOS1)
            {
                if (alertFlag == Globals.Shema1 && !string.IsNullOrEmpty(Properties.Settings.Default.EOS1FirstAlertPath))
                    extFileName = Properties.Settings.Default.EOS1FirstAlertPath;
                else if (alertFlag == Globals.Shema2 && !string.IsNullOrEmpty(Properties.Settings.Default.EOS1SecondAlertPath))
                    extFileName = Properties.Settings.Default.EOS1SecondAlertPath;
            }
            else if (slotId == Globals.EOS2)
            {
                if (alertFlag == Globals.Shema1 && !string.IsNullOrEmpty(Properties.Settings.Default.EOS2FirstAlertPath))
                    extFileName = Properties.Settings.Default.EOS2FirstAlertPath;
                else if (alertFlag == Globals.Shema2 && !string.IsNullOrEmpty(Properties.Settings.Default.EOS2SecondAlertPath))
                    extFileName = Properties.Settings.Default.EOS2SecondAlertPath;
            }
            else if (slotId == Globals.EOT2 &&
                alertFlag == Globals.Tefila2 &&
                !string.IsNullOrEmpty(Properties.Settings.Default.EOT2FirstAlertPath))
                extFileName = Properties.Settings.Default.EOT2FirstAlertPath;
            else if (slotId == Globals.SuS)
            {
                if (alertFlag == Globals.Sunset10 && Properties.Settings.Default.SunSetTenMin)
                    extFileName = Properties.Settings.Default.SunSetTenPath;
                else if (alertFlag == Globals.Sunset3 && Properties.Settings.Default.SunSetThreeMin)
                    extFileName = Properties.Settings.Default.SunSetThreePath;
                else if (alertFlag == Globals.Sunset && Properties.Settings.Default.SunSet)
                    extFileName = Properties.Settings.Default.SunSetPath;
            }
            else
                fileName = $"alert{slotId}_{alertFlag}.wav";
            try
            {
                SoundPlayer player = null;
                if (!string.IsNullOrEmpty(extFileName))
                {
                    player = new SoundPlayer(extFileName);
                    Logger.LogInfo($"Playing resource from settings");
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
                    Logger.LogInfo($"Playing resource from Resources.resx: {resourceKey}");
                }
                else
                {
                    Logger.LogWarning($"Resource not found in Resources.resx. and settings not set for {slotId} alert {alertFlag}");
                    return;
                }
                Logger.LogInfo($"Playing allert for {slotId} alert {alertFlag}");
                player.Play();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error playing embedded sound: {ex.Message}");
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

        private bool IsNowInSunSetAlertRange()
        {
            return DateTime.Now <= _internalSunsetTime.AddMinutes(11) && DateTime.Now >= _internalSunsetTime;
        }     
       
        public ImageSource SlideshowImage
        {
            get => _slideshowImage;
            private set { _slideshowImage = value; OnPropertyChanged(); }
        }

        public bool IsSlideshowActive
        {
            get => _isSlideshowActive;
            private set { _isSlideshowActive = value; OnPropertyChanged(); }
        }

        public string SlideshowHeaderText
        {
            get => _slideshowHeaderText;
            set { _slideshowHeaderText = value; OnPropertyChanged(); }
        }

        public string SlideshowTransitionType
        {
            get => _slideshowTransition.ToString();
            set
            {
                if (Enum.TryParse<TransitionType>(value, true, out var t))
                {
                    _slideshowTransition = t;
                    OnPropertyChanged();
                    if (_slideshowService != null) _slideshowService.Transition = t;
                }
            }
        }

        // call during ctor (after Initialize, once Application.Current is available)
        private void InitSlideshow()
        {
            try
            {
                _slideshowService = new SlideshowService(Application.Current.Dispatcher)
                {
                    ActivationInterval = TimeSpan.FromSeconds(Properties.Settings.Default.SlideshowActivationIntervalSeconds),
                    ReturnInterval = TimeSpan.FromSeconds(Properties.Settings.Default.SlideshowReturnIntervalSeconds),
                    ImageDuration = TimeSpan.FromSeconds(Properties.Settings.Default.SlideshowImageDisplaySeconds),
                    Transition = Enum.TryParse(Properties.Settings.Default.SlideshowTransitionType, out TransitionType t) ? t : TransitionType.Fade,
                    ImagesFolder = Properties.Settings.Default.SlideshowImagesFolder,
                    HeaderText = Properties.Settings.Default.SlideshowHeaderText
                };

                SlideshowHeaderText = _slideshowService.HeaderText;
                _slideshowService.OnImageChanged += (img, trans) =>
                {
                    // When image changes, push to UI
                    SlideshowImage = img.ImageSource;
                    // store transition if needed
                    _slideshowTransition = trans;
                    // if you want to expose current image path: img.FilePath
                };

                _slideshowService.OnSlideshowToggled += (active) =>
                {
                    IsSlideshowActive = active;
                    // When slideshow active, set header/footer visibility binding in XAML to IsSlideshowActive
                };

                _slideshowService.OnError += (msg) =>
                {
                    Logger.LogInfo($"Slideshow: {msg}");
                };

                _slideshowService.StartCycle();
            }
            catch (Exception ex)
            {
                Logger.LogError($"InitSlideshow error: {ex.Message}", ex);
            }
        }

        // call _slideshowService?.Dispose() in StopTimer or disposal path
        internal void ShutdownSlideshow()
        {
            try
            {
                _slideshowService?.Dispose();
                _slideshowService = null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ShutdownSlideshow error: {ex.Message}", ex);
            }
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