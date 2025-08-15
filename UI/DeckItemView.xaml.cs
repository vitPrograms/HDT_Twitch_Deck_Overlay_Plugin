using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Utility.Logging;
using TwitchDeckOverlay.Models;

namespace TwitchDeckOverlay.UI
{
    public partial class DeckItemView : UserControl
    {
        private TwitchDeckManager _deckManager;
        private ImprovedOverlayView _parentOverlay;
        private bool _isActive = false;

        public DeckItemView()
        {
            InitializeComponent();
            Loaded += DeckItemView_Loaded;
        }

        public void SetDeckManager(TwitchDeckManager deckManager)
        {
            _deckManager = deckManager;
        }

        public void SetParentOverlay(ImprovedOverlayView parentOverlay)
        {
            _parentOverlay = parentOverlay;
        }

        private void DeckItemView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateActiveState();
            UpdateTimeDisplay();
        }

        private void UpdateTimeDisplay()
        {
            if (DataContext is DeckInfo deckInfo)
            {
                // Показуємо реальний час у форматі HH:mm
                string timeText = deckInfo.TimeAdded.ToString("HH:mm");
                TimeText.Text = timeText;
            }
        }

        public void UpdateActiveState()
        {
            if (_parentOverlay != null && DataContext is DeckInfo deckInfo)
            {
                _isActive = _parentOverlay.CurrentDeckDetails == deckInfo;
                UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            if (_isActive)
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); // Синій для активної
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x40, 0xA5, 0xFA));
                MainBorder.BorderThickness = new Thickness(2, 0, 0, 1);
            }
            else
            {
                MainBorder.Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
                MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
                MainBorder.BorderThickness = new Thickness(0, 0, 0, 1);
            }
        }

        private void DeckItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is DeckInfo deckInfo && _parentOverlay != null)
            {
                _parentOverlay.ToggleDeckDetails(deckInfo);
                Log.Info($"DeckItemView: Toggling details for {deckInfo.Author}");
            }
        }

        private void DeckItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isActive && sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
            }
        }

        private void DeckItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isActive)
            {
                UpdateVisualState();
            }
        }
    }

    // Extension method для пошуку батьківського контролу
    public static class ControlExtensions
    {
        public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            
            if (parent == null) return null;
            
            if (parent is T parentT)
                return parentT;
            
            return FindParent<T>(parent);
        }
    }
}