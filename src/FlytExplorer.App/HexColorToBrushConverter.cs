using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FlytExplorer.App;

public sealed class HexColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && hex.Length == 7 && hex[0] == '#')
        {
            try
            {
                byte r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                byte g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                byte b = System.Convert.ToByte(hex.Substring(5, 2), 16);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            catch { }
        }
        return new SolidColorBrush(Color.FromArgb(255, 162, 176, 180)); // NordicTextMuted fallback
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}