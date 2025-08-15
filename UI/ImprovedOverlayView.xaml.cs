using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.API;
using TwitchDeckOverlay.Models;
using TwitchDeckOverlay.Config;

namespace TwitchDeckOverlay.UI
{
    public partial class ImprovedOverlayView : UserControl
    {
        private TwitchDeckManager _deckManager;
        private DeckInfo _currentDeckDetails;
        
        private bool _isDragging;
        private Point _dragStartPoint;
        private bool _isCollapsed = false;
        private Dictionary<int, int> _collectionCache;
        private DateTime _lastCollectionUpdate = DateTime.MinValue;
        private readonly TimeSpan _collectionCacheExpiry = TimeSpan.FromMinutes(10);
        
        public DeckInfo CurrentDeckDetails => _currentDeckDetails;

        public ImprovedOverlayView(TwitchDeckManager deckManager)
        {
            InitializeComponent();
            OverlayExtensions.SetIsOverlayHitTestVisible(this, true);
            _deckManager = deckManager;
            DataContext = deckManager;
            
            _ = InitializeCollectionCacheAsync();
        }



        // Методи для перетягування
        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border || e.OriginalSource is TextBlock)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(Core.OverlayCanvas);
                ((UIElement)sender).CaptureMouse();
                e.Handled = true;
            }
        }

        private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ((UIElement)sender).ReleaseMouseCapture();

                // Зберігаємо позицію після перетягування
                var config = PluginConfig.Instance;
                config.OverlayWindowLeft = Canvas.GetLeft(this);
                config.OverlayWindowTop = Canvas.GetTop(this);
                PluginConfig.Save();

                e.Handled = true;
            }
        }

        private void DragHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPosition = e.GetPosition(Core.OverlayCanvas);
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
        
        private void Header_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isCollapsed)
            {
                ToggleCollapse();
            }
        }

        private async Task InitializeCollectionCacheAsync()
        {
            try
            {
                await Task.Run(() =>
                {

                    UpdateCollectionCache();

                });
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error initializing collection cache: {ex.Message}");
            }
        }

        public void ToggleDeckDetails(DeckInfo deckInfo)
        {
            try
            {

                
                // Якщо це та сама колода і панель відкрита - закриваємо
                if (_currentDeckDetails == deckInfo && DetailsPanel.Visibility == Visibility.Visible)
                {
                    DetailsPanel.Visibility = Visibility.Collapsed;
                    CardListColumn.Visibility = Visibility.Collapsed;
                    _currentDeckDetails = null;
                    UpdateAllDeckItemsVisualState();

                    return;
                }
                
                _currentDeckDetails = deckInfo;
                
                // Швидко показуємо базову інформацію
                if (_collectionCache != null)
                {
                    // Якщо колекція вже завантажена - швидко підраховуємо
                    CalculateDustCosts(deckInfo);
                }
                else
                {
                    // Якщо колекція ще не завантажена - показуємо повідомлення
                    DetailsTotalDustCost.Text = "Loading...";
                    DetailsDustNeeded.Text = "Loading...";
                    
                    // Асинхронно завантажуємо колекцію і підраховуємо
                    _ = CalculateDustCostsAsync(deckInfo);
                }
                
                // Оновлюємо інформацію в панелі деталей
                DetailsTitle.Text = $"Deck: {deckInfo.Author}";
                
                // Встановлюємо іконку класу (використовуємо правильні іконки класів замість арту героя)
                try
                {
                    var classIcon = ImageCache.GetClassIcon(deckInfo.Class);
                    if (classIcon != null)
                    {
                        DetailsClassIcon.Source = classIcon;
                        DetailsClassIconText.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        DetailsClassIcon.Source = null;
                        DetailsClassIconText.Text = deckInfo.Class.Substring(0, 1).ToUpper();
                        DetailsClassIconText.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"ImprovedOverlayView: Could not load class icon for {deckInfo.Class}: {ex.Message}");
                    DetailsClassIcon.Source = null;
                    DetailsClassIconText.Text = deckInfo.Class.Substring(0, 1).ToUpper();
                    DetailsClassIconText.Visibility = Visibility.Visible;
                }
                
                DetailsClass.Text = deckInfo.Class;
                DetailsMode.Text = deckInfo.Mode;
                DetailsCardCount.Text = deckInfo.Cards.Count.ToString();
                // Значення будуть оновлені асинхронно в CalculateDustCostsAsync
                
                // Сортуємо карти по мані, потім по назві
                var sortedCards = deckInfo.Cards.OrderBy(c => c.Cost).ThenBy(c => c.Name).ToList();
                
                // Конвертуємо CardInfo в HDT Card для правильного відображення
                var hdtCards = ConvertToHdtCards(sortedCards);
                
                // Показуємо карти у вбудованому списку
                DetailsCardList.ItemsSource = hdtCards;
                CardListColumn.Visibility = Visibility.Visible;
                

                
                // Показуємо панель деталей
                DetailsPanel.Visibility = Visibility.Visible;
                
                // Позначаємо колоду як переглянуту
                deckInfo.IsNew = false;
                
                // Оновлюємо візуальний стан всіх DeckItemView
                UpdateAllDeckItemsVisualState();
                

            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error in ToggleDeckDetails: {ex.Message}");
            }
        }

        private List<Hearthstone_Deck_Tracker.Hearthstone.Card> ConvertToHdtCards(List<CardInfo> cardInfos)
        {
            var hdtCards = new List<Hearthstone_Deck_Tracker.Hearthstone.Card>();
            
            foreach (var cardInfo in cardInfos)
            {
                try
                {
                    // Знаходимо карту в HearthDb за ID
                    var dbCard = HearthDb.Cards.All.Values.FirstOrDefault(c => c.DbfId == cardInfo.Id);
                    if (dbCard != null)
                    {
                        var hdtCard = new Models.CardWithMissingIndicator(dbCard)
                        {
                            Count = cardInfo.Count,
                            IsMissingInCollection = cardInfo.IsMissingInCollection
                        };
                        hdtCards.Add(hdtCard);
                    }
                    else
                    {
                        Log.Warn($"ImprovedOverlayView: Could not find HDT card for ID {cardInfo.Id} ({cardInfo.Name})");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"ImprovedOverlayView: Error converting card {cardInfo.Name}: {ex.Message}");
                }
            }
            
            return hdtCards;
        }

        private void UpdateAllDeckItemsVisualState()
        {
            // Знаходимо всі DeckItemView і оновлюємо їх візуальний стан
            foreach (var item in DeckItemsControl.Items)
            {
                var container = DeckItemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                var deckItemView = FindVisualChild<DeckItemView>(container);
                deckItemView?.UpdateActiveState();
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childResult = FindVisualChild<T>(child);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }



        private void CalculateDustCosts(DeckInfo deckInfo)
        {
            try
            {
                if (_collectionCache == null || !_collectionCache.Any())
                {
                    // Якщо колекція недоступна - показуємо тільки загальну вартість
                    int totalDustCost = 0;
                    foreach (var cardInfo in deckInfo.Cards)
                    {
                        int dustCost = GetCraftingCost(cardInfo.RarityId, cardInfo.CardSetId);
                        totalDustCost += dustCost * cardInfo.Count;
                        cardInfo.IsMissingInCollection = false; // Не можемо визначити
                    }
                    
                    deckInfo.TotalDustCost = totalDustCost;
                    deckInfo.DustNeeded = 0;
                    
                    // Оновлюємо UI
                    Dispatcher.Invoke(() =>
                    {
                        DetailsTotalDustCost.Text = totalDustCost.ToString();
                        DetailsDustNeeded.Text = "N/A";
                    });
                    return;
                }

                int dustNeeded = 0;
                int totalDustCostCalculated = 0;

                Log.Info($"=== DUST CALCULATION TEST FOR DECK: {deckInfo.Author} ===");
                Log.Info($"Deck Class: {deckInfo.Class}, Mode: {deckInfo.Mode}");
                Log.Info($"Total cards in deck: {deckInfo.Cards.Count}");
                
                foreach (var cardInfo in deckInfo.Cards)
                {
                    int dbfId = cardInfo.Id;
                    int dustCost = GetCraftingCost(cardInfo.RarityId, cardInfo.CardSetId);
                    int cardTotalCost = dustCost * cardInfo.Count;
                    totalDustCostCalculated += cardTotalCost;

                    // Детальне логування для кожної карти
                    Log.Info($"Card: {cardInfo.Name} (ID: {dbfId})");
                    Log.Info($"  Rarity: {cardInfo.RarityId}, Set: {cardInfo.CardSetId}, Count: {cardInfo.Count}");
                    Log.Info($"  Dust per card: {dustCost}, Total for {cardInfo.Count}x: {cardTotalCost}");

                    if (!_collectionCache.TryGetValue(dbfId, out int ownedCount) || ownedCount < cardInfo.Count)
                    {
                        cardInfo.IsMissingInCollection = true;
                        int missingCount = cardInfo.Count - (ownedCount > 0 ? ownedCount : 0);
                        int cardDustNeeded = dustCost * missingCount;
                        dustNeeded += cardDustNeeded;
                        
                        Log.Info($"  MISSING: Owned {ownedCount}, Need {cardInfo.Count}, Missing {missingCount}");
                        Log.Info($"  Dust needed for this card: {cardDustNeeded}");
                    }
                    else
                    {
                        cardInfo.IsMissingInCollection = false;
                        Log.Info($"  OWNED: Have {ownedCount}, need {cardInfo.Count} - OK");
                    }

                    // Обробляємо компоненти карт (якщо є)
                    if (cardInfo.HasComponents)
                    {
                        Log.Info($"  Card has {cardInfo.Components.Count} components:");
                        
                        if (!cardInfo.IsMissingInCollection)
                        {
                            foreach (var component in cardInfo.Components)
                            {
                                int componentDustCost = GetCraftingCost(component.RarityId, component.CardSetId);
                                int componentTotalCost = componentDustCost * component.Count;
                                totalDustCostCalculated += componentTotalCost;
                                
                                Log.Info($"    Component: {component.Name} - {componentDustCost} dust x{component.Count} = {componentTotalCost}");
                            }
                            continue;
                        }

                        foreach (var component in cardInfo.Components)
                        {
                            int componentDustCost = GetCraftingCost(component.RarityId, component.CardSetId);
                            int componentTotalCost = componentDustCost * component.Count;
                            totalDustCostCalculated += componentTotalCost;

                            int componentDbfId = component.Id;
                            int componentOwnedCount = 0;
                            bool isMissing = !_collectionCache.TryGetValue(componentDbfId, out componentOwnedCount) || componentOwnedCount < component.Count;
                            
                            Log.Info($"    Component: {component.Name} (ID: {componentDbfId})");
                            Log.Info($"      Dust per card: {componentDustCost}, Total: {componentTotalCost}");
                            
                            if (isMissing)
                            {
                                int missingCount = component.Count - componentOwnedCount;
                                int componentDustNeeded = componentDustCost * missingCount;
                                dustNeeded += componentDustNeeded;
                                
                                Log.Info($"      MISSING: Owned {componentOwnedCount}, Need {component.Count}, Missing {missingCount}");
                                Log.Info($"      Dust needed: {componentDustNeeded}");
                            }
                            else
                            {
                                Log.Info($"      OWNED: Have {componentOwnedCount}, need {component.Count} - OK");
                            }
                        }
                    }
                    
                    Log.Info($"  Running total dust cost: {totalDustCostCalculated}");
                    Log.Info($"  Running dust needed: {dustNeeded}");
                    Log.Info("---");
                }

                deckInfo.DustNeeded = dustNeeded;
                deckInfo.TotalDustCost = totalDustCostCalculated;

                // Підсумкове логування
                Log.Info("=== DUST CALCULATION SUMMARY ===");
                Log.Info($"TOTAL DUST COST: {totalDustCostCalculated}");
                Log.Info($"DUST NEEDED: {dustNeeded}");
                Log.Info($"Expected total dust cost: 6680 (for comparison)");
                Log.Info($"Difference: {totalDustCostCalculated - 6680}");
                Log.Info("=== END SUMMARY ===");

                // Оновлюємо UI в головному потоці
                Dispatcher.Invoke(() =>
                {
                    DetailsTotalDustCost.Text = totalDustCostCalculated.ToString();
                    DetailsDustNeeded.Text = dustNeeded.ToString();
                });


            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error calculating dust costs: {ex.Message}");
                
                // Fallback - показуємо тільки загальну вартість
                int fallbackTotal = 0;
                foreach (var card in deckInfo.Cards)
                {
                    card.IsMissingInCollection = false;
                    fallbackTotal += GetCraftingCost(card.RarityId, card.CardSetId) * card.Count;
                }
                
                deckInfo.TotalDustCost = fallbackTotal;
                deckInfo.DustNeeded = 0;
                
                Dispatcher.Invoke(() =>
                {
                    DetailsTotalDustCost.Text = fallbackTotal.ToString();
                    DetailsDustNeeded.Text = "Error";
                });
            }
        }

        private async Task CalculateDustCostsAsync(DeckInfo deckInfo)
        {
            try
            {
                // Виконуємо важкий підрахунок у фоновому потоці
                await Task.Run(() =>
                {
                    CalculateDustCosts(deckInfo);
                });

                // Після завершення обчислень оновлюємо UI, тільки якщо ще показуємо ті ж деталі
                if (_currentDeckDetails == deckInfo && DetailsPanel.Visibility == Visibility.Visible)
                {
                    DetailsTotalDustCost.Text = deckInfo.TotalDustCost.ToString();
                    DetailsDustNeeded.Text = deckInfo.DustNeeded.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error in CalculateDustCostsAsync: {ex.Message}");
                if (_currentDeckDetails == deckInfo && DetailsPanel.Visibility == Visibility.Visible)
                {
                    DetailsTotalDustCost.Text = "-";
                    DetailsDustNeeded.Text = "-";
                }
            }
        }

        private void UpdateCollectionCache()
        {
            var now = DateTime.UtcNow;
            if (now - _lastCollectionUpdate < _collectionCacheExpiry && _collectionCache != null)
            {
                return; // Кеш ще актуальний
            }

            try
            {
                Log.Debug("ImprovedOverlayView: Fetching collection from HearthMirror...");
                var collection = HearthMirror.Reflection.Client.GetCollection();
                if (collection == null || !collection.Any())
                {
                    Log.Warn("ImprovedOverlayView: Could not retrieve user's collection");
                    return;
                }

                _collectionCache = new Dictionary<int, int>();
                foreach (HearthMirror.Objects.Card collectedCard in collection)
                {
                    var dbCard = Hearthstone_Deck_Tracker.Hearthstone.Database.GetCardFromId(collectedCard.Id);
                    if (dbCard != null && dbCard.DbfId != 0)
                    {
                        if (_collectionCache.ContainsKey(dbCard.DbfId))
                        {
                            _collectionCache[dbCard.DbfId] += collectedCard.Count;
                        }
                        else
                        {
                            _collectionCache[dbCard.DbfId] = collectedCard.Count;
                        }
                    }
                }

                _lastCollectionUpdate = now;

            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error updating collection cache: {ex.Message}");
                _collectionCache = null; // Очищуємо кеш при помилці
            }
        }

        private bool IsCoreCard(int cardSetId)
        {
            // Core set IDs (безкоштовні карти)
            // 2 = Basic, 1637 = Core, 1646 = Legacy, 1905 = Whizbang's Workshop (деякі безкоштовні)
            var coreSetIds = new[] { 2, 1637, 1646 };
            bool isCore = coreSetIds.Contains(cardSetId);
            
            if (isCore)
            {
                Log.Debug($"    IsCoreCard: SetId={cardSetId} is Core -> FREE");
            }
            
            return isCore;
        }

        private int GetCraftingCost(int rarityId, int cardSetId)
        {
            // Спочатку перевіряємо чи це Core карта (безкоштовна)
            if (IsCoreCard(cardSetId))
            {
                Log.Debug($"    GetCraftingCost: Core card (SetId={cardSetId}) -> 0 dust");
                return 0;
            }
            
            // Потім рахуємо за рідкістю
            int cost;
            string rarityName;
            
            switch (rarityId)
            {
                case 1: // Common
                    cost = 40;
                    rarityName = "Common";
                    break;
                case 2: // Герої або здібності (не враховуємо)
                    cost = 0;
                    rarityName = "Hero/Ability";
                    break;
                case 3: // Rare
                    cost = 100;
                    rarityName = "Rare";
                    break;
                case 4: // Epic
                    cost = 400;
                    rarityName = "Epic";
                    break;
                case 5: // Legendary
                    cost = 1600;
                    rarityName = "Legendary";
                    break;
                default:
                    cost = 0;
                    rarityName = "Unknown";
                    break;
            }
            
            Log.Debug($"    GetCraftingCost: RarityId={rarityId} ({rarityName}), SetId={cardSetId} -> {cost} dust");
            return cost;
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Opacity = 1.0;
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Opacity = 0.95;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCollapse();
        }
        
        private void ToggleCollapse()
        {
            _isCollapsed = !_isCollapsed;
            
            if (_isCollapsed)
            {
                // Згортаємо - приховуємо список колод, але залишаємо заголовок
                DeckListScrollViewer.Visibility = Visibility.Collapsed;
                DetailsPanel.Visibility = Visibility.Collapsed;
                CardListColumn.Visibility = Visibility.Collapsed;
                
                // Якщо є колоди, показуємо невеликий індикатор
                if (_deckManager.Decks.Count > 0)
                {
                    this.MinHeight = 80; // Трохи більше за заголовок
                }
                else
                {
                    this.MinHeight = 60; // Тільки заголовок
                }
                

            }
            else
            {
                // Розгортаємо
                DeckListScrollViewer.Visibility = Visibility.Visible;
                this.MinHeight = 60;
                

            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Закриваємо вікно
            this.Visibility = Visibility.Hidden;

        }

        private void DeckItemView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is DeckItemView deckItemView)
            {
                deckItemView.SetDeckManager(_deckManager);
                deckItemView.SetParentOverlay(this);
            }
        }

        private void CloseDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            CardListColumn.Visibility = Visibility.Collapsed;
            _currentDeckDetails = null;
            
            // Оновлюємо візуальний стан всіх DeckItemView
            UpdateAllDeckItemsVisualState();
            

        }

        private async void CopyDeckCodeDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeckDetails != null && !string.IsNullOrEmpty(_currentDeckDetails.DeckCode))
            {
                try
                {
                    Clipboard.SetText(_currentDeckDetails.DeckCode);
                    
                    // Показуємо вікно фокусу, якщо увімкнено
                    if (PluginConfig.Instance.ShowFocusWindowOnCopyEnabled)
                    {
                        try
                        {
                            var focusWindow = new FocusSwitchWindow();
                            focusWindow.Show();
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Failed to open FocusSwitchWindow: {ex.Message}");
                        }
                    }
                    
                    // Візуальний фідбек
                    if (sender is Button button)
                    {
                        var originalBackground = button.Background;
                        button.Background = new SolidColorBrush(Colors.LimeGreen);
                        await Task.Delay(1000);
                        button.Background = originalBackground;
                    }
                    

                }
                catch (Exception ex)
                {
                    Log.Error($"ImprovedOverlayView: Error copying deck code: {ex.Message}");
                }
            }
        }

        private void RemoveDeckDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeckDetails != null)
            {
                _deckManager.Decks.Remove(_currentDeckDetails);
                DetailsPanel.Visibility = Visibility.Collapsed;
                _currentDeckDetails = null;

            }
        }
    }
}