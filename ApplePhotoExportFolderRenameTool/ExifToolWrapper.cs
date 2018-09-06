using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ApplePhotoExportFolderRenameTool
{
    public class ExifTagItem
    {
        public string Group;
        public string Name;
        public string Value;
    }

    public class ExifToolWrapper : List<ExifTagItem>
    {
        #region public methods

        public bool CheckToolExists()
        {
            var toolPath = GetAppPath();
            toolPath += "\\exiftool.exe";
            toolPath = $"\"{toolPath}\"";
            toolPath += " -ver";

            var output = "";
            if (!File.Exists("ExifTool.exe"))
            {
                return false;
            }

            try
            {
                output = Open(toolPath);
            }
            catch (Exception)
            {
                // ignored
            }

            // check the output
            if (output.Length < 4)
                return false;

            // (could check version number here if you care)
            return true;
        }

        public void Run(string filename, bool removeWhitespaceInTagNames = false)
        {
            // exiftool command
            var toolPath = GetAppPath();
            toolPath += "\\exiftool.exe";
            toolPath = $"\"{toolPath}\"";
            if (removeWhitespaceInTagNames)
                toolPath += " -s";
            toolPath += " -fast -G -t -m -q -q";
            toolPath += $" \"{filename}\"";

            var output = Open(toolPath);

            // parse the output into tags
            Clear();
            while (output.Length > 0)
            {
                var epos = output.IndexOf('\r');

                if (epos < 0)
                    epos = output.Length;
                var tmp = output.Substring(0, epos);
                var tpos1 = tmp.IndexOf('\t');
                var tpos2 = tmp.IndexOf('\t', tpos1 + 1);

                if (tpos1 > 0 && tpos2 > 0)
                {
                    var taggroup = tmp.Substring(0, tpos1);
                    ++tpos1;
                    var tagname = tmp.Substring(tpos1, tpos2 - tpos1);
                    ++tpos2;
                    var tagvalue = tmp.Substring(tpos2, tmp.Length - tpos2);

                    // special processing for tags with binary data 
                    tpos1 = tagvalue.IndexOf(", use -b option to extract", StringComparison.Ordinal);
                    if (tpos1 >= 0)
                    {
                        tagvalue = tagvalue.Remove(tpos1, 26);
                    }

                    var itm = new ExifTagItem
                    {
                        Name = tagname,
                        Value = tagvalue,
                        Group = taggroup
                    };
                    Add(itm);
                }

                // is \r followed by \n ?
                if (epos < output.Length)
                    epos += (output[epos + 1] == '\n') ? 2 : 1;
                output = output.Substring(epos, output.Length - epos);
            }
        }

        public bool HasExifData()
        {
            return Count > 0;
        }

        public ExifTagItem Find(string tagname)
        {
            return this.FirstOrDefault(x => x.Name.Equals(tagname));
            //var qItems = from tagItem in this
            //             where tagItem.Name == tagname
            //             select tagItem;
            //return qItems.First();
        }

        /// <summary>
        /// This method saves EXIF data to an external file ([file].exif). Only tags with group EXIF are saved.
        /// </summary>
        /// <param name="sourceImage">Source Image file path</param>
        /// <param name="destinationExifFile">Destination .exif file path</param>
        /// <returns>True if no error</returns>
        public bool SaveExifData(string sourceImage, string destinationExifFile)
        {
            // exiftool command
            var toolPath = GetAppPath();
            toolPath += "\\exiftool.exe";
            toolPath = $"\"{toolPath}\"";
            toolPath += " -fast -m -q -q";
            toolPath += $" -tagsfromfile \"{sourceImage}\"";
            toolPath += $" -exif \"{destinationExifFile}\"";

            var output = Open(toolPath);

            if (output.Contains("Error"))
                return false;

            return true;
        }

        /// <summary>
        /// This method writes EXIF data to the given destination image file (must exist beforehand).
        /// </summary>
        /// <param name="sourceExifFile">Source .exif file</param>
        /// <param name="destinationImage">Destination image path (file must exist)</param>
        /// <returns></returns>
        public bool WriteExifData(string sourceExifFile, string destinationImage)
        {
            // exiftool command
            var toolPath = GetAppPath();
            toolPath += "\\exiftool.exe";
            toolPath = $"\"{toolPath}\"";
            toolPath += " -fast -m -q -q";
            toolPath += $" -TagsFromFile \"{sourceExifFile}\"";
            toolPath += $" -all:all \"{destinationImage}\"";

            var output = Open(toolPath);

            if (output.Contains("Error"))
                return false;

            return true;
        }
        #endregion
        #region Private methods
        /// <summary>
        /// Gets the path from where the executable is being run
        /// </summary>
        /// <returns>Path</returns> 
        private string GetAppPath()
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            appPath = Path.GetDirectoryName(appPath);

            return appPath;
        }

        private string _stdOut;
        private string _stdErr;
        private ProcessStartInfo _psi;
        private Process _activeProcess;

        private void Thread_ReadStandardError()
        {
            if (_activeProcess != null)
            {
                _stdErr = _activeProcess.StandardError.ReadToEnd();
            }
        }

        private void Thread_ReadStandardOut()
        {
            if (_activeProcess != null)
            {
                _stdOut = _activeProcess.StandardOutput.ReadToEnd();
            }
        }

        private string Open(string cmd)
        {
            var program = "\"%COMSPEC%\"";
            var args = "/c \"[command]\"";
            _psi = new ProcessStartInfo(
                Environment.ExpandEnvironmentVariables(program),
                args.Replace("[command]", cmd)
            )
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var threadReadStandardError = new Thread(Thread_ReadStandardError);
            var threadReadStandardOut = new Thread(Thread_ReadStandardOut);

            _activeProcess = Process.Start(_psi);
            if (_psi.RedirectStandardError)
            {
                threadReadStandardError.Start();
            }
            if (_psi.RedirectStandardOutput)
            {
                threadReadStandardOut.Start();
            }
            _activeProcess?.WaitForExit();

            threadReadStandardError.Join();
            threadReadStandardOut.Join();

            var output = _stdOut + _stdErr;

            return output;
        }

        #endregion
    }
}
