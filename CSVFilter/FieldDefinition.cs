using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CSVFilter
{
    public class FieldDefinition
    {
        public string Name { get; set; }
        public string DisallowedCharacters { get; set; } = string.Empty;
        public string AllowedCharacters { get; set; } = string.Empty;
        public bool AllowedBlank { get; set; } = true;
        public bool TrimField { get; set; } = true;
        public int MaxLength { get; set; } = int.MaxValue;
        public int MinLength { get; set; } = 0;
        public string RegEx { get; set; } = string.Empty;
        public string[] AllowedValues { get; set; } = null; //limit values to a certain set of options

        /// <summary>
        /// Only used if AllowedBlank=false
        /// </summary>
        public string ValueIfBlank { get; set; } = "ERROR: Missing required value.";
        public string ValueIfWrongLength { get; set; } = "ERROR: Invalid length.";
        public string ValueIfNotInAllowedOptions { get; set; } = "ERROR: Not a valid selection from the list of options.";

        public List<CustomCheck> CustomChecks = new List<CustomCheck>();

        //for convenience, some standard character sets
        public const string ALPHA = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ ";
        public const string NUMERIC = "1234567980";
        public const string DECIMAL = "1234567980.";
        public const string SYMBOLS = "/*-+`~!@#$%^&*()_+=-{}[]\\|:;<>,./?\"";
        public static string NAMECHARS => ALPHA + ". ";
        public static string ALPHANUMERIC => ALPHA + NUMERIC;

        public FieldDefinition(string name, bool allowedBlank = true, bool trimField = true, string disallowedChars = "", string allowedChars = "", int minLength = 0, int maxLength = int.MaxValue, CustomCheck check = null, string regex = null, string[] allowedValues = null)
        {
            Name = name;
            AllowedBlank = allowedBlank;
            TrimField = trimField;
            MaxLength = maxLength;
            MinLength = minLength;
            DisallowedCharacters = disallowedChars;
            AllowedCharacters = allowedChars;
            CustomChecks = new List<CustomCheck>();
            if (check != null) CustomChecks.Add(check);
            if (!string.IsNullOrWhiteSpace(regex)) RegEx = regex;
            AllowedValues = allowedValues;
        }

        public FieldDefinition(string name)
        {
            Name = name;
        }

        public FieldDefinition(string name, CustomCheck check)
        {
            Name = name;
            CustomChecks = new List<CustomCheck>() { check };
        }

        public FieldDefinition(string name, string regex)
        {
            Name = name;
            RegEx = regex;
        }

        public void Sanitize(Value value)
        {
            value.FieldName = Name ?? string.Empty;
            string sanitizedValue = TrimField ? value.OriginalValue.Trim()
                                              : value.OriginalValue;

            //If it wasn't in the original file, handle it in advance
            if (value.Missing || sanitizedValue == string.Empty)
            {
                if (!AllowedBlank)
                    value.ErrorMsg = ValueIfBlank;                    
                return;
            }

            //Removes any disallowed characters and retains only allowed characters
            var chars = sanitizedValue.ToCharArray();
            if (AllowedCharacters.Any())
                chars = chars.Where(c => AllowedCharacters.Any(a => a == c)).ToArray();
            if (DisallowedCharacters.Any())
                chars = chars.Where(c => !DisallowedCharacters.Any(d => d == c)).ToArray();

            //validate the length
            if (chars.Length > MaxLength ||
                chars.Length < MinLength)
            {
                value.SanitizedValue = new string(chars);
                value.ErrorMsg = ValueIfWrongLength;
                return;
            }

            //run regex
            sanitizedValue = new string(chars);
            if (RegEx != string.Empty && !Regex.IsMatch(sanitizedValue, RegEx))
            {
                value.ErrorMsg = "Value failed regex check on value.";
                return;
            }

            //run any custom checks
            value.SanitizedValue = RemoveDoubleSpaces(sanitizedValue); //remove any double spaces
            CustomChecks.ForEach(c => c.Execute(value));

            //If this there are a fixed number of options, check for them
            if ((AllowedValues?.Count() ?? 0) != 0 &&
                !AllowedValues.Any(v => v == value.SanitizedValue))
            {
                value.ErrorMsg = ValueIfNotInAllowedOptions;
            }
        }

        private static string RemoveDoubleSpaces(string value)
        {
            string retVal = value;
            string dbl = "  ";
            while (retVal.Contains(dbl))
            {
                retVal = retVal.Replace(dbl, " ");
            }
            return retVal;
        }
    }
}