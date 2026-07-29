using System;
using System.Text;
using System.Threading;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidworksDiscordRPC
{
    /// <summary>
    /// Watches the active SolidWorks document and pushes updates to Discord.
    ///
    /// Implementation note: this uses lightweight polling (~2s) rather than hooking
    /// native SolidWorks document events (ActiveDocChangeNotify, DestroyNotify2, etc).
    /// Event delegate signatures differ per doc-type coclass and per SW API version,
    /// making them risky to get right without a live SW session to test against.
    /// Polling is straightforward and reliable. Event-driven refresh (near-instant
    /// instead of ~2s lag) is a natural follow-up.
    ///
    /// Phase 3: reads enriched info (feature count, material, dirty/rebuild) and
    /// respects PresenceSettings (hide filename, per-field toggles).
    /// </summary>
    internal sealed class DocumentTracker : IDisposable
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        private readonly ISldWorks _swApp;
        private readonly DiscordPresenceManager _presence;
        private PresenceSettings _settings;
        private readonly object _settingsLock = new object();
        private Timer _timer;

        private string _lastPath;
        private string _lastTitle;
        private int _lastDocType = -1;
        private bool _wasIdle = true;
        private string _lastMaterial;
        private int _lastFeatureCount;
        private bool _lastHasFeatureCount;

        public DocumentTracker(ISldWorks swApp, DiscordPresenceManager presence, PresenceSettings settings)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _settings = settings ?? new PresenceSettings();
        }

        // Back-compat ctor for any external callers still using 2-arg form
        public DocumentTracker(ISldWorks swApp, DiscordPresenceManager presence)
            : this(swApp, presence, new PresenceSettings())
        {
        }

        public void Start()
        {
            _timer = new Timer(_ => SafePoll(), null, TimeSpan.FromMilliseconds(500), PollInterval);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void UpdateSettings(PresenceSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            lock (_settingsLock)
            {
                _settings = settings;
            }

            // Force a presence refresh so Hide-FileName etc take effect immediately
            // without waiting for the next poll tick.
            ForceDirty();
        }

        /// <summary>
        /// Forces the next Poll() to push a new presence even if nothing changed,
        /// then triggers an immediate poll.
        /// </summary>
        public void RefreshNow()
        {
            ForceDirty();
            SafePoll();
        }

        private void ForceDirty()
        {
            _lastPath = null;
            _lastTitle = null;
            _lastDocType = -1;
            _wasIdle = false; // so idle -> non-idle transition isn't suppressed
        }

        private PresenceSettings CurrentSettings
        {
            get
            {
                lock (_settingsLock)
                {
                    // Return a snapshot; PresenceSettings is mutable but all fields are value types / strings
                    return new PresenceSettings
                    {
                        PresenceEnabled = _settings.PresenceEnabled,
                        HideFileName = _settings.HideFileName,
                        ShowFeatureCount = _settings.ShowFeatureCount,
                        ShowMaterial = _settings.ShowMaterial,
                        CustomProjectName = _settings.CustomProjectName
                    };
                }
            }
        }

        private void SafePoll()
        {
            try
            {
                Poll();
            }
            catch (Exception)
            {
                // Transient COM error (e.g. SW mid-rebuild) must never crash the timer
                // or take SolidWorks down.
            }
        }

        private void Poll()
        {
            var settings = CurrentSettings;
            if (!settings.PresenceEnabled)
            {
                return;
            }

            var activeDoc = _swApp.ActiveDoc as ModelDoc2;

            if (activeDoc == null)
            {
                SetIdleIfChanged(settings);
                return;
            }

            var info = DocumentInfoProvider.ReadEnriched(activeDoc);

            bool unchanged = !_wasIdle
                && info.PathName == _lastPath
                && info.Title == _lastTitle
                && info.DocType == _lastDocType
                && info.MaterialName == _lastMaterial
                && info.FeatureCount == _lastFeatureCount
                && info.HasFeatureCount == _lastHasFeatureCount;

            if (unchanged)
            {
                return;
            }

            _wasIdle = false;
            _lastPath = info.PathName;
            _lastTitle = info.Title;
            _lastDocType = info.DocType;
            _lastMaterial = info.MaterialName;
            _lastFeatureCount = info.FeatureCount;
            _lastHasFeatureCount = info.HasFeatureCount;

            // Compose Discord presence lines.
            var presence = BuildPresence(info, settings);
            _presence.SetPresence(presence.details, presence.state, presence.smallImageKey);
        }

        private struct PresenceLines
        {
            public string details;
            public string state;
            public string smallImageKey;
        }

        private PresenceLines BuildPresence(EnrichedDocInfo info, PresenceSettings settings)
        {
            string title = string.IsNullOrEmpty(info.Title) ? "Untitled document" : info.Title;
            string docTypeName = DocTypeName(info.DocType);
            string smallKey = DocTypeImageKey(info.DocType);

            bool hasProjectName = !string.IsNullOrWhiteSpace(settings.CustomProjectName);

            // Details line (top): project name > filename > doc type label
            string details;
            if (hasProjectName)
            {
                details = Truncate(settings.CustomProjectName.Trim(), 124);
            }
            else if (settings.HideFileName)
            {
                details = $"Editing {ArticleFor(docTypeName)} {docTypeName}";
            }
            else
            {
                details = Truncate(title, 124); // Discord Details limit is 128, leave margin
            }

            // State line: primary is "Editing a Part" plus optional enrichments.
            var stateBuilder = new StringBuilder();

            if (settings.HideFileName)
            {
                // When hiding filename we already used "Editing a Part" as Details,
                // so use enrichments or fall back.
                bool wroteSomething = false;

                if (settings.ShowFeatureCount && info.HasFeatureCount)
                {
                    stateBuilder.Append($"{info.FeatureCount} features");
                    wroteSomething = true;
                }

                if (settings.ShowMaterial && info.HasMaterial && !string.IsNullOrEmpty(info.MaterialName))
                {
                    if (wroteSomething)
                    {
                        stateBuilder.Append(" | ");
                    }
                    stateBuilder.Append(Truncate(info.MaterialName, 40));
                    wroteSomething = true;
                }

                // Intentionally leave state empty if no features are being tracked.
            }
            else
            {
                // Filename shown in Details, so State = "Editing a Part" + optional extras
                stateBuilder.Append($"Editing {ArticleFor(docTypeName)} {docTypeName}");

                // Append feature count / material as suffix if enabled, keeping under 128 chars
                var extras = new StringBuilder();
                if (settings.ShowFeatureCount && info.HasFeatureCount)
                {
                    extras.Append($" | {info.FeatureCount} features");
                }

                if (settings.ShowMaterial && info.HasMaterial && !string.IsNullOrEmpty(info.MaterialName))
                {
                    extras.Append($" | {Truncate(info.MaterialName, 30)}");
                }

                string suffix = extras.ToString();
                string combined = stateBuilder.ToString() + suffix;
                if (combined.Length > 124)
                {
                    // Truncate suffix to fit
                    int allowedSuffix = 124 - stateBuilder.Length;
                    if (allowedSuffix > 3)
                    {
                        stateBuilder.Append(suffix.Substring(0, allowedSuffix));
                    }
                }
                else
                {
                    stateBuilder.Append(suffix);
                }
            }

            string state = stateBuilder.ToString();
            if (string.IsNullOrEmpty(state))
            {
                state = $"Editing {ArticleFor(docTypeName)} {docTypeName}";
            }

            return new PresenceLines
            {
                details = details,
                state = Truncate(state, 124),
                smallImageKey = smallKey
            };
        }



        private static string ArticleFor(string noun)
        {
            if (string.IsNullOrEmpty(noun)) return "a";
            char c = char.ToUpperInvariant(noun[0]);
            return (c == 'A' || c == 'E' || c == 'I' || c == 'O' || c == 'U') ? "an" : "a";
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            if (s.Length <= maxLen)
            {
                return s;
            }

            return s.Substring(0, maxLen);
        }

        private void SetIdleIfChanged(PresenceSettings settings)
        {
            if (_wasIdle)
            {
                return;
            }

            _wasIdle = true;
            _lastPath = null;
            _lastTitle = null;
            _lastDocType = -1;
            _lastMaterial = null;
            _lastFeatureCount = 0;
            _lastHasFeatureCount = false;

            _presence.SetPresence("No document open", "");
        }

        private static string DocTypeName(int swDocType)
        {
            switch (swDocType)
            {
                case (int)swDocumentTypes_e.swDocPART: return "Part";
                case (int)swDocumentTypes_e.swDocASSEMBLY: return "Assembly";
                case (int)swDocumentTypes_e.swDocDRAWING: return "Drawing";
                default: return "document";
            }
        }

        private static string DocTypeImageKey(int swDocType)
        {
            // Only renders once matching art asset is uploaded under the Discord app.
            // Harmless no-op until then.
            switch (swDocType)
            {
                case (int)swDocumentTypes_e.swDocPART: return "part_icon";
                case (int)swDocumentTypes_e.swDocASSEMBLY: return "assembly_icon";
                case (int)swDocumentTypes_e.swDocDRAWING: return "drawing_icon";
                default: return null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
