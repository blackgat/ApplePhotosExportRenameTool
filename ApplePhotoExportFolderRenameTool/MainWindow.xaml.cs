using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                // 1. List all subdirectories under the target directory
                var directories = Directory.GetDirectories(_selectedPath);

                Parallel.ForEach(directories, directory =>
                {
                    var parentDirectory = Directory.GetParent(directory).ToString();

                    // 2. Separate the date part using a regular expression for each subdirectory name
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
                    // 3. For the date part, use the regular expression to separate the year, month, and day, and finally use the YYYY-MM-DD format to replace the original date format
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

                    // 4. Move the data to the directory after the name is modified
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

                    var exifToolWrapper = new ExifToolWrapper();
                    if (exifToolWrapper.CheckToolExists())
                    {
                        // 5. if we have third party tool, we can using it to help modify the file creation time to match the exif information.
                        var fileEntries = Directory.GetFiles(newDirectory);

                        foreach (var fileEntry in fileEntries)
                        {
                            exifToolWrapper.Run(fileEntry);

                            if (exifToolWrapper.HasExifData())
                            {
                                var target = exifToolWrapper.Find("Create Date");

                                if (target != null)
                                {
                                    var createDate = DateTime.ParseExact(target.Value, "yyyy:MM:dd HH:mm:ss.FFF", null);
                                    File.SetCreationTime(fileEntry, createDate);
                                    File.SetLastAccessTime(fileEntry, DateTime.Now);
                                    File.SetLastWriteTime(fileEntry, createDate);

                                    // TODO: Double check the file in the correct date folder, assume Photos app export correctly.
                                }
                            }
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

        private void BtnCorrectDate_Click(object sender, RoutedEventArgs e)
        {
            UiLog("BtnCorrectDate_Click");
            Task.Run(() =>
            {
                var directories = Directory.GetDirectories(_selectedPath);

                Parallel.ForEach(directories, directory =>
                {
                    try
                    {
                        var exifToolWrapper = new ExifToolWrapper();
                        if (exifToolWrapper.CheckToolExists())
                        {
                            UiLog($"Begin process files in {Path.GetFileName(directory)}");
                            // 5. if we have third party tool, we can using it to help modify the file creation time to match the exif information.
                            var fileEntries = Directory.GetFiles(directory);

                            foreach (var fileEntry in fileEntries)
                            {
                                UiLog($"Parsing \"{fileEntry}\"...");
                                exifToolWrapper.Run(fileEntry);

                                if (exifToolWrapper.HasExifData())
                                {
                                    var target = exifToolWrapper.Find("Create Date");

                                    if (target != null)
                                    {
                                        UiLog($"Updating \"{fileEntry}\"...");
                                        var createDate = DateTime.ParseExact(target.Value, "yyyy:MM:dd HH:mm:ss.FFF", null);
                                        File.SetCreationTime(fileEntry, createDate);
                                        File.SetLastAccessTime(fileEntry, DateTime.Now);
                                        File.SetLastWriteTime(fileEntry, createDate);

                                        // TODO: Double check the file in the correct date folder, assume Photos app export correctly.
                                    }
                                }
                            }

                            UiLog($"Finished {fileEntries.Length} files in {Path.GetFileName(directory)}");
                        }
                        else
                        {
                            UiLog($"Do not found ExifTool.");
                        }
                    }
                    catch (Exception exception)
                    {
                        UiLog($"Exception: \"{exception.Message}\" on \"{directory}\"");
                    }
                });
            });
        }
    }
}
