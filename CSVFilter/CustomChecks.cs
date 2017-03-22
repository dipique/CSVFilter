using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVFilter
{
    public abstract class CustomCheck
    {
        /// <summary>
        /// The error returned when the minimum requirements for this check are not met.
        /// </summary>
        public virtual string ErrorString { get; set; }

        /// <summary>
        /// Accepts an input and returns a sanitized string or an error if needed
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public abstract void Execute(Value value);

        public CustomCheck() { }
    }

    public class DateCheck: CustomCheck
    {
        public override void Execute(Value value)
        {
            if (DateTime.TryParse(value.OriginalValue, out DateTime dt))
            {
                value.SanitizedValue = dt.ToString("M/d/yyyy");
            }
            else
            {
                value.SanitizedValue = value.OriginalValue;
                value.ErrorMsg = "Unable to recognize date in date field.";
            }
        }

        public DateCheck() { }
    }

    public class GenderCheck : CustomCheck
    {
        public override void Execute(Value value)
        {
            switch (value.OriginalValue.ToUpper()[0])
            {
                case 'M':
                    value.SanitizedValue = "M";
                    break;
                case 'F':
                    value.SanitizedValue = "F";
                    break;
                default:
                    value.SanitizedValue = value.OriginalValue;
                    value.ErrorMsg = "Unable to determine gender from entry.";
                    break;
            }
        }

        public GenderCheck() { }
    }
}
