using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json.Linq;
using HearthMirror;
using HearthMirror.Objects;
using TwitchDeckOverlay.Models;
using TwitchDeckOverlay.Config;
using TwitchDeckOverlay.Utility;

namespace TwitchDeckOverlay.Services
{
    /// <summary>
    /// BlizzardApiService з кешуванням та оптимізаціями продуктивності
    /// </summary>
    public class BlizzardApiService : IDisposable
    {
        private readonly string _bearerToken;
        private readonly HttpClient _httpClient;
        private readonly PluginConfig _config;
        private readonly PerformanceMonitor _performanceMonitor;
        
        // Кеш для API відповідей
        private readonly ConcurrentDictionary<string, CachedDeckInfo> _deckCache = new ConcurrentDictionary<string, CachedDeckInfo>();
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);
        private const int MaxCacheSize = 100;
        
        // Кеш для колекції користувача
        private Dictionary<int, int> _collectionCache;
        private DateTime _lastCollectionUpdate = DateTime.MinValue;
        private readonly TimeSpan _collectionCacheExpiry = TimeSpan.FromMinutes(5);
        
        // Семафор для обмеження паралельних запитів
        private readonly SemaphoreSlim _apiSemaphore = new SemaphoreSlim(3, 3); // Максимум 3 одночасних запити
        
        private bool _disposed = false;

        public BlizzardApiService(string bearerToken, PluginConfig config)
        {
            _bearerToken = bearerToken;
            _config = config;
            _performanceMonitor = new PerformanceMonitor();
            
            // Налаштовуємо HttpClient з оптимізаціями
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
            _httpClient.Timeout = TimeSpan.FromSeconds(10); // Таймаут 10 секунд
            
            Log.Info("BlizzardApiService initialized with caching and performance optimizations");
        }

        public void UpdateBearerToken(string newToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
            Log.Info("Blizzard bearer token updated");
        }

        public async Task<DeckInfo> GetDeckInfoAsync(string deckCode)
        {
            using (var operation = _performanceMonitor.StartOperation("GetDeckInfoAsync"))
            {
                try
                {
                    // Перевіряємо кеш спочатку
                    if (_deckCache.TryGetValue(deckCode, out var cachedEntry))
                    {
                        if (DateTime.UtcNow - cachedEntry.CachedAt < _cacheExpiry)
                        {
                            Log.Debug($"Returning cached deck for code: {deckCode}");
                            return cachedEntry.DeckInfo;
                        }
                        else
                        {
                            // Видаляємо застарілий запис
                            _deckCache.TryRemove(deckCode, out _);
                        }
                    }

                    // Обмежуємо кількість одночасних API запитів
                    await _apiSemaphore.WaitAsync();
                    
                    try
                    {
                        var url = $"https://us.api.blizzard.com/hearthstone/deck?locale=en_US&code={deckCode}";
                        
                        DeckInfo result;
                        using (var apiCall = _performanceMonitor.StartOperation("BlizzardApiCall"))
                        {
                            var response = await _httpClient.GetAsync(url);

                            if (!response.IsSuccessStatusCode)
                            {
                                Log.Warn($"Blizzard API returned {response.StatusCode} for deck code: {deckCode}");
                                return null;
                            }

                            var content = await response.Content.ReadAsStringAsync();
                            Log.Debug($"Received {content.Length} characters from Blizzard API");
                            
                            using (var parseOperation = _performanceMonitor.StartOperation("ParseDeckResponse"))
                            {
                                result = ParseDeckResponse(content);
                            }
                        }
                        
                        if (result != null)
                        {
                            // Додаємо до кешу
                            CacheDeck(deckCode, result);
                            Log.Info($"Successfully parsed and cached deck: {result.Class} with {result.Cards?.Count ?? 0} cards");
                        }
                        
                        return result;
                    }
                    finally
                    {
                        _apiSemaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error fetching deck info: {ex.Message}");
                    return null;
                }
            }
        }

        private void CacheDeck(string deckCode, DeckInfo deckInfo)
        {
            // Обмежуємо розмір кешу
            if (_deckCache.Count >= MaxCacheSize)
            {
                // Видаляємо найстаріші записи (10% від максимального розміру)
                var entriesToRemove = _deckCache
                    .OrderBy(kvp => kvp.Value.CachedAt)
                    .Take(MaxCacheSize / 10)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in entriesToRemove)
                {
                    _deckCache.TryRemove(key, out _);
                }
                
                Log.Debug($"Cleaned up {entriesToRemove.Count} old cache entries");
            }

            _deckCache[deckCode] = new CachedDeckInfo
            {
                DeckInfo = deckInfo,
                CachedAt = DateTime.UtcNow
            };
        }

