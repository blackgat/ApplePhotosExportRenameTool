using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;

namespace ApplePhotoExportFolderRenameTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _selectedPath = string.Empty;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnTarget_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;
                dialog.Multiselect = false;
                dialog.ShowHiddenItems = true;
                dialog.Title = @"Select the export root folder";
                dialog.DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var result = dialog.ShowDialog();
                if (result == CommonFileDialogResult.Ok)
                {
                    _selectedPath = dialog.FileName;
                    TextBoxTargetFolder.Text = _selectedPath;
                }
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                var directories = Directory.GetDirectories(_selectedPath);

                Parallel.ForEach(directories, directory =>
                {
                    var parentDirectory = Directory.GetParent(directory).ToString();
                    var currentDirectoryName = Path.GetFileName(directory);
                    var newDirectoryName = string.Empty;
                    var rx = new Regex(
                        @"^(?<area>.* - )?(?<street>.*, )?(?<date>.*)",
                        RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    Debug.Assert(currentDirectoryName != null);
                    var matches = rx.Matches(currentDirectoryName);
                    Debug.Assert(matches.Count == 1);
                    var dateTime = string.Empty;
                    foreach (Match match in matches)
                    {
                        var groups = match.Groups;

                        const string patternDateTime = @"(?<year>\d*)年(?<month>\d*)月(?<date>\d*)日";
                        var rxDateTime = new Regex(
                            patternDateTime,
                            RegexOptions.Compiled | RegexOptions.IgnoreCase);

                        dateTime = groups["date"].Value;
                        var matchDateTime = rxDateTime.Match(dateTime);
                        if (matchDateTime.Captures.Count == 0)
                        {
                            Debug.WriteLine($"No match string found!!! {directories}");
                            return;
                        }

                        var yearValue = int.Parse(matchDateTime.Groups["year"].Value);
                        var monthValue = int.Parse(matchDateTime.Groups["month"].Value);
                        var dateValue = int.Parse(matchDateTime.Groups["date"].Value);

                        newDirectoryName = $"{yearValue:0000}-{monthValue:00}-{dateValue:00}";
                    }

                    Debug.WriteLine($"\"{currentDirectoryName}\" found \"{dateTime}\", Rename as \"{newDirectoryName}\"");
                    var newDirectory = Path.Combine(parentDirectory, newDirectoryName);
                    if (Directory.Exists(newDirectory))
                    {
                        var srcFiles = Directory.GetFiles(directory);
                        foreach (var srcFile in srcFiles)
                        {
                            var srcFileInfo = new FileInfo(srcFile);
                            var destinationFile = Path.Combine(newDirectory, srcFileInfo.Name);
                            File.Move(srcFile, destinationFile);
                        }

                        try
                        {
                            Directory.Delete(directory);
                        }
                        catch (Exception exception)
                        {
                            Debug.WriteLine($"Exception: {exception.Message}");
                        }
                    }
                    else
                    {
                        try
                        {
                            Directory.Move(directory, newDirectory);
                        }
                        catch (Exception exception)
                        {
                            Debug.WriteLine($"Exception: {exception.Message}");
                        }
                    }
                });
            });
        }
    }
}
