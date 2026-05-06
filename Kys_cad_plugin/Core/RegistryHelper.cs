using Microsoft.Win32;

namespace Kys_cad_plugin.Core
{
    public static class RegistryHelper
    {
        private const string REGISTRY_PATH = @"SOFTWARE\KysCadPlugin";
        private const string KEY_NAME = "LicenseKey";

        public static void SaveLicenseKey(string key)
        {
            using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
            {
                rk.SetValue(KEY_NAME, key);
            }
        }

        public static string GetLicenseKey()
        {
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
            {
                return rk?.GetValue(KEY_NAME)?.ToString() ?? string.Empty;
            }
        }

        public static void DeleteLicenseKey()
        {
            using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH))
            {
                rk.DeleteValue(KEY_NAME, false);
            }
        }
    }
}