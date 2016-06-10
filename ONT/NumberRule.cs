using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ONT
{
    public class NumberRule : ValidationRule
    {
        public int Min { get; set; }
        
        public int Max { get; set; }

        public NumberRule() : base() { }

        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            int Number = 0;

            if (!Int32.TryParse((String)value, out Number))
            {
                return new ValidationResult(false, "Value doesn't look like a number");
            }

            if (Number < Min)
            {
                return new ValidationResult(false, "Number entered is too small");
            }
            if (Number > Max)
            {
                return new ValidationResult(false, "Number entered is too large");
            }

            return new ValidationResult(true, null);
        }
    }
}
