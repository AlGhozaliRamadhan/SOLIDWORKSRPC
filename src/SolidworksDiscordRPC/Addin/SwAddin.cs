using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;

namespace SolidworksDiscordRPC
{
    /// <summary>
    /// SolidWorks add-in entry point.
    /// Loads inside SolidWorks, connects to Discord, keeps Rich Presence in sync
    /// with the active document, and hosts the TaskPane settings UI.
    /// </summary>
    [ComVisible(true)]
    [Guid("2F6A6B2E-9F10-4B7B-9C7A-E3D9B9B46B1D")]
    public class SwAddin : ISwAddin
    {
        private ISldWorks _swApp;
        private int _addinCookie;
        private DiscordPresenceManager _presence;
        private DocumentTracker _tracker;
        private TaskPane.TaskPaneManager _taskPane;
        private PresenceSettings _settings;

        // -----------------------------------------------------------------
        // ISwAddin
        // -----------------------------------------------------------------

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            _swApp = (ISldWorks)ThisSW;
            _addinCookie = Cookie;

            // Required so SolidWorks can route add-in callbacks to us.
            _swApp.SetAddinCallbackInfo2(0, this, _addinCookie);

            _settings = PresenceSettings.Load();

            _presence = new DiscordPresenceManager();
            if (_settings.PresenceEnabled)
            {
                _presence.Initialize();
            }

            _tracker = new DocumentTracker(_swApp, _presence, _settings);
            _tracker.Start();

            // TaskPane — best-effort, never let UI hosting failures break the add-in.
            try
            {
                _taskPane = new TaskPane.TaskPaneManager(_swApp, _settings, OnSettingsChangedFromUI);
                _taskPane.Create();
            }
            catch (Exception)
            {
                // Swallow — TaskPane is optional; presence tracking still works.
                _taskPane = null;
            }

            return true;
        }

        public bool DisconnectFromSW()
        {
            try
            {
                _taskPane?.Dispose();
            }
            catch
            {
                // ignore
            }

            _taskPane = null;

            _tracker?.Dispose();
            _tracker = null;

            _presence?.Dispose();
            _presence = null;

            _swApp = null;

            return true;
        }

        // -----------------------------------------------------------------
        // Settings change callback from TaskPane UI thread
        // -----------------------------------------------------------------

        private void OnSettingsChangedFromUI(PresenceSettings newSettings)
        {
            try
            {
                _settings = newSettings ?? _settings;
                _settings.Save();

                _tracker?.UpdateSettings(_settings);

                if (_settings.PresenceEnabled)
                {
                    if (_presence != null && !_presence.IsInitialized)
                    {
                        _presence.Initialize();
                    }
                    _tracker?.RefreshNow();
                }
                else
                {
                    _presence?.ClearPresence();
                }
            }
            catch
            {
                // Never let a UI settings change crash SolidWorks.
            }
        }

        // -----------------------------------------------------------------
        // COM registration - delegates to AddinRegistration to avoid duplication
        // -----------------------------------------------------------------

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                var guid = new Guid(t.GUID.ToString());
                AddinRegistration.Register(guid);
            }
            catch
            {
                // If AddinRegistration fails, fall back to manual so regasm still reports?
                // At this point regasm is running elevated, so the HKLM write should succeed.
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                var guid = new Guid(t.GUID.ToString());
                AddinRegistration.Unregister(guid);
            }
            catch
            {
                // ignore - unregistration is best-effort
            }
        }
    }
}
