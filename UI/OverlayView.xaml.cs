using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using TwitchDeckOverlay.Models;
using Hearthstone_Deck_Tracker.Utility.Logging;
using TwitchDeckOverlay.Config;
using System.Collections.Generic;

namespace TwitchDeckOverlay.UI
{
    public partial class OverlayView : UserControl
    {
        private readonly TwitchDeckManager _deckManager;
        private bool _isDragging;
        private Point _dragStartPoint;
        private bool _isCollapsed;
        private DispatcherTimer _notificationTimer;

        public OverlayView(TwitchDeckManager deckManager)
        {
            InitializeComponent();
            OverlayExtensions.SetIsOverlayHitTestVisible(this, true);

            _deckManager = deckManager;
            DataContext = _deckManager;

            // Відновлення позиції з конфігурації
            Canvas.SetLeft(this, PluginConfig.Instance.OverlayWindowLeft);
            Canvas.SetTop(this, PluginConfig.Instance.OverlayWindowTop);
            Log.Info($"Restored OverlayView position: Left={PluginConfig.Instance.OverlayWindowLeft}, Top={PluginConfig.Instance.OverlayWindowTop}");

            _isCollapsed = false;
            _notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.5)
            };
            _notificationTimer.Tick += NotificationTimer_Tick;

            Log.Info("TwitchOverlayView initialized");
            _deckManager.Decks.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    ShowNewDeckNotification();
                }
            };
        }

        private void ShowNewDeckNotification()
        {
            NewDeckIndicator.Visibility = Visibility.Visible;
            NewDeckIndicator.Fill = new SolidColorBrush(Colors.Red);
            _notificationTimer.Start();
        }

        private void NotificationTimer_Tick(object sender, EventArgs e)
        {
            if (NewDeckIndicator.Fill == new SolidColorBrush(Colors.Red))
                NewDeckIndicator.Fill = new SolidColorBrush(Colors.Transparent);
            else
                NewDeckIndicator.Fill = new SolidColorBrush(Colors.Red);
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isCollapsed = !_isCollapsed;
            if (_isCollapsed)
            {
                DeckListScrollViewer.Visibility = Visibility.Collapsed;
                ToggleButton.Content = "+";
                Opacity = 0.7;
            }
            else
            {
                DeckListScrollViewer.Visibility = Visibility.Visible;
                ToggleButton.Content = "−";
                Opacity = 1.0;
                NewDeckIndicator.Visibility = Visibility.Hidden;
                _notificationTimer.Stop();
            }
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isCollapsed)
            {
                DeckListScrollViewer.Visibility = Visibility.Visible;
                ToggleButton.Content = "−";
                _isCollapsed = false;
                NewDeckIndicator.Visibility = Visibility.Hidden;
                _notificationTimer.Stop();
            }
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Handled by XAML triggers for opacity
        }

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(Hearthstone_Deck_Tracker.API.Core.OverlayCanvas);
                DragHandle.CaptureMouse();
                e.Handled = true;
            }
        }

        private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                DragHandle.ReleaseMouseCapture();

                // Зберігаємо позицію після перетягування
                PluginConfig.Instance.OverlayWindowLeft = Canvas.GetLeft(this);
                PluginConfig.Instance.OverlayWindowTop = Canvas.GetTop(this);
                PluginConfig.Save();
                Log.Info($"Saved OverlayView position: Left={PluginConfig.Instance.OverlayWindowLeft}, Top={PluginConfig.Instance.OverlayWindowTop}");

                e.Handled = true;
            }
        }

        private void DragHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPosition = e.GetPosition(Hearthstone_Deck_Tracker.API.Core.OverlayCanvas);
                double deltaX = currentPosition.X - _dragStartPoint.X;
                double deltaY = currentPosition.Y - _dragStartPoint.Y;

                double newLeft = Canvas.GetLeft(this) + deltaX;
                double newTop = Canvas.GetTop(this) + deltaY;

                Canvas.SetLeft(this, newLeft);
                Canvas.SetTop(this, newTop);

                _dragStartPoint = currentPosition;
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Collapsed;
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
        }

        private void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
        }

        private void Expander_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void CopyDeckCode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DeckInfo deck)
            {
                try
                {
                    // Копіюємо код колоди в буфер обміну
                    Clipboard.SetText(deck.DeckCode);
                    Log.Info($"Deck code copied: {deck.DeckCode}");

                    // Змінюємо колір рамки кнопки для візуального фідбеку
                    var originalBorderBrush = button.BorderBrush;
                    button.BorderBrush = new SolidColorBrush(Colors.LimeGreen);

                    // Показуємо вікно фокусу, якщо увімкнено
                    if (PluginConfig.Instance.ShowFocusWindowOnCopyEnabled)
                    {
                        try
                        {
                            var focusWindow = new FocusSwitchWindow();
                            focusWindow.Show();
                            Log.Info("FocusSwitchWindow created and shown.");
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Failed to open FocusSwitchWindow: {ex.Message}");
                        }
                    }

                    // Повертаємо оригінальний колір рамки через 1 секунду
                    Task.Delay(1000).ContinueWith(_ => Dispatcher.Invoke(() => button.BorderBrush = originalBorderBrush));
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to copy deck code: {ex.Message}");
                }
            }
        }

        private void RemoveDeck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is DeckInfo deck)
            {
                _deckManager.Decks.Remove(deck);
                Log.Info($"Removed deck from collection. Current deck count: {_deckManager.Decks.Count}");
            }
        }

        private async void ClassRow_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is DeckInfo deck && !string.IsNullOrEmpty(deck.HeroPowerImage))
            {
                var popup = grid.FindName("HeroPowerPopup") as Popup;
                if (popup != null)
                {
                    await Task.Delay(100); // Затримка 100 мс
                    if (grid.IsMouseOver) // Перевіряємо, чи ми все ще над елементом
                    {
                        var windowPosition = this.PointToScreen(new Point(0, 0));
                        var screenWidth = SystemParameters.PrimaryScreenWidth;
                        var windowRightEdge = windowPosition.X + this.ActualWidth;
                        if (windowRightEdge > screenWidth - 250)
                        {
                            popup.Placement = PlacementMode.Left;
                            popup.HorizontalOffset = -10;
                        }
                        else
                        {
                            popup.Placement = PlacementMode.Right;
                            popup.HorizontalOffset = 10;
                        }
                        popup.IsOpen = true;
                        var mousePos = e.GetPosition(grid);
                    }
                }
            }
        }

        private void ClassRow_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is DeckInfo deck)
            {
                var popup = grid.FindName("HeroPowerPopup") as Popup;
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }
        }

        private void HeroPowerPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.FindAncestor<Popup>() is Popup popup && popup.DataContext is DeckInfo deck)
            {
                popup.IsOpen = false;
            }
        }

        private async void CardRow_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                var grid = border.FindVisualChild<Grid>(child => child.Name == "CardRowGrid");
                if (grid != null)
                {
                    var popup = grid.FindName("CardImagePopup") as Popup;
                    if (popup != null)
                    {
                        await Task.Delay(100); // Затримка 100 мс
                        if (border.IsMouseOver) // Перевіряємо, чи ми все ще над елементом
                        {
                            var windowPosition = this.PointToScreen(new Point(0, 0));
                            var screenWidth = SystemParameters.PrimaryScreenWidth;
                            var windowRightEdge = windowPosition.X + this.ActualWidth;
                            if (windowRightEdge > screenWidth - 250)
                            {
                                popup.Placement = PlacementMode.Left;
                                popup.HorizontalOffset = -10;
                            }
                            else
                            {
                                popup.Placement = PlacementMode.Right;
                                popup.HorizontalOffset = 10;
                            }
                            popup.IsOpen = true;
                            var mousePos = e.GetPosition(grid);
                        }
                    }
                }
            }
        }

        private void CardRow_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                var grid = border.FindVisualChild<Grid>(child => child.Name == "CardRowGrid");
                if (grid != null)
                {
                    var popup = grid.FindName("CardImagePopup") as Popup;
                    if (popup != null)
                    {
                        popup.IsOpen = false;
                        var card = grid.DataContext as CardInfo;
                    }
                }
            }
        }

        private void CardImagePopup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.FindAncestor<Popup>() is Popup popup && popup.DataContext is CardInfo card)
            {
                popup.IsOpen = false;
            }
        }

        // Допоміжний метод для пошуку всіх елементів типу T у дереві
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        private void DeckListScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                double scrollAmount = e.Delta > 0 ? -30 : 30; // 30 пікселів за рух колеса
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);
                e.Handled = true; // Позначимо подію як оброблену
            }
        }
    }

    public static class VisualTreeExtensions
    {
        public static T FindVisualChild<T>(this DependencyObject depObj, Func<T, bool> predicate = null) where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t && (predicate == null || predicate(t)))
                {
                    return t;
                }

                var result = FindVisualChild<T>(child, predicate);
                if (result != null) return result;
            }
            return null;
        }

        public static T FindAncestor<T>(this DependencyObject depObj) where T : DependencyObject
        {
            var current = depObj;
            while (current != null)
            {
                if (current is T ancestor) return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}