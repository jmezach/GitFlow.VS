using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace GitFlow.VS
{
    public class GitHelper
    {
        public static string GetGitInstallationPath()
        {
            string gitPath = GetInstallPathFromEnvironmentVariable();
            if (gitPath != null)
                return gitPath;

            gitPath = GetInstallPathFromRegistry();
            if (gitPath != null)
                return gitPath;

            gitPath = GetInstallPathFromProgramFiles();
            if (gitPath != null)
                return gitPath;
            return null;
        }

        public static string GetInstallPathFromEnvironmentVariable()
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            var allPaths = path.Split(';');
            string gitPath = allPaths.FirstOrDefault(p => p.ToLower().TrimEnd('\\').EndsWith("git\\cmd"));
            if (gitPath != null)
            {
                gitPath = Directory.GetParent(gitPath).FullName.TrimEnd('\\');
            }
            return gitPath;
        }

        public static string GetInstallPathFromRegistry()
        {
            var installLocation = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1", "InstallLocation", null);
            if (installLocation != null)
                return installLocation.ToString().TrimEnd('\\');

            //try 32-bit OS
            installLocation = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1", "InstallLocation", null);
            if (installLocation != null)
                return installLocation.ToString().TrimEnd('\\');
            return null;
        }

        public static string GetInstallPathFromProgramFiles()
        {
            string gitPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "git");
            if (Directory.Exists(gitPath))
                return gitPath.TrimEnd('\\');
            gitPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "git");
            if (Directory.Exists(gitPath))
                return gitPath.TrimEnd('\\');
            return null;

        }
    }
}