using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ONT
{
    /// <summary>
    /// Simple converter class to multiply one kind of unit into another.
    /// </summary>
    public class UnitConverter : IValueConverter
    {
        /// <summary>
        /// Multiplier to be used to convert values.
        /// </summary>
        public double Multiplier { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double a;
            a = GetDouble(value);
            return a * Multiplier;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double a;
            a = GetDouble(value);
            return a / Multiplier;
        }

        private static double GetDouble(object value)
        {
            double a;
            if (value != null)
            {
                try
                {
                    a = System.Convert.ToDouble(value);
                }
                catch
                {
                    a = 0;
                }
            }
            else
                a = 0;
            return a;
        }
    }
}
