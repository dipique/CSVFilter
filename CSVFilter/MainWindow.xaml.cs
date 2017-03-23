using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;

namespace CSVFilter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string[] pendingFiles = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void grdFiles_DragOver(object sender, DragEventArgs e)
        {
            //check if the files are csv
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                grdFiles.Background = new SolidColorBrush(Color.FromArgb(255, 241, 155, 97));
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
                grdFiles.Background = new SolidColorBrush(Color.FromArgb(255, 135, 164, 217));

            }
        }

        private void grdFiles_DragLeave(object sender, DragEventArgs e)
        {
            grdFiles.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
        }

        private void grdFiles_Drop(object sender, DragEventArgs e)
        {
            btnProcess.IsEnabled = false;

            //check if the files are csv
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
            filenames = filenames.Where(f => f.Substring(f.Length - 4).ToLower() == ".csv").ToArray();
            if (filenames.Count() == 0) return;

            pendingFiles = filenames;
            lblFilenames.Content = $"{filenames.Count()} ready to process.";
            btnProcess.IsEnabled = true;
        }

        private void btnProcess_Click(object sender, RoutedEventArgs e)
        {
            //make the santitizer object
            FileSanitizer f = GetWellnessCSVFileSanitizer();

            //make sure the output director
            DirectoryInfo di = new DirectoryInfo(FileSanitizer.OUTPUT_DIR);
            di.Create();

            //get the files to process and process them
            pendingFiles.ToList()
                        .ForEach(i => {
                            ResetFieldAttribute.Reset(f);
                            f.LoadFile(new FileInfo(i));
                            f.SaveSanitizedResults();
                        });

            lblFilenames.Content = "Processing complete.";
            pendingFiles = null;
            btnProcess.IsEnabled = false;

            Process.Start("explorer.exe", di.FullName);

        }

        const string email_regex = @"^(([\w-]+\.)+[\w-]+|([a-zA-Z]{1}|[\w-]{2,}))@"
                                 + @"((([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\."
                                 + @"([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])){1}|"
                                 + @"([a-zA-Z]+[\w-]+\.)+[a-zA-Z]{2,4})$";

        private FileSanitizer GetWellnessCSVFileSanitizer()
        {
            string disallowed = "*+`~!#$%^&*()={}[]|:;<>?";
            string[] allowedPayPeriods = new[] { "12", "24", "26", "52" };
            return new FileSanitizer(new[] {
                new FieldDefinition("GroupId", false, allowedChars: FieldDefinition.NUMERIC),
                new FieldDefinition("CompanyDivLocID", allowedChars: FieldDefinition.ALPHA),
                new FieldDefinition("CompanyDepartment", allowedChars: FieldDefinition.ALPHA),
                new FieldDefinition("StartDate", false, check: new DateCheck()),
                new FieldDefinition("EmployeeEmailAddress", false, regex: email_regex),
                new FieldDefinition("LastName", false, allowedChars: FieldDefinition.NAMECHARS),
                new FieldDefinition("FirstName", false, allowedChars: FieldDefinition.NAMECHARS),
                new FieldDefinition("MiddleName", allowedChars: FieldDefinition.NAMECHARS),
                new FieldDefinition("DateOfBirth", false, check: new DateCheck()),
                new FieldDefinition("Address1", false, allowedChars: FieldDefinition.ALPHANUMERIC),
                new FieldDefinition("Address2", allowedChars: FieldDefinition.ALPHANUMERIC),
                new FieldDefinition("City", false, allowedChars: FieldDefinition.ALPHA),
                new FieldDefinition("State", false, allowedChars: FieldDefinition.ALPHA),
                new FieldDefinition("Zip", false, minLength:5, maxLength:9, allowedChars: FieldDefinition.NUMERIC),
                new FieldDefinition("HomePhone", false, true, string.Empty, FieldDefinition.NUMERIC, 10,10),
                new FieldDefinition("WorkPhone", true, true, string.Empty, FieldDefinition.NUMERIC, 10,10),
                new FieldDefinition("SSN", false, true, string.Empty, FieldDefinition.NUMERIC, 9,9),
                new FieldDefinition("EducationMajor", true, true, string.Empty, string.Empty),
                new FieldDefinition("Occupation", true, true, string.Empty, string.Empty),
                new FieldDefinition("BenefitDollars", true, true, string.Empty, FieldDefinition.DECIMAL),
                new FieldDefinition("BirthCity", true, true, string.Empty, string.Empty),
                new FieldDefinition("BirthState", true, true, string.Empty, string.Empty),
                new FieldDefinition("BeneficiaryInformation", true, true, string.Empty, string.Empty),
                new FieldDefinition("Tobacco", true, true, string.Empty, string.Empty),
                new FieldDefinition("WellnessDeduction", true, true, string.Empty, string.Empty),
                new FieldDefinition("LifeInsurancePremium", true, true, string.Empty, string.Empty),
                new FieldDefinition("LifeInsuranceFaceValue", true, true, string.Empty, string.Empty),
                new FieldDefinition("UserGender", new GenderCheck()),
                new FieldDefinition("PayrollFrequency", true, allowedChars: FieldDefinition.NUMERIC, allowedValues: allowedPayPeriods)
            }, ",", disallowed.ToCharArray());
        }
    }
}
