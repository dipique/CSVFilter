using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        const string ERROR_SUFFIX = "_ERROR.csv";
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

            //get adder for position of row in file
            int adder = 1;
            if (HeaderRow != null) adder++;
            
            DataRows = Enumerable.Range(0, lineCount).Select(ind => new Row(dataLines[ind], delimiter, FieldCount, ind + adder, globalDisallowed)).ToArray();

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
}