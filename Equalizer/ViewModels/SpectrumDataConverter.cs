using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Equalizer.ViewModels
{
    public class SpectrumDataConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not null)
            {
                float dB = (float)value;
                // Приводим dB к диапазону [0, 1] для высоты
                float normalized = Math.Clamp((dB + 100) / 100f, 0, 1);
                return normalized * 100; // максимальная высота 100 пикселей    
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
