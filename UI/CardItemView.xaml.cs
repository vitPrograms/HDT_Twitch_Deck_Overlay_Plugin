using System.Linq;
using System.Windows.Controls;
using HearthDb;
using Hearthstone_Deck_Tracker.Controls.Tooltips;
using Hearthstone_Deck_Tracker.Hearthstone;
using TwitchDeckOverlay.Models;

namespace TwitchDeckOverlay.UI
{
    public partial class CardItemView : UserControl, ICardTooltip
    {
        public CardItemView()
        {
            InitializeComponent();
        }

        public void UpdateTooltip(CardTooltipViewModel viewModel)
        {
            if (DataContext is CardInfo cardInfo)
            {
                // Конвертуємо CardInfo в HDT Card для tooltip
                var dbCard = Cards.All.Values.FirstOrDefault(c => c.DbfId == cardInfo.Id);
                if (dbCard != null)
                {
                    var hdtCard = new Hearthstone_Deck_Tracker.Hearthstone.Card(dbCard)
                    {
                        Count = cardInfo.Count
                    };
                    
                    viewModel.Card = hdtCard;
                }
            }
        }
    }
}