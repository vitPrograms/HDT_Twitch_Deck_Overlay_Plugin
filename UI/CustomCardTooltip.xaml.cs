using System.Windows.Controls.Primitives;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace TwitchDeckOverlay.UI
{
    public partial class CustomCardTooltip : Popup
    {
        public CustomCardTooltip()
        {
            InitializeComponent();
        }

        public void ShowForCard(Card card)
        {
            DataContext = card;
            IsOpen = true;
        }

        public void Hide()
        {
            IsOpen = false;
        }
    }
}