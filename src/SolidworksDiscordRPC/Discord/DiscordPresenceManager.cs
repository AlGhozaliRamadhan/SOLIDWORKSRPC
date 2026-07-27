using System;
using DiscordRPC;

namespace SolidworksDiscordRPC
{
    /// <summary>
    /// Thin wrapper around <see cref="DiscordRpcClient"/>.
    /// Owns the IPC connection to Discord and exposes SetPresence / Clear.
    /// Safe to call from any thread; never throws into caller — Discord IPC
    /// errors must never affect SolidWorks.
    /// </summary>
    internal sealed class DiscordPresenceManager : IDisposable
    {
        private const string DiscordApplicationId = "1531258607457271939";

        private DiscordRpcClient _client;
        private readonly DateTime _sessionStart = DateTime.UtcNow;
        private bool _disposed;

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (_disposed || IsInitialized)
            {
                return;
            }

            try
            {
                _client = new DiscordRpcClient(DiscordApplicationId)
                {
                    // No logger — ConsoleLogger can trigger unwanted console/Chromium
                    // windows when running as a SolidWorks COM add-in (no console host).
                };

                _client.OnReady += (sender, e) =>
                {
                    // Fires once Discord's IPC handshake completes.
                };

                _client.OnError += (sender, e) =>
                {
                    // Never let a Discord IPC error take SolidWorks down.
                };

                _client.Initialize();
                IsInitialized = true;

                // Show something immediately; DocumentTracker's first poll overwrites this.
                SetPresence("No document open", "Idle — SOLIDWORKS Design");
            }
            catch (Exception)
            {
                // Discord not installed/running or IPC unavailable — fail silently.
                IsInitialized = false;
            }
        }

        /// <summary>
        /// Sets the Discord presence. No-op when not initialized or disposed.
        /// </summary>
        public void SetPresence(string details, string state, string smallImageKey = null)
        {
            if (_disposed || !IsInitialized || _client == null || _client.IsDisposed)
            {
                return;
            }

            try
            {
                var assets = new Assets
                {
                    LargeImageKey = "sw_logo",
                    LargeImageText = "SOLIDWORKS Design"
                };

                if (!string.IsNullOrEmpty(smallImageKey))
                {
                    assets.SmallImageKey = smallImageKey;
                }

                _client.SetPresence(new RichPresence
                {
                    Details = details,
                    State = state,
                    Timestamps = new Timestamps { Start = _sessionStart },
                    Assets = assets
                });
            }
            catch (Exception)
            {
                // Bad presence update must never crash SW.
            }
        }

        public void ClearPresence()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _client?.ClearPresence();
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ClearPresence();

            try
            {
                _client?.Dispose();
            }
            catch
            {
                // ignore
            }

            _client = null;
            IsInitialized = false;
        }
    }
}
