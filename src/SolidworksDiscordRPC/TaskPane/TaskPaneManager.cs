using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SolidworksDiscordRPC.TaskPane
{
    /// <summary>
    /// Creates and manages the SolidWorks Task Pane tab that hosts <see cref="SettingsControl"/>.
    /// Uses real interop signature:
    ///   ISldWorks.CreateTaskpaneView3(object ImageList, string ToolTip) -> TaskpaneView
    ///   ITaskpaneView.DeleteView()
    /// All COM-hosting failures are non-fatal — the add-in still works without the Task Pane.
    /// </summary>
    internal sealed class TaskPaneManager : IDisposable
    {
        private readonly ISldWorks _swApp;
        private readonly Action<PresenceSettings> _onSettingsChanged;
        private ITaskpaneView _view;
        private SettingsControl _control;
        private bool _autoLoadWriteSupported = true;

        public TaskPaneManager(
            ISldWorks swApp,
            PresenceSettings initialSettings,
            Action<PresenceSettings> onSettingsChanged)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _onSettingsChanged = onSettingsChanged ?? throw new ArgumentNullException(nameof(onSettingsChanged));
            InitialSettings = initialSettings ?? new PresenceSettings();
        }

        public PresenceSettings InitialSettings { get; }

        /// <summary>Builds the Task Pane tab. Call from ConnectToSW (UI thread).</summary>
        public void Create()
        {
            ITaskpaneView view;
            try
            {
                // Real signature from D:\SOLIDWORKS Corp\... redists: CreateTaskpaneView3(object ImageList, string ToolTip)
                // Passing null ImageList gives no rail icon (non-critical).
                view = _swApp.CreateTaskpaneView3(null, "SOLIDWORKS Design");
            }
            catch
            {
                // Task pane not supported or API fluke — non-fatal
                return;
            }

            if (view == null) return;

            _view = view;

            string progId = ResolveProgId();
            if (string.IsNullOrEmpty(progId)) return;

            object ctrlObj;
            try { ctrlObj = _view.AddControl(progId, ""); }
            catch { return; }

            _control = TryUnwrapControl(ctrlObj) ?? new SettingsControl();
            _control.SettingsChangedFromUI += HandlePresenceSettingsChangedFromUI;
            _control.AutoLoadChangedFromUI += HandleAutoLoadChangedFromUI;

            _autoLoadWriteSupported = ProbeAutoLoadWritable();
            bool autoLoadOn = AddinRegistration.IsAutoLoadEnabled();

            try { _control.LoadFrom(InitialSettings, autoLoadOn, _autoLoadWriteSupported); }
            catch { /* ignore */ }
        }

        private static string ResolveProgId()
        {
            try
            {
                var attrs = typeof(SettingsControl).GetCustomAttributes(typeof(ProgIdAttribute), false);
                if (attrs.Length > 0 && attrs[0] is ProgIdAttribute prog) return prog.Value;
                return "SolidworksDiscordRPC.SettingsPane";
            }
            catch { return "SolidworksDiscordRPC.SettingsPane"; }
        }

        private static SettingsControl TryUnwrapControl(object ctrlObj)
        {
            if (ctrlObj == null) return null;
            if (ctrlObj is SettingsControl direct) return direct;

            try
            {
                var type = ctrlObj.GetType();
                foreach (var prop in type.GetProperties())
                {
                    try
                    {
                        if (prop.GetValue(ctrlObj, null) is SettingsControl inner) return inner;
                    }
                    catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }

            return null;
        }

        private void HandlePresenceSettingsChangedFromUI(PresenceSettings newSettings)
        {
            try { _onSettingsChanged?.Invoke(newSettings); }
            catch { /* must never crash SolidWorks */ }
        }

        private void HandleAutoLoadChangedFromUI(bool enabled)
        {
            bool ok = AddinRegistration.SetAutoLoadEnabled(enabled);
            _autoLoadWriteSupported = ok;

            if (!ok)
            {
                try
                {
                    bool actual = AddinRegistration.IsAutoLoadEnabled();
                    _control?.LoadFrom(
                        new PresenceSettings { PresenceEnabled = _control != null },
                        actual,
                        false);
                }
                catch { /* ignore */ }
            }
        }

        private static bool ProbeAutoLoadWritable()
        {
            try
            {
                bool current = AddinRegistration.IsAutoLoadEnabled();
                return AddinRegistration.SetAutoLoadEnabled(current); // non-destructive probe
            }
            catch { return false; }
        }

        public void Dispose()
        {
            try
            {
                if (_control != null)
                {
                    _control.SettingsChangedFromUI -= HandlePresenceSettingsChangedFromUI;
                    _control.AutoLoadChangedFromUI -= HandleAutoLoadChangedFromUI;
                    _control.Dispose();
                }
            }
            catch { /* ignore */ }

            _control = null;

            try
            {
                if (_view != null)
                {
                    // Real API: ITaskpaneView.DeleteView(), not ISldWorks.DeleteTaskpaneView2
                    _view.DeleteView();
                    Marshal.ReleaseComObject(_view);
                }
            }
            catch { /* ignore */ }

            _view = null;
        }
    }
}
