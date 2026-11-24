using Microsoft.Win32;
using System.Windows.Forms;

namespace GHelper.Helpers
{
    /// <summary>
    /// Manages the per-user Run registry entry so the tray app starts with Windows.
    /// </summary>
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "G-Helper Mouse";

        private static string Command => "\"" + Application.ExecutablePath + "\"";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                var value = key?.GetValue(AppName) as string;
                return !string.IsNullOrEmpty(value)
                    && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Startup check failed: " + ex.Message);
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key is null) return;

                if (enabled)
                    key.SetValue(AppName, Command, RegistryValueKind.String);
                else
                    key.DeleteValue(AppName, false);

                Logger.WriteLine("Startup " + (enabled ? "enabled" : "disabled"));
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Startup toggle failed: " + ex.Message);
            }
        }
    }
}
