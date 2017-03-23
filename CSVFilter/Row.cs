using System;
using System.Collections.Generic;
using System.Linq;

namespace CSVFilter
{
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
            var retVal = Enumerable.Range(0, vals.Count()).Select(f => new Value() {
                FieldIndex = f,
                OriginalValue = vals[f]
            }).ToArray();

            //Add "missing" values if needed
            if (!HasCorrectColCount)
            {
                retVal = retVal.Concat(Enumerable.Range(vals.Count(), correctColCount)
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
}