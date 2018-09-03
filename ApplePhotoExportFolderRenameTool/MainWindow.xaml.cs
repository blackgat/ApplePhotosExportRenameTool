using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using Path = System.IO.Path;

namespace ApplePhotoExportFolderRenameTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
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
                    const string appleDefaultExportFolderPatten = @"^(?<area>.* - )?(?<street>.*, )?(?<date>.*)";
                    var rx = new Regex(
                        appleDefaultExportFolderPatten,
                        RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    Debug.Assert(currentDirectoryName != null);
                    var matchAappleDefaultExportFolderPatten = rx.Match(currentDirectoryName);
                    if (matchAappleDefaultExportFolderPatten.Captures.Count == 0)
                    {
                        UiLog($"No match folder string found!!! {directories}");
                        return;
                    }

                    var groups = matchAappleDefaultExportFolderPatten.Groups;

                    const string patternDateTime = @"(?<year>\d*)年(?<month>\d*)月(?<date>\d*)日";
                    var rxDateTime = new Regex(
                        patternDateTime,
                        RegexOptions.Compiled | RegexOptions.IgnoreCase);

                    var dateTime = groups["date"].Value;
                    var matchDateTime = rxDateTime.Match(dateTime);
                    if (matchDateTime.Captures.Count == 0)
                    {
                        UiLog($"No match string found!!! {directories}");
                        return;
                    }

                    var yearValue = int.Parse(matchDateTime.Groups["year"].Value);
                    var monthValue = int.Parse(matchDateTime.Groups["month"].Value);
                    var dateValue = int.Parse(matchDateTime.Groups["date"].Value);

                    var newDirectoryName = $"{yearValue:0000}-{monthValue:00}-{dateValue:00}";

                    UiLog($"\"{currentDirectoryName}\" found \"{dateTime}\", Rename as \"{newDirectoryName}\"");
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
                            UiLog($"Exception: {exception.Message}");
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
                            UiLog($"Exception: {exception.Message}");
                        }
                    }
                });
            });
        }

        private void UiLog(string msg)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
            {
                var newItem = new ListBoxItem
                {
                    Content = $"[{DateTime.Now}] {msg}"
                };
                ListBoxMessages.Items.Add(newItem);
                ListBoxMessages.ScrollIntoView(newItem);
            }));
        }
    }
}
