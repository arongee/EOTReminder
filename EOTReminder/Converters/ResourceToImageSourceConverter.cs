using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Drawing; // IMPORTANT: You need to add a reference to System.Drawing in your project

namespace EOTReminder.Converters
{
    /// <summary>
    /// Converts a string resource name (e.g., "clock", "background") into a BitmapImage
    /// suitable for use as an ImageSource in WPF. It loads the image from the
    /// project's Properties.Resources.
    /// </summary>
    public class ResourceToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // The 'parameter' is expected to be the string name of the resource (e.g., "clock", "background")
            if (parameter is string resourceName && !string.IsNullOrEmpty(resourceName))
            {
                try
                {
                    // Get the resource object from Properties.Resources by its string name.
                    // This assumes the images (like clock.png, background.jpg) have been
                    // dragged into your project's Properties/Resources.resx file.
                    object resourceObject = Properties.Resources.ResourceManager.GetObject(resourceName);

                    // Check if the retrieved object is a System.Drawing.Bitmap
                    if (resourceObject is Bitmap bitmap)
                    {
                        // Convert the System.Drawing.Bitmap to a System.Windows.Media.Imaging.BitmapImage
                        // which is what WPF Image controls expect.
                        using (MemoryStream memory = new MemoryStream())
                        {
                            // Save the bitmap to a memory stream as a PNG.
                            // PNG format is generally good for transparency and quality.
                            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                            memory.Position = 0; // Reset stream position to the beginning

                            BitmapImage bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memory;
                            // Cache the image to improve performance.
                            // OnLoad means the entire image is loaded into memory when created.
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();

                            return bitmapImage; // Return the WPF-compatible image source
                        }
                    }
                    else if (resourceObject != null)
                    {
                        // Log a warning if the resource was found but is not of the expected Bitmap type.
                        System.Diagnostics.Debug.WriteLine($"Resource '{resourceName}' found but is not a System.Drawing.Bitmap. Actual Type: {resourceObject.GetType().Name}");
                        // You could also use your Logger utility here:
                        // EOTReminder.Utilities.Logger.LogWarning($"Resource '{resourceName}' found but is not a System.Drawing.Bitmap. Actual Type: {resourceObject.GetType().Name}");
                    }
                    else
                    {
                        // Log a warning if the resource was not found at all.
                        System.Diagnostics.Debug.WriteLine($"Resource '{resourceName}' not found in Properties.Resources.");
                        // EOTReminder.Utilities.Logger.LogWarning($"Resource '{resourceName}' not found in Properties.Resources.");
                    }
                }
                catch (Exception ex)
                {
                    // Log any exceptions that occur during the loading or conversion process.
                    System.Diagnostics.Debug.WriteLine($"Error loading resource '{resourceName}': {ex.Message}");
                    // EOTReminder.Utilities.Logger.LogError($"Error loading image resource '{resourceName}': {ex.Message}", ex);
                }
            }
            // Return null if the parameter is invalid or conversion fails,
            // which will result in no image being displayed.
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter is for one-way binding (source to target), so ConvertBack is not implemented.
            throw new NotImplementedException();
        }
    }
}