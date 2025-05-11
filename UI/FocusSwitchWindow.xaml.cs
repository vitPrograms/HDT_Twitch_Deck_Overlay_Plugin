using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace TwitchDeckOverlay.UI
{
    public partial class FocusSwitchWindow : Window
    {
        private DispatcherTimer _closeTimer;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public FocusSwitchWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                IntPtr windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (SetForegroundWindow(windowHandle))
                {
                    Log.Info("FocusSwitchWindow activated successfully.");
                }
                else
                {
                    Log.Warn("Failed to activate FocusSwitchWindow.");
                }

                _closeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(10)
                };
                _closeTimer.Tick += (sender, err) =>
                {
                    _closeTimer.Stop();
                    try
                    {
                        Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to close FocusSwitchWindow: {ex.Message}");
                        Dispatcher.InvokeShutdown();
                    }
                };
                _closeTimer.Start();
            };
        }
    }
}