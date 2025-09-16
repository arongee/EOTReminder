
// Models/TimeSlot.cs

using EOTReminder.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EOTReminder.Models
{
    public class TimeSlot : INotifyPropertyChanged
    {
        private string _description;
        private bool _isPassed;
        private string _countdownText;
        private bool _showSandClock;
        private bool _highlight;
        private TimeSpan _countdown;
        private bool _isIn30MinAlert;
        private string _passedText;

        public string Id { get; set; }
        public DateTime Time { get; set; }

        public Dictionary<string, bool> AlertFlags { get; set; } = new Dictionary<string, bool>()
        {
            [Globals.Visual] = false,
            [Globals.Shema1] = false,
            [Globals.Shema2] = false,
            [Globals.Tefila2] = false
        };

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public string PassedText
        {
            get => _passedText;
            set
            {
                _passedText = value;
                OnPropertyChanged();
            } 
        }

        public bool IsPassed
        {
            get => _isPassed;
            set
            {
                _isPassed = value;
                OnPropertyChanged();
            }
        }

        public string CountdownText
        {
            get => _countdownText;
            set
            {
                _countdownText = value;
                OnPropertyChanged();
            }
        }

        public bool ShowSandClock
        {
            get => _showSandClock;
            set
            {
                _showSandClock = value;
                OnPropertyChanged();
            }
        }

        public bool Highlight
        {
            get => _highlight;
            set
            {
                _highlight = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan Countdown
        {
            get => _countdown;
            set
            {
                _countdown = value;
                OnPropertyChanged();
            }
        }

        public bool IsIn30MinAlert
        {
            get => _isIn30MinAlert;
            set
            {
                _isIn30MinAlert = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
