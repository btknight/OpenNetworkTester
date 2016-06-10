using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ONT
{
    /// <summary>
    /// Converts bandwidth in octets to a hybrid logarithmic scale for the slider control.
    /// </summary>
    public class BWSliderConverter : IValueConverter
    {
        /* The below math formula causes the following to happen on the slider control:
         *   From 0 to 1 megabit/sec (1000000 bits/sec), the scale is linear.
         *   From 1 megabit/sec to 1 gigabit/sec, the scale is logarithmic.
         * The formula first converts the above numbers from bits/sec to octets/sec.
         * Then it takes the Log base 10 of that octets/sec number.
         * Finally, it aligns the start of the log scale with the linear scale, by subtracting Log base 10 of 1 megabit/sec (125,000 octets/sec), minus 1.
         */

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double bandwidth;
            bandwidth = GetDouble(value);
            if (bandwidth < 125000)
            {
                return (double)bandwidth / 125000.0;
            }
            return Math.Log10(bandwidth) - (Math.Log10(125000) - 1);
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double logBandwidth;
            logBandwidth = GetDouble(value);
            if (logBandwidth < 1)
            {
                return (int)(logBandwidth * 125000.0);
            }
            else
            {
                return (int)Math.Pow(10, logBandwidth + (Math.Log10(125000) - 1));
            }
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
