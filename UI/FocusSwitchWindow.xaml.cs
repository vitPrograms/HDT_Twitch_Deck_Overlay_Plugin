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

        // WinAPI для примусової активації вікна
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public FocusSwitchWindow()
        {
            InitializeComponent();

            // Активуємо вікно при створенні
            Loaded += (s, e) =>
            {
                Log.Info("FocusSwitchWindow loaded.");
                IntPtr windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (SetForegroundWindow(windowHandle))
                {
                    Log.Info("FocusSwitchWindow activated successfully.");
                }
                else
                {
                    Log.Warn("Failed to activate FocusSwitchWindow.");
                }

                // Налаштування таймера для закриття вікна через 10 мс
                _closeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(10) // Майже моментально
                };
                _closeTimer.Tick += (sender, err) =>
                {
                    _closeTimer.Stop();
                    Log.Info("Closing FocusSwitchWindow to return focus.");
                    try
                    {
                        Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to close FocusSwitchWindow: {ex.Message}");
                        Dispatcher.InvokeShutdown(); // Примусове завершення Dispatcher'а
                    }
                };
                _closeTimer.Start();
            };

            // Логування закриття
            Closed += (s, e) =>
            {
                Log.Info("FocusSwitchWindow successfully closed.");
            };
        }
    }
}