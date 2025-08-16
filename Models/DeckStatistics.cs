using System.Collections.Generic;
using System.Linq;

namespace TwitchDeckOverlay.Models
{
    public class DeckStatistics
    {
        public int CommonCards { get; set; }
        public int RareCards { get; set; }
        public int EpicCards { get; set; }
        public int LegendaryCards { get; set; }
        
        public int Minions { get; set; }
        public int Spells { get; set; }
        public int Weapons { get; set; }
        
        public double AverageManaCost { get; set; }
        public Dictionary<int, int> ManaCurve { get; set; } = new Dictionary<int, int>();
        
        public int TotalCards => CommonCards + RareCards + EpicCards + LegendaryCards;
        
        public void CalculateFromCards(List<CardInfo> cards)
        {
            if (cards == null || !cards.Any()) return;
            
            // Скидаємо статистику
            CommonCards = RareCards = EpicCards = LegendaryCards = 0;
            Minions = Spells = Weapons = 0;
            ManaCurve.Clear();
            
            int totalManaCost = 0;
            int totalCardCount = 0;
            
            foreach (var card in cards)
            {
                // Підрахунок за рідкістю (1=Common, 2=Free, 3=Rare, 4=Epic, 5=Legendary)
                switch (card.RarityId)
                {
                    case 1: // Common
                    case 2: // Free (Basic)
                        CommonCards += card.Count;
                        break;
                    case 3: // Rare
                        RareCards += card.Count;
                        break;
                    case 4: // Epic
                        EpicCards += card.Count;
                        break;
                    case 5: // Legendary
                        LegendaryCards += card.Count;
                        break;
                }
                
                // Мана-крива
                int manaCost = card.Cost > 7 ? 7 : card.Cost; // 7+ об'єднуємо
                if (!ManaCurve.ContainsKey(manaCost))
                    ManaCurve[manaCost] = 0;
                ManaCurve[manaCost] += card.Count;
                
                // Середня мана
                totalManaCost += card.Cost * card.Count;
                totalCardCount += card.Count;
            }
            
            // Середня вартість мани
            AverageManaCost = totalCardCount > 0 ? (double)totalManaCost / totalCardCount : 0;
        }
    }
}