        private string DeckCodeNormalizer(string deckCode)
        {
            return deckCode.Replace(" ", "+");
        }

        private DeckInfo ParseDeckResponse(string json)
        {
            try
            {
                var jObject = JObject.Parse(json);

                var deckInfo = new DeckInfo
                {
                    Class = jObject["class"]?["name"]?.ToString() ?? "Unknown",
                    Mode = jObject["format"]?.ToString() ?? "standard",
                    DeckCode = DeckCodeNormalizer(jObject["deckCode"]?.ToString()),
                    Cards = new List<CardInfo>(),
                    DustNeeded = 0,
                    TotalDustCost = 0,
                    RuneSlots = jObject["runeSlots"] != null ? new RuneSlots
                    {
                        Blood = (int?)jObject["runeSlots"]["blood"] ?? 0,
                        Frost = (int?)jObject["runeSlots"]["frost"] ?? 0,
                        Unholy = (int?)jObject["runeSlots"]["unholy"] ?? 0
                    } : null,
                    HeroPowerImage = jObject["heroPower"]?["image"]?.ToString(),
                    HeroImage = jObject["hero"]?["cropImage"]?.ToString()
                };

                var cards = jObject["cards"]?.ToObject<List<JObject>>();
                if (cards == null || cards.Count == 0)
                {
                    return deckInfo;
                }

                var cardGroups = cards
                    .GroupBy(card => card["id"]?.ToString())
                    .Select(group => new
                    {
                        Id = group.Key,
                        Count = group.Count(),
                        Card = group.First()
                    });

                foreach (var group in cardGroups)
                {
                    var card = group.Card;
                    var cardInfo = new CardInfo
                    {
                        Id = (int?)card["id"] ?? 0,
                        Name = card["name"]?.ToString() ?? "Unknown",
                        Cost = (int?)card["manaCost"] ?? 0,
                        Count = group.Count,
                        ImageUrl = card["image"]?.ToString(),
                        CropImage = card["cropImage"]?.ToString(),
                        RarityId = (int?)card["rarityId"] ?? 1,
                        CardSetId = (int?)card["cardSetId"] ?? 0,
                        HasComponents = false,
                        Components = new List<CardInfo>()
                    };
                    deckInfo.Cards.Add(cardInfo);
                }

                // Обробка sideboardCards (якщо є)
                var sideboardCards = jObject["sideboardCards"]?.ToObject<List<JObject>>();
                if (sideboardCards != null && sideboardCards.Any())
                {
                    foreach (var sideboard in sideboardCards)
                    {
                        var sideboardCard = sideboard["sideboardCard"];
                        var cardsInSideboard = sideboard["cardsInSideboard"]?.ToObject<List<JObject>>();

                        var matchingCard = deckInfo.Cards.FirstOrDefault(c => c.Name == sideboardCard["name"]?.ToString());
                        if (matchingCard != null)
                        {
                            matchingCard.HasComponents = true;
                            if (cardsInSideboard != null)
                            {
                                foreach (var componentCard in cardsInSideboard)
                                {
                                    var component = new CardInfo
                                    {
                                        Id = (int?)componentCard["id"] ?? 0,
                                        Name = componentCard["name"]?.ToString() ?? "Unknown",
                                        Cost = (int?)componentCard["manaCost"] ?? 0,
                                        Count = 1,
                                        ImageUrl = componentCard["image"]?.ToString(),
                                        CropImage = componentCard["cropImage"]?.ToString(),
                                        RarityId = (int?)componentCard["rarityId"] ?? 1,
                                        CardSetId = (int?)componentCard["cardSetId"] ?? 0,
                                        HasComponents = false,
                                        Components = new List<CardInfo>()
                                    };
                                    matchingCard.Components.Add(component);
                                }
                            }
                        }
                    }
                }

                deckInfo.Cards = deckInfo.Cards
                    .OrderBy(c => c.Cost)
                    .ThenBy(c => c.Name)
                    .ToList();

                if (_config.CheckCardsInCollectionEnabled)
                {
                    CheckCardsInCollection(deckInfo);
                }

                return deckInfo;
            }
            catch (Exception ex)
            {
                Log.Error($"Parse error: {ex.Message}");
                return null;
            }
        }

