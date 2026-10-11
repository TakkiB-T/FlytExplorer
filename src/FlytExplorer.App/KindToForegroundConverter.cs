using FlytExplorer.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FlytExplorer.App;

public sealed class KindToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value is FileEntryKind.Directory ? "NordicAccentBrush" : "NordicTextMutedBrush";
        return Application.Current.Resources.TryGetValue(key, out var brush)
            ? brush
            : (object)new SolidColorBrush();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
