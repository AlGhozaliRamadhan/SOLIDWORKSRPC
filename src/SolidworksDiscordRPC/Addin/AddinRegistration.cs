using System;
using Microsoft.Win32;

namespace SolidworksDiscordRPC
{
    /// <summary>
    /// Single source of truth for the two SolidWorks registry keys that control
    /// this add-in: Tools > Add-Ins presence and auto-load at startup.
    /// Used by both [ComRegisterFunction] in SwAddin and the TaskPane checkbox.
    /// </summary>
    internal static class AddinRegistration
    {
        public static readonly Guid AddinGuid = new Guid("2F6A6B2E-9F10-4B7B-9C7A-E3D9B9B46B1D");

        private const string AddinKeyTemplate = @"SOFTWARE\SolidWorks\Addins\{{{0}}}";
        private const string AddinStartupKeyTemplate = @"SOFTWARE\SolidWorks\AddInsStartup\{{{0}}}";

        public static void Register(Guid guid)
        {
            // Tools > Add-Ins entry
            using (var addinKey = Registry.LocalMachine.CreateSubKey(
                       string.Format(AddinKeyTemplate, guid)))
            {
                addinKey?.SetValue(null, 1); // 1 = enabled / listed
                addinKey?.SetValue("Title", "SOLIDWORKS Design");
                addinKey?.SetValue("Description",
                    "Shows the active SOLIDWORKS Design document on Discord Rich Presence.");
            }

            // Default to auto-load ON
            SetAutoLoadEnabled(true, guid);
        }

        public static void Unregister(Guid guid)
        {
            Registry.LocalMachine.DeleteSubKeyTree(
                string.Format(AddinKeyTemplate, guid), throwOnMissingSubKey: false);
            Registry.LocalMachine.DeleteSubKeyTree(
                string.Format(AddinStartupKeyTemplate, guid), throwOnMissingSubKey: false);
        }

        public static bool IsAutoLoadEnabled() => IsAutoLoadEnabled(AddinGuid);

        public static bool IsAutoLoadEnabled(Guid guid)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                           string.Format(AddinStartupKeyTemplate, guid)))
                {
                    var value = key?.GetValue(null);
                    return value != null && Convert.ToInt32(value) != 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetAutoLoadEnabled(bool enabled) => SetAutoLoadEnabled(enabled, AddinGuid);

        /// <summary>
        /// Returns false (instead of throwing) when the write fails — most
        /// likely because SolidWorks was launched non-elevated and HKLM isn't
        /// writable. Callers fall back to showing a message.
        /// </summary>
        public static bool SetAutoLoadEnabled(bool enabled, Guid guid)
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(
                           string.Format(AddinStartupKeyTemplate, guid)))
                {
                    key?.SetValue(null, enabled ? 1 : 0);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
