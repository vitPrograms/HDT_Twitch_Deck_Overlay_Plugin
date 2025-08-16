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
using TwitchDeckOverlay.Services;

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
        
        private async Task LoadOnlineStatisticsAsync(DeckInfo deckInfo)
        {
            Log.Info($"LoadOnlineStatisticsAsync called for deck: {deckInfo.Author}");
            Log.Info($"FetchOnlineStatisticsEnabled: {PluginConfig.Instance.FetchOnlineStatisticsEnabled}");
            Log.Info($"DeckCode: {deckInfo.DeckCode}");
            
            if (!PluginConfig.Instance.FetchOnlineStatisticsEnabled || string.IsNullOrEmpty(deckInfo.DeckCode))
            {
                Log.Info("LoadOnlineStatisticsAsync: Skipping - disabled or no deck code");
                return;
            }

            try
            {
                Log.Info("LoadOnlineStatisticsAsync: Calling HSGuruService...");
                var onlineStats = await HSGuruService.GetDeckStatisticsAsync(deckInfo.DeckCode);
                
                if (onlineStats != null)
                {
                    Log.Info($"LoadOnlineStatisticsAsync: Got stats - WinRate: {onlineStats.WinRate}%, Games: {onlineStats.TotalGames}");
                    deckInfo.OnlineStats = onlineStats;
                    
                    // Якщо є назва колоди з HSGuru, використовуємо її
                    if (!string.IsNullOrEmpty(onlineStats.DeckName))
                    {
                        Log.Info($"LoadOnlineStatisticsAsync: Updating deck title to: {onlineStats.DeckName}");
                        Dispatcher.Invoke(() => {
                            DetailsDeckTitle.Text = onlineStats.DeckName;
                            HSGuruLogoTitle.Visibility = Visibility.Visible;
                        });
                    }
                    
                    // Оновлюємо UI в головному потоці
                    Dispatcher.Invoke(() => {
                        UpdateOnlineStatisticsUI(deckInfo);
                        // Оновлюємо іконку архетипу після отримання онлайн статистики
                        UpdateArchetypeIcon(deckInfo);
                    });
                }
                else
                {
                    Log.Info("LoadOnlineStatisticsAsync: No stats returned from HSGuruService");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error loading online statistics: {ex.Message}");
            }
        }
        
        private void ClearOnlineStatisticsUI()
        {
            Log.Info("ClearOnlineStatisticsUI: Clearing online statistics display");
            
            // Очищуємо відображення онлайн статистики
            DetailsWinRate.Text = "--%";
            DetailsTotalGames.Text = "--";
            
            // Ховаємо HSGuru логотипи
            HSGuruLogoWinRate.Visibility = Visibility.Collapsed;
            HSGuruLogoGames.Visibility = Visibility.Collapsed;
            HSGuruLogoTitle.Visibility = Visibility.Collapsed;
        }

        private void UpdateOnlineStatisticsUI(DeckInfo deckInfo)
        {
            if (deckInfo != _currentDeckDetails || deckInfo.OnlineStats == null)
            {
                Log.Info("UpdateOnlineStatisticsUI: Skipping - deck not current or no stats");
                return;
            }

            Log.Info($"UpdateOnlineStatisticsUI: Updating UI for deck {deckInfo.Author} with winrate {deckInfo.OnlineStats.WinRate}%");

            // Показуємо винрейт якщо є дані (включаючи 0% винрейт, якщо є ігри)
            if (deckInfo.OnlineStats.WinRate >= 0 && deckInfo.OnlineStats.TotalGames > 0)
            {
                DetailsWinRate.Text = $"{deckInfo.OnlineStats.WinRate:F1}%";
                DetailsTotalGames.Text = deckInfo.OnlineStats.TotalGames.ToString();
                
                // Показуємо HSGuru логотипи для онлайн статистики
                HSGuruLogoWinRate.Visibility = Visibility.Visible;
                HSGuruLogoGames.Visibility = Visibility.Visible;
                
                Log.Info($"UpdateOnlineStatisticsUI: Updated stats - WinRate: {deckInfo.OnlineStats.WinRate:F1}%, Games: {deckInfo.OnlineStats.TotalGames}");
            }
            else
            {
                Log.Info("UpdateOnlineStatisticsUI: No valid online stats data");
                DetailsWinRate.Text = "--%";
                DetailsTotalGames.Text = "--";
                
                // Ховаємо HSGuru логотипи
                HSGuruLogoWinRate.Visibility = Visibility.Collapsed;
                HSGuruLogoGames.Visibility = Visibility.Collapsed;
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
                    DetailsDustCost.Text = "Loading...";
                    
                    // Асинхронно завантажуємо колекцію і підраховуємо
                    _ = CalculateDustCostsAsync(deckInfo);
                }
                
                // Оновлюємо інформацію в панелі деталей
                DetailsDeckTitle.Text = $"Deck: {deckInfo.Author}";
                
                // Оновлюємо режим гри з нормалізацією
                DetailsMode.Text = NormalizeGameMode(deckInfo.Mode);
                
                // Встановлюємо іконку класу
                try
                {
                    // Спробуємо отримати іконку класу з HDT
                    var heroCard = Hearthstone_Deck_Tracker.Hearthstone.Database.GetHeroCardFromClass(deckInfo.Class);
                    if (heroCard != null)
                    {
                        var heroImage = ImageCache.GetClassIcon(heroCard.PlayerClass);
                        Log.Info($"ImprovedOverlayView: Class icon loading removed - using archetype icon instead");
                    }
                    else
                    {
                        Log.Debug($"ImprovedOverlayView: No hero card found for {deckInfo.Class}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"ImprovedOverlayView: Class icon loading skipped: {ex.Message}");
                }
                
                DetailsClass.Text = deckInfo.Class;
                DetailsMode.Text = NormalizeGameMode(deckInfo.Mode);
                
                // Оновлюємо іконку архетипу
                UpdateArchetypeIcon(deckInfo);
                
                // Очищуємо онлайн статистику при зміні колоди
                ClearOnlineStatisticsUI();
                
                // Оновлюємо додаткову статистику
                DetailsLegendaryCount.Text = deckInfo.Statistics.LegendaryCards.ToString();
                
                // Створюємо графік мани
                CreateManaCurve(deckInfo);
                
                // Завантажуємо онлайн статистику асинхронно
                _ = LoadOnlineStatisticsAsync(deckInfo);
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
                        DetailsDustCost.Text = totalDustCost.ToString();
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



                    if (!_collectionCache.TryGetValue(dbfId, out int ownedCount) || ownedCount < cardInfo.Count)
                    {
                        cardInfo.IsMissingInCollection = true;
                        int missingCount = cardInfo.Count - (ownedCount > 0 ? ownedCount : 0);
                        int cardDustNeeded = dustCost * missingCount;
                        dustNeeded += cardDustNeeded;
                        

                    }
                    else
                    {
                        cardInfo.IsMissingInCollection = false;
                    }

                    // Обробляємо компоненти карт (якщо є)
                    if (cardInfo.HasComponents)
                    {
                        
                        if (!cardInfo.IsMissingInCollection)
                        {
                            foreach (var component in cardInfo.Components)
                            {
                                int componentDustCost = GetCraftingCost(component.RarityId, component.CardSetId);
                                int componentTotalCost = componentDustCost * component.Count;
                                totalDustCostCalculated += componentTotalCost;
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

                            
                            if (isMissing)
                            {
                                int missingCount = component.Count - componentOwnedCount;
                                int componentDustNeeded = componentDustCost * missingCount;
                                dustNeeded += componentDustNeeded;
                                
                            }
                        }
                    }
                    

                }

                deckInfo.DustNeeded = dustNeeded;
                deckInfo.TotalDustCost = totalDustCostCalculated;

                // Підсумкове логування
                Log.Info("=== DUST CALCULATION SUMMARY ===");
                Log.Info($"TOTAL DUST COST: {totalDustCostCalculated}");
                Log.Info($"DUST NEEDED: {dustNeeded}");
                Log.Info("=== END SUMMARY ===");

                // Оновлюємо UI в головному потоці
                Dispatcher.Invoke(() =>
                {
                    DetailsDustCost.Text = totalDustCostCalculated.ToString();
                    
                    // Показуємо інформацію про недостачу пилу
                    if (dustNeeded > 0)
                    {
                        DetailsDustNeeded.Text = $"(-{dustNeeded})";
                        DetailsDustNeeded.Visibility = Visibility.Visible;
                        DetailsDustNeeded.ToolTip = $"Need {dustNeeded} more dust to craft this deck";
                    }
                    else
                    {
                        DetailsDustNeeded.Visibility = Visibility.Collapsed;
                    }
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
                    DetailsDustCost.Text = fallbackTotal.ToString();
                    DetailsDustNeeded.Visibility = Visibility.Collapsed;
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
                    DetailsDustCost.Text = deckInfo.TotalDustCost.ToString();
                    
                    // Показуємо інформацію про недостачу пилу
                    if (deckInfo.DustNeeded > 0)
                    {
                        DetailsDustNeeded.Text = $"(-{deckInfo.DustNeeded})";
                        DetailsDustNeeded.Visibility = Visibility.Visible;
                        DetailsDustNeeded.ToolTip = $"Need {deckInfo.DustNeeded} more dust to craft this deck";
                    }
                    else
                    {
                        DetailsDustNeeded.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error in CalculateDustCostsAsync: {ex.Message}");
                if (_currentDeckDetails == deckInfo && DetailsPanel.Visibility == Visibility.Visible)
                {
                    DetailsDustCost.Text = "-";
                    DetailsDustNeeded.Visibility = Visibility.Collapsed;
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

        private async void CopyDeckCodeDetails_Click(object sender, MouseButtonEventArgs e)
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

        private void RemoveDeckDetails_Click(object sender, MouseButtonEventArgs e)
        {
            if (_currentDeckDetails != null)
            {
                _deckManager.Decks.Remove(_currentDeckDetails);
                DetailsPanel.Visibility = Visibility.Collapsed;
                CardListColumn.Visibility = Visibility.Collapsed;
                _currentDeckDetails = null;
                
                // Update visual state of all deck items
                UpdateAllDeckItemsVisualState();
            }
        }

        private void CreateManaCurve(DeckInfo deckInfo)
        {
            try
            {
                ManaCurveCanvas.Children.Clear();
                
                // Підраховуємо карти по мані (0-7+)
                var manaCounts = new int[8]; // 0, 1, 2, 3, 4, 5, 6, 7+
                
                foreach (var card in deckInfo.Cards)
                {
                    int manaSlot = Math.Min(card.Cost, 7);
                    manaCounts[manaSlot] += card.Count;
                }
                
                // Знаходимо максимум для масштабування
                int maxCount = manaCounts.Max();
                if (maxCount == 0) return;
                
                double canvasWidth = ManaCurveCanvas.Width;
                double canvasHeight = ManaCurveCanvas.Height;
                double barWidth = (canvasWidth - 16) / 8; // 8 стовпців з відступами
                double maxBarHeight = canvasHeight - 20; // Залишаємо місце для підписів
                
                for (int i = 0; i < 8; i++)
                {
                    double barHeight = (double)manaCounts[i] / maxCount * maxBarHeight;
                    double x = i * barWidth + 2;
                    double y = canvasHeight - barHeight - 15;
                    
                    // Створюємо стовпець
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Width = barWidth - 2,
                        Height = barHeight,
                        Fill = new SolidColorBrush(Color.FromArgb(255, 100, 149, 237)), // Синій колір
                        Stroke = new SolidColorBrush(Color.FromArgb(255, 70, 130, 180)),
                        StrokeThickness = 1
                    };
                    
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    ManaCurveCanvas.Children.Add(rect);
                    
                    // Додаємо підпис мани
                    var manaLabel = new TextBlock
                    {
                        Text = i == 7 ? "7+" : i.ToString(),
                        FontSize = 8,
                        Foreground = new SolidColorBrush(Colors.LightGray),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    
                    Canvas.SetLeft(manaLabel, x + (barWidth - 2) / 2 - 4);
                    Canvas.SetTop(manaLabel, canvasHeight - 12);
                    ManaCurveCanvas.Children.Add(manaLabel);
                    
                    // Додаємо кількість карт над стовпцем, якщо є
                    if (manaCounts[i] > 0)
                    {
                        var countLabel = new TextBlock
                        {
                            Text = manaCounts[i].ToString(),
                            FontSize = 8,
                            Foreground = new SolidColorBrush(Colors.White),
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        
                        Canvas.SetLeft(countLabel, x + (barWidth - 2) / 2 - 4);
                        Canvas.SetTop(countLabel, y - 12);
                        ManaCurveCanvas.Children.Add(countLabel);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error creating mana curve: {ex.Message}");
            }
        }

        private void MatchupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentDeckDetails?.OnlineStats?.ClassMatchups == null || 
                    _currentDeckDetails.OnlineStats.ClassMatchups.Count == 0)
                {
                    return;
                }

                // Створюємо вікно з матчапами
                var matchupsWindow = new Window
                {
                    Title = "Class Matchups",
                    Width = 300,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow,
                    Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                    ResizeMode = ResizeMode.NoResize
                };

                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Padding = new Thickness(10)
                };

                var stackPanel = new StackPanel();

                // Заголовок
                var titleBlock = new TextBlock
                {
                    Text = "Class Matchups",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stackPanel.Children.Add(titleBlock);

                // Сортуємо матчапи по винрейту (найкращі зверху)
                var sortedMatchups = _currentDeckDetails.OnlineStats.ClassMatchups
                    .OrderByDescending(kvp => kvp.Value)
                    .ToList();

                foreach (var matchup in sortedMatchups)
                {
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(255, 45, 45, 45)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var classText = new TextBlock
                    {
                        Text = matchup.Key,
                        Foreground = new SolidColorBrush(Colors.White),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(classText, 0);

                    var winRateText = new TextBlock
                    {
                        Text = $"{matchup.Value:F1}%",
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Колір залежно від винрейту
                    if (matchup.Value >= 60)
                        winRateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)); // Зелений
                    else if (matchup.Value >= 50)
                        winRateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 193, 7)); // Жовтий
                    else
                        winRateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54)); // Червоний

                    Grid.SetColumn(winRateText, 1);

                    grid.Children.Add(classText);
                    grid.Children.Add(winRateText);
                    border.Child = grid;
                    stackPanel.Children.Add(border);
                }

                scrollViewer.Content = stackPanel;
                matchupsWindow.Content = scrollViewer;
                matchupsWindow.Show();
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error showing matchups: {ex.Message}");
            }
        }

        private void MatchupsButton_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                if (_currentDeckDetails?.OnlineStats?.ClassMatchups == null || 
                    _currentDeckDetails.OnlineStats.ClassMatchups.Count == 0)
                {
                    return;
                }

                // Очищуємо попередній контент tooltip
                MatchupsTooltipContent.Children.Clear();

                // Заголовок
                var titleBlock = new TextBlock
                {
                    Text = "Class Matchups",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                MatchupsTooltipContent.Children.Add(titleBlock);

                // Сортуємо матчапи по винрейту (найкращі зверху)
                var sortedMatchups = _currentDeckDetails.OnlineStats.ClassMatchups
                    .OrderByDescending(kvp => kvp.Value)
                    .ToList();

                foreach (var matchup in sortedMatchups)
                {
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 67)),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 1, 0, 1)
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var classText = new TextBlock
                    {
                        Text = matchup.Key,
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(classText, 0);

                    var winRateText = new TextBlock
                    {
                        Text = $"{matchup.Value:F1}%",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Кольорове кодування винрейту
                    if (matchup.Value >= 60)
                    {
                        winRateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 111, 207, 151)); // Зелений
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 111, 207, 151));
                    }
                    else if (matchup.Value >= 50)
                    {
                        winRateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 242, 201, 76)); // Жовтий
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 242, 201, 76));
                    }
                    else
                    {
                        winRateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 235, 87, 87)); // Червоний
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 235, 87, 87));
                    }

                    border.BorderThickness = new Thickness(1);
                    Grid.SetColumn(winRateText, 1);

                    grid.Children.Add(classText);
                    grid.Children.Add(winRateText);
                    border.Child = grid;
                    MatchupsTooltipContent.Children.Add(border);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error updating matchups tooltip: {ex.Message}");
            }
        }

        private void MatchupsButton_MouseLeave(object sender, MouseEventArgs e)
        {
            // Tooltip автоматично зникне
        }

        private string NormalizeGameMode(string mode)
        {
            if (string.IsNullOrEmpty(mode))
                return mode;
                
            switch (mode.ToUpper())
            {
                case "FT_STANDARD":
                    return "STANDARD";
                case "FT_WILD":
                    return "WILD";
                case "FT_CLASSIC":
                    return "CLASSIC";
                case "FT_TWIST":
                    return "TWIST";
                default:
                    return mode; // Повертаємо як є, якщо не знайдено відповідності
            }
        }

        private void UpdateArchetypeIcon(DeckInfo deckInfo)
        {
            try
            {
                // Визначаємо іконку залежно від архетипу колоди
                string icon = "⚡"; // За замовчуванням
                string tooltip = "Deck Archetype";
                string archetypeText = "";
                
                // Спочатку перевіряємо чи є дані з HSGuru про архетип
                if (deckInfo.OnlineStats?.ArchetypeCategory != null)
                {
                    var category = deckInfo.OnlineStats.ArchetypeCategory;
                    switch (category.ToLower())
                    {
                        case "aggro":
                            icon = "⚔️";
                            tooltip = "Aggro Deck";
                            archetypeText = "Aggro";
                            break;
                        case "control":
                            icon = "🛡️";
                            tooltip = "Control Deck";
                            archetypeText = "Control";
                            break;
                        case "control/combo":
                            icon = "🎯";
                            tooltip = "Control/Combo Deck";
                            archetypeText = "Control/Combo";
                            break;
                        case "midrange":
                            icon = "⚖️";
                            tooltip = "Midrange Deck";
                            archetypeText = "Midrange";
                            break;
                    }
                    
                    if (deckInfo.OnlineStats.AverageTurns > 0)
                    {
                        tooltip += $" (Avg: {deckInfo.OnlineStats.AverageTurns:F1} turns)";
                    }
                }
                else
                {
                    // Fallback до старого методу на основі назви автора
                    var deckName = deckInfo.Author.ToLower();
                    
                    if (deckName.Contains("aggro") || deckName.Contains("face"))
                    {
                        icon = "⚔️";
                        tooltip = "Aggro Deck";
                    }
                    else if (deckName.Contains("control"))
                    {
                        icon = "🛡️";
                        tooltip = "Control Deck";
                    }
                    else if (deckName.Contains("combo"))
                    {
                        icon = "🎯";
                        tooltip = "Combo Deck";
                    }
                    else if (deckName.Contains("midrange"))
                    {
                        icon = "⚖️";
                        tooltip = "Midrange Deck";
                    }
                    else if (deckName.Contains("tempo"))
                    {
                        icon = "💨";
                        tooltip = "Tempo Deck";
                    }
                    else if (deckName.Contains("ramp"))
                    {
                        icon = "📈";
                        tooltip = "Ramp Deck";
                    }
                    else if (deckName.Contains("otk") || deckName.Contains("one turn kill"))
                    {
                        icon = "💥";
                        tooltip = "OTK Deck";
                    }
                }
                
                DetailsArchetype.Text = icon;
                DetailsArchetype.ToolTip = tooltip;
                
                // Оновлюємо текст архетипу під іконкою
                if (!string.IsNullOrEmpty(archetypeText))
                {
                    DetailsArchetypeText.Text = archetypeText;
                    DetailsArchetypeText.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailsArchetypeText.Visibility = Visibility.Collapsed;
                }
                
                Log.Debug($"ImprovedOverlayView: Set archetype icon {icon} ({archetypeText}) for deck {deckInfo.Author}");
            }
            catch (Exception ex)
            {
                Log.Error($"ImprovedOverlayView: Error updating archetype icon: {ex.Message}");
                DetailsArchetype.Text = "⚡";
                DetailsArchetype.ToolTip = "Deck Archetype";
                DetailsArchetypeText.Visibility = Visibility.Collapsed;
            }
        }
    }
}