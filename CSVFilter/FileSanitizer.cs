using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CSVFilter
{
    class FileSanitizer
    {
        //whole-file meta-data & description
        public string[] delimiter { get; set; }
        public bool RemoveEmptyEntries { get; set; } = true;
        [ResetField] public bool? HasHeader { get; set; } = null;
        [ResetField] public string Filename { get; set; } = string.Empty;
        public string Error_Filename => $"{Output_Filename}{ERROR_SUFFIX}";
        public string Output_Filename => $"{OUTPUT_DIR}\\{Filename}";
        private char[] globalDisallowed { get; set; }

        //definitions
        public FieldDefinition[] FieldDefinitions { get; set; }
        public int FieldCount => FieldDefinitions.Count();

        //data
        [ResetField] public string[] HeaderRow { get; set; }
        public string HeaderRowString => string.Join(delimiter[0], HeaderRow);
        [ResetField] public Row[] DataRows { get; set; }
        public bool IsValid => ErrorMsg == string.Empty;
        [ResetField] public string ErrorMsg { get; set; }
        public bool HasErrors => DataRows.Any(d => !d.AllValidValues);

        //constants
        const string ERROR_SUFFIX = "_ERROR.TXT";
        const decimal HEADER_SIZE_CHANGE = .85m;
        string ERROR_HDR_ROW => $"Line{delimiter[0]}Field{delimiter[0]}Value{delimiter[0]}Error";
        public const string OUTPUT_DIR = "OUTPUT";
        const string HDR_STRIP_CHARS = "\"1234567890/\\@;";


        public FileSanitizer(FieldDefinition[] fields, string delim, char[] disallowedChars, bool removeEmptyEntries = true, bool? hasHeader = null)
        {
            globalDisallowed = disallowedChars;
            FieldDefinitions = fields;
            delimiter = new[] { delim };
            RemoveEmptyEntries = removeEmptyEntries;
            HasHeader = hasHeader;
        }

        public void LoadFile(FileInfo inputFile)
        {
            Filename = inputFile.Name;
            var data = File.ReadAllLines(inputFile.FullName);
            ParseContents(data);                    
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="inputFile">Contents of the file separated by line breaks</param>
        //public void LoadFile(string filename, string inputFile)
        //{
        //    Filename = filename;
        //    var data = inputFile.Split('\n').Select(s => s.Trim()).ToArray();
        //    ParseContents(data);
        //}

        //public void LoadFile(string filename, string[] inputFile)
        //{
        //    Filename = filename;
        //    ParseContents(inputFile);
        //}

        public void ParseContents(string[] data)
        {
            if (RemoveEmptyEntries) data = data.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            if (data.Count() == 0)
            {
                ErrorMsg = "Empty files cannot be processed.";
                return;
            }

            //Fill header and content vars
            string[] dataLines = null;
            if (HasHeader == true || IsHeader(data[0]))
            {
                HeaderRow = data[0].Split(delimiter, StringSplitOptions.None).ToArray();
                dataLines = data.Skip(1).ToArray();
            }
            else
            {
                Console.WriteLine($"No header detected in {Filename}");
                dataLines = data;
            }

            int lineCount = dataLines.Count();
            if (lineCount == 0) return;
            
            DataRows = Enumerable.Range(0, lineCount - 1).Select(ind => new Row(dataLines[ind], delimiter, FieldCount, ind, globalDisallowed)).ToArray();

            //Add any extra data rows found, if applicable
            DataRows = DataRows.Concat(DataRows.SelectMany(r => r.ExtraRows)).ToArray();
        }

        public bool IsHeader(string line)
        {
            //Is this a header? We try to validate by
            //taking the length headers with all the numbers and punctuation removed
            string hdrTxtStripped = new string(line.Where(c => !HDR_STRIP_CHARS.Any(a => a == c)).ToArray());

            //then we see if the length has changed more than 15%, which usually happens in data but not in headers
            return (line.Length * HEADER_SIZE_CHANGE) < hdrTxtStripped.Length;
        }

        public void SaveSanitizedResults()
        {
            Sanitize();

            var output = new List<string>();
            if (HeaderRow != null) output.Add(HeaderRowString);
            output.AddRange(DataRows.Select(r => r.ToRowString(delimiter[0])));
            File.WriteAllLines(Output_Filename, output.ToArray());

            //write error file if needed
            if (HasErrors)
            {
                output.Clear();
                if (HeaderRow != null) output.Add(ERROR_HDR_ROW);
                output.AddRange(DataRows.SelectMany(r => r.Values.Where(v => !v.IsValid)
                                                                 .Select(v => $"{r.Position}{delimiter[0]}{v.FieldName}{delimiter[0]}{v.OriginalValue}{delimiter[0]}{v.ErrorMsg}")));
                File.WriteAllLines(Error_Filename, output.ToArray());
            }
        }

        public void Sanitize()
        {
            //Execute santize method for each field on each line and add the result to the output
            int maxFieldIndex = FieldCount - 1;
            foreach (Value v in DataRows.SelectMany(r => r.Values))
            {
                //check for fields that don't exist
                if (v.FieldIndex > maxFieldIndex)
                {
                    v.SanitizedValue = v.OriginalValue;
                    v.ErrorMsg = "No field definition for this field.";
                    continue;
                }

                //if the field does exist, apply its sanitize method
                FieldDefinitions[v.FieldIndex].Sanitize(v);
            }
        }


    }

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
    public class Value
    {
        public int FieldIndex { get; set; }
        public string FieldName { get; set; }
        public string OriginalValue { get; set; } = string.Empty;
        public string SanitizedValue { get; set; }
        public bool IsValid => ErrorMsg == string.Empty;
        public string ErrorMsg { get; set; } = string.Empty;
        public bool Sanitized { get; set; } = false;
        public bool Missing { get; set; } = false;
    }

    public class Row
    {
        public string OriginalText { get; set; }
        public Value[] Values { get; set; }
        public bool AllValidValues => !Values.Any(v => !v.IsValid);
        public bool HasCorrectColCount { get; set; }

        public List<Row> ExtraRows = new List<Row>();
        public int Position { get; set; }
        public string ToRowString(string delim) => string.Join(delim, Values.Select(v => v.SanitizedValue));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contents"></param>
        /// <param name="delim"></param>
        /// <param name="correctColCount"></param>
        /// <param name="position"></param>
        /// <param name="disallowedChars">A list of disallowed characters to remove in addition to 
        /// those specified by the field. This is for support of globally disallowed characters.</param>
        public Row(string contents, string[] delim, int correctColCount, int position, char[] disallowedChars)
        {
            Position = position;

            //immediately get rid of any characters that are never ever legal
            OriginalText = PreSanitize(contents, disallowedChars);
            var stringVals = OriginalText.Split(delim, StringSplitOptions.None);
            HasCorrectColCount = correctColCount == stringVals.Count();

            //if this is a single row, save the values and scoot
            if (stringVals.Count() <= correctColCount)
            {
                Values = GetValues(stringVals, correctColCount);
                return;
            }

            //but wait! what if it's just two or more rows joined together? In that case it would be twice the number of values except
            //that two values would be smushed together. Luckily we can undo that.
            int tmp = stringVals.Count() - correctColCount;
            if (tmp % (correctColCount - 1) == 0)
            {
                var allRows = SeparateCombinedRows(stringVals, correctColCount, delim);
                ExtraRows.AddRange(allRows.Skip(1).Select(r => new Row(string.Join(delim[0], r), delim, correctColCount, Position, disallowedChars)));

                //set the parameters of this row
                OriginalText = string.Join(delim[0], allRows[0]);
                HasCorrectColCount = true;
                Values = GetValues(allRows[0], correctColCount);
            }
            else
            {
                Values = GetValues(stringVals, correctColCount);
            }
        }

        private Value[] GetValues(string[] vals, int correctColCount)
        {
            var retVal = Enumerable.Range(0, vals.Count() - 1).Select(f => new Value() {
                FieldIndex = f,
                OriginalValue = vals[f]
            }).ToArray();

            //Add "missing" values if needed
            if (!HasCorrectColCount)
            {
                retVal = retVal.Concat(Enumerable.Range(vals.Count(), correctColCount - 1)
                                                 .Select(f => new Value() {
                                                     FieldIndex = f,
                                                     Missing = true
                                                 })).ToArray();
            }
            return retVal;
        }

        private string[][] SeparateCombinedRows(string[] stringVals, int correctColCount, string[] delim)
        {
            List<string[]> retVal = new List<string[]>();

            //Add the first row (the first few values are easy)
            List<string> newValues = stringVals.Take(correctColCount - 1).ToList();

            //Now we just need to grab the last value, which will be mushed
            int firstMushedIndex = correctColCount;
            int firstMushedTextLength = stringVals[firstMushedIndex].Length;
            if (firstMushedTextLength == 0)
                newValues.Add(string.Empty);
            else
                newValues.Add(stringVals[firstMushedIndex].Substring(0, firstMushedTextLength));

            //Add these values to be returned
            retVal.Add(newValues.ToArray());

            //figure out how many more we have
            int tmp = stringVals.Count() - correctColCount;
            int extraRowCount = tmp / (correctColCount - 1); //we already know it's an int
            for (int x = 0; x < extraRowCount; x++)
            {
                List<string> tmpValues = new List<string>();

                //the first value is the second half of the last item in a row
                int mushedIndex = correctColCount + (x * (correctColCount - 1));
                int mushedLength = stringVals[mushedIndex].Length / 2; //we sure hope this is an integer, otherwise we guessed wrong
                if (mushedLength > 0)
                {
                    tmpValues.Add(stringVals[mushedIndex].Substring(mushedLength)); //this is the starting squished together value
                }
                else
                {
                    tmpValues.Add(string.Empty);
                }

                //the middle items come next
                if (correctColCount > 2) //if there aren't at least two, there won't be able middle items
                {
                    tmpValues.AddRange(stringVals.Skip(mushedIndex).Take(correctColCount - 2));
                }

                //the last item will be smushed together except for the final row, which will have a normal last value
                if ((x + 1) == extraRowCount)
                {
                    //add the last value, which will be correct and un-smushed
                    tmpValues.Add(stringVals[mushedIndex + correctColCount - 1 - 1]);
                }
                else
                {
                    //add the smushed final value
                    int lastMushedIndex = mushedIndex + correctColCount - 1 - 1;
                    int lastMushedLength = stringVals[lastMushedIndex].Length;
                    if (lastMushedLength > 0)
                    {
                        tmpValues.Add(stringVals[lastMushedIndex].Substring(0, lastMushedLength));
                    }
                    else
                    {
                        tmpValues.Add(string.Empty);
                    }
                }

                retVal.Add(tmpValues.ToArray());
                tmpValues.Clear();
            }

            return retVal.ToArray();
        }

        //get the text ready for analysis by removing certain characters and ensuring that there aren't quote-respecting sections
        private string PreSanitize(string value, char[] disallowedChars)
        {
            string retVal = value;
            if (retVal.Length == 0) return string.Empty;

            //get rid of quote-respection sections
            List<char> chars = new List<char>();
            bool insideQuotes = false;
            for (int x = 0; x < retVal.Length; x++)
            {
                char c = retVal[x];
                switch (c)
                {
                    case '\r':   //These are the characters that are NEVER legal and cause issues
                    case '\t':   
                    case '\"':   //toggle inside/outside quotes
                        insideQuotes = !insideQuotes;
                        break;
                    case ',':    //include the comma only as column break
                        if (!insideQuotes)
                            chars.Add(',');
                        else
                            chars.Add(' ');
                        break;
                    default:     //non-special characters get added
                        chars.Add(c);
                        break;
                }
            }

            //remove any globally disallowed characters
            return new string(chars.Where(c => !disallowedChars.Any(d => c == d))
                                   .ToArray());
        }        
    }

    /// <summary>
    /// Indicates that the field should be cleared on reset
    /// </summary>
    public class ResetFieldAttribute: Attribute
    {
        /// <summary>
        /// Resets properties in an object marked with this attribute. Assigned null to
        /// everything but strings, which it sets to string.empty
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        public static void Reset<T>(T obj)
        {
            typeof(T).GetProperties()
                     .Where(p => p.GetCustomAttribute(typeof(ResetFieldAttribute)) != null).ToList()
                     .ForEach(p => {
                         if (p.PropertyType == typeof(string))
                             p.SetValue(obj, string.Empty);
                         else
                             p.SetValue(obj, null);
                     });
        }

        public ResetFieldAttribute() { }
    }
}