        private void CheckCardsInCollection(DeckInfo deckInfo)
        {
            using (var operation = _performanceMonitor.StartOperation("CheckCardsInCollection"))
            {
                try
                {
                    // Оновлюємо кеш колекції якщо потрібно
                    UpdateCollectionCache();

                    if (_collectionCache == null || !_collectionCache.Any())
                    {
                        Log.Warn("Could not retrieve user's collection from Hearthstone Deck Tracker via HearthMirror.");
                        foreach (var card in deckInfo.Cards)
                        {
                            card.IsMissingInCollection = false;
                        }
                        return;
                    }

                    int dustNeeded = 0;
                    int totalDustCost = 0;

                    foreach (var cardInfo in deckInfo.Cards)
                    {
                        int dbfId = cardInfo.Id;

                        int dustCost = _config.CalculateTotalDustCostEnabled ? GetCraftingCost(cardInfo.RarityId, cardInfo.CardSetId) : 0;
                        totalDustCost += dustCost * cardInfo.Count;

                        if (!_collectionCache.TryGetValue(dbfId, out int ownedCount) || ownedCount < cardInfo.Count)
                        {
                            cardInfo.IsMissingInCollection = true;
                            if (_config.CalculateDustNeededEnabled)
                            {
                                int missingCount = cardInfo.Count - (ownedCount > 0 ? ownedCount : 0);
                                dustNeeded += dustCost * missingCount;
                            }
                        }
                        else
                        {
                            cardInfo.IsMissingInCollection = false;
                        }

                        // Обробка компонентів
                        if (cardInfo.HasComponents)
                        {
                            if (!cardInfo.IsMissingInCollection)
                            {
                                foreach (var component in cardInfo.Components)
                                {
                                    int componentDustCost = _config.CalculateTotalDustCostEnabled ? GetCraftingCost(component.RarityId, component.CardSetId) : 0;
                                    totalDustCost += componentDustCost * component.Count;
                                }
                                continue;
                            }

                            foreach (var component in cardInfo.Components)
                            {
                                int componentDustCost = _config.CalculateTotalDustCostEnabled ? GetCraftingCost(component.RarityId, component.CardSetId) : 0;
                                totalDustCost += componentDustCost * component.Count;

                                int componentDbfId = component.Id;
                                int componentOwnedCount = 0;
                                bool isMissing = !_collectionCache.TryGetValue(componentDbfId, out componentOwnedCount) || componentOwnedCount < component.Count;
                                if (_config.CalculateDustNeededEnabled && isMissing)
                                {
                                    int missingCount = component.Count - componentOwnedCount;
                                    dustNeeded += componentDustCost * missingCount;
                                }
                            }
                        }
                    }

                    deckInfo.DustNeeded = dustNeeded;
                    deckInfo.TotalDustCost = totalDustCost;
                    
                    Log.Debug($"Collection check completed: {dustNeeded} dust needed, {totalDustCost} total dust cost");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error checking cards in collection: {ex.Message}");
                    foreach (var card in deckInfo.Cards)
                    {
                        card.IsMissingInCollection = false;
                    }
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
                var collection = Reflection.Client.GetCollection();
                if (collection == null || !collection.Any())
                {
                    return;
                }

                _collectionCache = new Dictionary<int, int>();
                foreach (HearthMirror.Objects.Card collectedCard in collection)
                {
                    var dbCard = Database.GetCardFromId(collectedCard.Id);
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
                Log.Debug($"Collection cache updated: {_collectionCache.Count} unique cards");
            }
            catch (Exception ex)
            {
                Log.Error($"Error updating collection cache: {ex.Message}");
            }
        }

        private int GetCraftingCost(int rarityId, int cardSetId)
        {
            if (cardSetId == 1637)
            {
                return 0;
            }

            switch (rarityId)
            {
                case 1: // Common
                    return 40;
                case 2: // Герої або здібності (не враховуємо)
                    return 0;
                case 3: // Rare
                    return 100;
                case 4: // Epic
                    return 400;
                case 5: // Legendary
                    return 1600;
                default:
                    return 0; // Для невідомої рідкості
            }
        }

        public void ClearCache()
        {
            _deckCache.Clear();
            _collectionCache = null;
            _lastCollectionUpdate = DateTime.MinValue;
            Log.Info("All caches cleared");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _performanceMonitor?.LogPerformanceReport();
                _performanceMonitor?.Dispose();
                _httpClient?.Dispose();
                _apiSemaphore?.Dispose();
                _disposed = true;
            Log.Info("BlizzardApiService disposed");
            }
        }

        private class CachedDeckInfo
        {
            public DeckInfo DeckInfo { get; set; }
            public DateTime CachedAt { get; set; }
        }
    }
}