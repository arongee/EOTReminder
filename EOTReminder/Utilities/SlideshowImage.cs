using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;

using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EOTReminder.Utilities
{
    public enum TransitionType { Fade, Slide, None }

    public class SlideshowImage
    {
        public ImageSource ImageSource { get; set; }
        public string FilePath { get; set; }
    }

    public class SlideshowService : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Timer _activationTimer;      // toggles Normal -> Slideshow
        private readonly Timer _returnTimer;          // toggles Slideshow -> Normal
        private readonly Timer _imageTimer;           // rotates images inside slideshow
        private readonly object _sync = new object();

        private List<string> _imagePaths = new List<string>();
        private int _currentIndex = -1;
        private System.Threading.CancellationTokenSource _loadingCts;

        public event Action<SlideshowImage, TransitionType> OnImageChanged;
        public event Action<bool> OnSlideshowToggled; // true = active, false = stopped
        public event Action<string> OnError; // error messages for logging/UI

        public bool IsActive { get; private set; }

        public TimeSpan ActivationInterval { get; set; } = TimeSpan.FromSeconds(300);
        public TimeSpan ReturnInterval { get; set; } = TimeSpan.FromSeconds(60);
        public TimeSpan ImageDuration { get; set; } = TimeSpan.FromSeconds(3);
        public TransitionType Transition { get; set; } = TransitionType.Fade;
        public string ImagesFolder { get; set; } = string.Empty;
        public string HeaderText { get; set; } = string.Empty;

        public SlideshowService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _activationTimer = new Timer() { AutoReset = false };
            _activationTimer.Elapsed += ActivationTimer_Elapsed;

            _returnTimer = new Timer() { AutoReset = false };
            _returnTimer.Elapsed += ReturnTimer_Elapsed;

            _imageTimer = new Timer() { AutoReset = false };
            _imageTimer.Elapsed += ImageTimer_Elapsed;
        }

        // Call to (re)configure and start cycle
        public void StartCycle()
        {
            StopCycleInternal();
            LoadImagePaths(); // synchronous quick scan
            _activationTimer.Interval = Math.Max(1000, ActivationInterval.TotalMilliseconds);
            _activationTimer.Start();
            Log($"Slideshow cycle started. Activation in {ActivationInterval.TotalSeconds}s");
        }

        public void StopCycle()
        {
            StopCycleInternal();
            OnSlideshowToggled?.Invoke(false);
            Log("Slideshow cycle stopped.");
        }

        private void StopCycleInternal()
        {
            _activationTimer.Stop();
            _returnTimer.Stop();
            _imageTimer.Stop();
            CancelImageLoading();
            IsActive = false;
        }

        private void ActivationTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _dispatcher.Invoke(async () =>
            {
                if (!HasValidImages())
                {
                    Log("Slideshow skipped: no images available.");
                    // schedule next activation
                    _activationTimer.Interval = Math.Max(1000, ActivationInterval.TotalMilliseconds);
                    _activationTimer.Start();
                    return;
                }

                await ActivateSlideshowAsync();
            });
        }

        private async Task ActivateSlideshowAsync()
        {
            if (IsActive) return;
            IsActive = true;
            OnSlideshowToggled?.Invoke(true);

            // start image rotation
            _currentIndex = -1;
            StartImageRotation();

            // schedule return
            _returnTimer.Interval = Math.Max(1000, ReturnInterval.TotalMilliseconds);
            _returnTimer.Start();
            Log($"Slideshow activated for {ReturnInterval.TotalSeconds}s");
        }

        private void ReturnTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _dispatcher.Invoke(() =>
            {
                // stop image rotation, notify VM
                StopImageRotation();
                IsActive = false;
                OnSlideshowToggled?.Invoke(false);

                // schedule next activation
                _activationTimer.Interval = Math.Max(1000, ActivationInterval.TotalMilliseconds);
                _activationTimer.Start();
                Log($"Slideshow returned to normal. Next activation in {ActivationInterval.TotalSeconds}s");
            });
        }

        private void ImageTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _dispatcher.Invoke(async () => await ShowNextImageAsync());
        }

        private void StartImageRotation()
        {
            // immediate first image
            _imageTimer.Interval = 50;
            _imageTimer.Start();
        }

        private void StopImageRotation()
        {
            _imageTimer.Stop();
            CancelImageLoading();
            _currentIndex = -1;
        }

        private void CancelImageLoading()
        {
            lock (_sync)
            {
                _loadingCts?.Cancel();
                _loadingCts?.Dispose();
                _loadingCts = null;
            }
        }

        private async Task ShowNextImageAsync()
        {
            try
            {
                // set next index
                if (_imagePaths.Count == 0)
                {
                    Log("No images to show; stopping image timer.");
                    _imageTimer.Stop();
                    return;
                }

                _currentIndex = (_currentIndex + 1) % _imagePaths.Count;
                string path = _imagePaths[_currentIndex];

                // async load image into memory with OnLoad so file can be released
                var img = await LoadBitmapImageAsync(path);

                if (img == null)
                {
                    Log($"Failed to load image: {path}. Skipping.");
                    // try next immediately
                    _imageTimer.Interval = 50;
                    return;
                }

                OnImageChanged?.Invoke(new SlideshowImage { ImageSource = img, FilePath = path }, Transition);

                // schedule next tick after display duration
                _imageTimer.Interval = Math.Max(300, ImageDuration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"ShowNextImageAsync error: {ex.Message}");
                Log($"ShowNextImageAsync exception: {ex}");
                _imageTimer.Interval = Math.Max(1000, ImageDuration.TotalMilliseconds);
            }
        }

        private Task<BitmapImage> LoadBitmapImageAsync(string path)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(path)) return null;

                    // load with CacheOption.OnLoad so file can be unlocked after load
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.EndInit();
                    bmp.Freeze(); // freeze for cross-thread safe usage
                    return bmp;
                }
                catch (Exception ex)
                {
                    Log($"LoadBitmapImageAsync error for '{path}': {ex.Message}");
                    return null;
                }
            });
        }

        private void LoadImagePaths()
        {
            lock (_sync)
            {
                _imagePaths.Clear();
                try
                {
                    if (string.IsNullOrWhiteSpace(ImagesFolder) || !Directory.Exists(ImagesFolder))
                    {
                        Log($"Images folder not available: '{ImagesFolder}'");
                        return;
                    }

                    var files = Directory.EnumerateFiles(ImagesFolder, "*.png", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.EnumerateFiles(ImagesFolder, "*.jpg", SearchOption.TopDirectoryOnly))
                        .Where(f => IsImageFileSupported(f)).ToList();

                    _imagePaths = files;
                    if (!_imagePaths.Any())
                        Log($"No supported images in folder '{ImagesFolder}'");
                }
                catch (Exception ex)
                {
                    Log($"LoadImagePaths exception: {ex.Message}");
                }
            }
        }

        private bool IsImageFileSupported(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private bool HasValidImages()
        {
            lock (_sync)
            {
                return _imagePaths != null && _imagePaths.Count > 0;
            }
        }

        private void Log(string msg) => OnError?.Invoke(msg);

        public void Dispose()
        {
            StopCycleInternal();
            _activationTimer?.Dispose();
            _returnTimer?.Dispose();
            _imageTimer?.Dispose();
            CancelImageLoading();
        }
    }
}
