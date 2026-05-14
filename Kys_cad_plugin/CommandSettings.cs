using Microsoft.Win32;

namespace Kys_cad_plugin.Core
{
    public static class CommandSettings
    {
        private const string RegistryKeyPath = @"Software\KysCadPlugin\Settings";
        private static bool _isPluginEnabled = true;

        static CommandSettings()
        {
            LoadSettings();
        }

        // 전체 명령어 활성화 여부 (하나로 통합)
        public static bool IsPluginEnabled
        {
            get => _isPluginEnabled;
            set { _isPluginEnabled = value; SaveSetting("IsPluginEnabled", value); }
        }

        private static void LoadSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        _isPluginEnabled = Convert.ToInt32(key.GetValue("IsPluginEnabled", 1)) == 1;
                    }
                }
            }
            catch { _isPluginEnabled = true; }
        }

        private static void SaveSetting(string name, bool value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    key?.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }
    }
}