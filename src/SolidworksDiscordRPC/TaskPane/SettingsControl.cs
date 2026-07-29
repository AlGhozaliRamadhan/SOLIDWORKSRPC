using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SolidworksDiscordRPC.TaskPane
{
    // ReSharper disable once InconsistentNaming
    static class NativeMethods
    {
        internal const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }

    /// <summary>
    /// WinForms settings panel hosted inside the SolidWorks Task Pane.
    /// Must be COM-visible with a ProgId so SolidWorks can instantiate it via
    /// ITaskpaneView.AddControl(ProgId, "").
    /// </summary>
    [ComVisible(true)]
    [ProgId("SolidworksDiscordRPC.SettingsPane")]
    [Guid("B1A3F4C2-7E8A-4E6B-9D2C-4F6A8C3E9B02")]
    public partial class SettingsControl : UserControl
    {
        private CheckBox _chkEnabled;
        private CheckBox _chkHideFileName;
        private CheckBox _chkFeatureCount;
        private CheckBox _chkMaterial;
        private CheckBox _chkAutoLoad;
        private TextBox _txtProjectName;
        private Button _btnOpenFolder;
        private Label _lblStatus;

        private bool _suppressEvents;
        private bool _autoLoadSupported = true;

        public event Action<PresenceSettings> SettingsChangedFromUI;
        public event Action<bool> AutoLoadChangedFromUI;

        public SettingsControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = SystemColors.Window;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Padding = new Padding(10);
            Dock = DockStyle.Fill;
            AutoScroll = true;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(2),
                BackColor = Color.Transparent
            };
            flow.SuspendLayout();

            // Title
            var lblTitle = new Label
            {
                Text = "Discord Rich Presence",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            var lblSubtitle = new Label
            {
                Text = "Shows the active SOLIDWORKS Design document on Discord.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                MaximumSize = new Size(280, 0),
                Margin = new Padding(0, 0, 0, 12)
            };

            // Presence group
            var grpPresence = CreateGroupBox("Presence", out var grpPresenceFlow);

            _chkEnabled = CreateCheckBox("Enable Discord Rich Presence", "Turn the Discord status on/off without unloading the add-in.");
            _chkHideFileName = CreateCheckBox("Hide file name (privacy mode)", "Shows only \"Editing a Part/Assembly\" instead of the real file name.");
            _chkFeatureCount = CreateCheckBox("Show feature count", "Adds e.g. \"| 42 features\" to the status line when available.");
            _chkMaterial = CreateCheckBox("Show material name", "Adds material e.g. \"| 6061 Aluminum\" for parts when available.");

            grpPresenceFlow.Controls.Add(_chkEnabled);
            grpPresenceFlow.Controls.Add(_chkHideFileName);
            grpPresenceFlow.Controls.Add(_chkFeatureCount);
            grpPresenceFlow.Controls.Add(_chkMaterial);

            // Project name input
            var lblProjectName = new Label
            {
                Text = "Project name (optional):",
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 2)
            };
            var tipProjectName = new ToolTip();
            tipProjectName.SetToolTip(lblProjectName, "Shows a custom project label on Discord instead of just the file name, e.g. \"Formula SAE Chassis\".");

            _txtProjectName = new TextBox
            {
                MaxLength = 100,
                Width = 250,
                Margin = new Padding(0, 0, 0, 4)
            };
            _txtProjectName.HandleCreated += (s, e) =>
                NativeMethods.SendMessage(_txtProjectName.Handle, NativeMethods.EM_SETCUEBANNER, IntPtr.Zero, "e.g. Formula SAE Chassis");
            _txtProjectName.TextChanged += (s, e) => OnAnyPresenceCheckChanged();

            grpPresenceFlow.Controls.Add(lblProjectName);
            grpPresenceFlow.Controls.Add(_txtProjectName);

            // Startup group
            var grpStartup = CreateGroupBox("Startup", out var grpStartupFlow);

            _chkAutoLoad = CreateCheckBox("Load at SolidWorks startup", "Controls HKLM\\...\\AddInsStartup. Requires admin if changed here; otherwise toggle under Tools > Add-Ins.");
            grpStartupFlow.Controls.Add(_chkAutoLoad);

            // Bottom actions
            var pnlActions = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 12, 0, 0),
                MaximumSize = new Size(300, 0)
            };

            _btnOpenFolder = new Button
            {
                Text = "Open settings folder",
                AutoSize = true,
                FlatStyle = FlatStyle.System,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnOpenFolder.Click += (s, e) => PresenceSettings.OpenSettingsFolder();

            _lblStatus = new Label
            {
                Text = "",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                MaximumSize = new Size(280, 0),
                Margin = new Padding(0, 6, 0, 0)
            };

            pnlActions.Controls.Add(_btnOpenFolder);

            var lblFooter = new Label
            {
                Text = "Discord must be running with Activity Status enabled for the presence to appear.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                MaximumSize = new Size(280, 0),
                Margin = new Padding(0, 10, 0, 0)
            };

            flow.Controls.Add(lblTitle);
            flow.Controls.Add(lblSubtitle);
            flow.Controls.Add(grpPresence);
            flow.Controls.Add(grpStartup);
            flow.Controls.Add(pnlActions);
            flow.Controls.Add(_lblStatus);
            flow.Controls.Add(lblFooter);

            // Event wiring
            _chkEnabled.CheckedChanged += (s, e) => OnAnyPresenceCheckChanged();
            _chkHideFileName.CheckedChanged += (s, e) => OnAnyPresenceCheckChanged();
            _chkFeatureCount.CheckedChanged += (s, e) => OnAnyPresenceCheckChanged();
            _chkMaterial.CheckedChanged += (s, e) => OnAnyPresenceCheckChanged();
            _chkAutoLoad.CheckedChanged += (s, e) => OnAutoLoadCheckChanged();

            flow.ResumeLayout(false);
            flow.PerformLayout();

            Controls.Add(flow);

            ResumeLayout(false);
            PerformLayout();
        }

        private static GroupBox CreateGroupBox(string text, out FlowLayoutPanel innerFlow)
        {
            var group = new GroupBox
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(260, 0),
                MaximumSize = new Size(320, 0),
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(8, 6, 8, 8)
            };

            innerFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                MaximumSize = new Size(300, 0)
            };

            group.Controls.Add(innerFlow);
            return group;
        }

        private static CheckBox CreateCheckBox(string text, string tooltip)
        {
            var chk = new CheckBox
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(270, 0),
                Margin = new Padding(0, 3, 0, 3)
            };

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tip = new ToolTip();
                tip.SetToolTip(chk, tooltip);
            }

            return chk;
        }

        /// <summary>
        /// Populate UI from settings. Call from UI thread.
        /// </summary>
        public void LoadFrom(PresenceSettings settings, bool autoLoadEnabled, bool autoLoadSupported = true)
        {
            if (settings == null)
            {
                return;
            }

            _suppressEvents = true;
            try
            {
                _chkEnabled.Checked = settings.PresenceEnabled;
                _chkHideFileName.Checked = settings.HideFileName;
                _chkFeatureCount.Checked = settings.ShowFeatureCount;
                _chkMaterial.Checked = settings.ShowMaterial;
                _txtProjectName.Text = settings.CustomProjectName ?? "";

                _autoLoadSupported = autoLoadSupported;
                _chkAutoLoad.Checked = autoLoadEnabled;
                _chkAutoLoad.Enabled = autoLoadSupported;

                UpdateDependentEnabledState();
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void UpdateDependentEnabledState()
        {
            bool enabled = _chkEnabled.Checked;
            _chkHideFileName.Enabled = enabled;
            _chkFeatureCount.Enabled = enabled;
            _chkMaterial.Enabled = enabled;
            _txtProjectName.Enabled = enabled;
        }

        private void OnAnyPresenceCheckChanged()
        {
            if (_suppressEvents)
            {
                return;
            }

            UpdateDependentEnabledState();

            var settings = new PresenceSettings
            {
                PresenceEnabled = _chkEnabled.Checked,
                HideFileName = _chkHideFileName.Checked,
                ShowFeatureCount = _chkFeatureCount.Checked,
                ShowMaterial = _chkMaterial.Checked,
                CustomProjectName = _txtProjectName.Text?.Trim() ?? ""
            };

            _lblStatus.Text = settings.PresenceEnabled
                ? "Presence enabled. Changes save automatically."
                : "Presence disabled.";
            _lblStatus.ForeColor = settings.PresenceEnabled ? Color.FromArgb(0x1A, 0x7F, 0x37) : SystemColors.GrayText;

            SettingsChangedFromUI?.Invoke(settings);
        }

        private void OnAutoLoadCheckChanged()
        {
            if (_suppressEvents)
            {
                return;
            }

            bool enabled = _chkAutoLoad.Checked;
            AutoLoadChangedFromUI?.Invoke(enabled);

            if (!_autoLoadSupported)
            {
                _lblStatus.Text = "Auto-load toggle requires administrator privileges. Use Tools > Add-Ins > Start Up instead.";
                _lblStatus.ForeColor = Color.FromArgb(0xCF, 0x22, 0x2E);
                return;
            }

            _lblStatus.Text = enabled
                ? "Auto-load at startup is ON."
                : "Auto-load at startup is OFF. Enable in Tools > Add-Ins to load on next startup.";
            _lblStatus.ForeColor = SystemColors.GrayText;
        }
    }
}
