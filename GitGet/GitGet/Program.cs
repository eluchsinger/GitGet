using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using LibGit2Sharp;

namespace GitGet
{
    class Program
    {
        private const string REPO_URL = "https://github.com/Microsoft/MixedRealityToolkit-Unity.git";
        private static readonly TimeSpan DelaySpan = new TimeSpan(0, 0, 0, 1);
        private static Stopwatch stopwatch;

        static void Main(string[] args)
        {
            try
            {
                var unityAssetsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
#if !DEBUG
                if (!Directory.Exists(unityAssetsFolder))
                {
                    throw new Exception("Please put this tool in the root of the desired Unity project.");
                }
#endif
                var tempPath = Path.Combine(InitializeTempFolder(), GetGitProjectName(REPO_URL));

                if(CloneOrPull(REPO_URL, tempPath) || !Directory.Exists(unityAssetsFolder)) { 
                    DirectoryCopy(Path.Combine(tempPath, "Assets\\HoloToolkit"), unityAssetsFolder, true);
                    Console.WriteLine("Moved into Unity assets");
                }
                else
                {
                    Console.WriteLine("Apparently, there were no significant changes to move into Unity Assets");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.WriteLine("Finito...");
            }
        }

        private static string InitializeTempFolder()
        {
            var tempPath = Path.GetTempPath();
            var directoryInfo = Directory.CreateDirectory(Path.Combine(tempPath, "GitGets"));
            return directoryInfo.FullName;
        }

        private static string GetGitProjectName(string gitUrl)
        {
            var newUrl = gitUrl.Replace(".git", "");
            newUrl = newUrl.Replace("http://github.com", "");
            newUrl = newUrl.Replace("https://github.com", "");
            newUrl = newUrl.Replace("/", "");
            return newUrl;
        }

        /// <summary>
        /// Clones or pulls, depending if the repository was already local or not.
        /// Return true if there were changes worth to copy to the Assets folder.
        /// </summary>
        /// <param name="gitUrl"></param>
        /// <param name="workDir"></param>
        /// <returns>Return true if there were changes worth to copy to the Assets folder.</returns>
        private static bool CloneOrPull(string gitUrl, string workDir)
        {
            if (Repository.IsValid(workDir))
            {
                using (var repo = new Repository(workDir))
                {
                    Console.WriteLine("Pulling from origin. Please wait!");
                    var options = new PullOptions();
                    var result = Commands.Pull(repo, new Signature("GitGet", "eluchsin@hsr.ch", DateTimeOffset.Now), options);

                    switch (result.Status)
                    {
                        case MergeStatus.UpToDate:
                            return false;
                        case MergeStatus.Conflicts:
                            throw new Exception("There were merge conflicts. Houston, we have a problem (fix it yourself)");
                        default:
                            return true;
                    }
                }
                
            }
            else
            {
                CloneOptions options = new CloneOptions()
                {
                    OnCheckoutProgress = OnCheckoutProgress,
                    OnProgress = OnProgress,
                    OnTransferProgress = OnTransferProgress
                };

                Console.WriteLine("Cloning Git repo using HTTP. Please wait!");
                Repository.Clone(gitUrl, workDir, options);
                Console.WriteLine("Cloned into: " + workDir);
                return true;
            }
        }

        private static void OnCheckoutProgress(string path, int completedSteps, int totalSteps)
        {
            if (completedSteps.Equals(0))
            {
                Console.WriteLine("");
            }

            if (completedSteps.Equals(totalSteps))
            {
                string output = $"Completed steps: {completedSteps} / {totalSteps}";
                Console.WriteLine($"\r{output}");
                return;
            }

            if(stopwatch == null || (stopwatch.IsRunning && stopwatch.Elapsed.CompareTo(DelaySpan) > 0)) { 
                string output = $"Completed steps: {completedSteps} / {totalSteps}";
                Console.Write($"\r{FillRestOfConsoleWithWhites(output)}");
                stopwatch = Stopwatch.StartNew();
            }
        }

        private static bool OnTransferProgress(TransferProgress progress)
        {
            if (stopwatch == null || (stopwatch.IsRunning && stopwatch.Elapsed.CompareTo(DelaySpan) > 0))
            {
                string output = $"Received Objects: {progress.ReceivedObjects} / {progress.TotalObjects}";
                Console.Write($"\r{FillRestOfConsoleWithWhites(output)}");
                stopwatch = Stopwatch.StartNew();
            }

            return true;
        }

        private static bool OnProgress(string serverProgressOutput)
        {
            if (stopwatch == null || (stopwatch.IsRunning && stopwatch.Elapsed.CompareTo(DelaySpan) > 0))
            {
                Console.Write($"\r{FillRestOfConsoleWithWhites(serverProgressOutput)}");
                stopwatch = Stopwatch.StartNew();
            }

            return true;
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the source directory does not exist, throw an exception.
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory does not exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }


            // Get the file contents of the directory to copy.
            FileInfo[] files = dir.GetFiles();

            foreach (FileInfo file in files)
            {
                // Create the path to the new copy of the file.
                string temppath = Path.Combine(destDirName, file.Name);

                // Copy the file.
                file.CopyTo(temppath, false);
            }

            // If copySubDirs is true, copy the subdirectories.
            if (copySubDirs)
            {

                foreach (DirectoryInfo subdir in dirs)
                {
                    // Create the subdirectory.
                    string temppath = Path.Combine(destDirName, subdir.Name);

                    // Copy the subdirectories.
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        private static string FillRestOfConsoleWithWhites(string desiredOutput)
        {
            return desiredOutput + new string(' ', Console.BufferWidth - desiredOutput.Length - 1);
        }
    }
}
