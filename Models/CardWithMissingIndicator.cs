using System.Windows;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Hearthstone;
using TwitchDeckOverlay.Models;

namespace TwitchDeckOverlay.Models
{
    public class CardWithMissingIndicator : Card
    {
        public bool IsMissingInCollection { get; set; }
        
        public CardWithMissingIndicator(HearthDb.Card card) : base(card)
        {
        }

        public new DrawingBrush Background
        {
            get
            {
                var originalBackground = base.Background;
                
                if (!IsMissingInCollection)
                    return originalBackground;

                // Створюємо композитне зображення з синім overlay як в Hearthstone
                var drawingGroup = new DrawingGroup();
                
                // Додаємо оригінальне зображення карти
                if (originalBackground?.Drawing != null)
                {
                    drawingGroup.Children.Add(originalBackground.Drawing);
                }
                
                // Додаємо тонкий сірий overlay для приглушення карти
                var overlayGeometry = new RectangleGeometry(new Rect(0, 0, 217, 34));
                var overlayBrush = new SolidColorBrush(Color.FromArgb(100, 32, 32, 32));
                var overlayDrawing = new GeometryDrawing(overlayBrush, null, overlayGeometry);
                drawingGroup.Children.Add(overlayDrawing);
                
                var compositeBrush = new DrawingBrush(drawingGroup);
                if (compositeBrush.CanFreeze)
                    compositeBrush.Freeze();
                    
                return compositeBrush;
            }
        }
    }
}