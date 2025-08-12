using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json.Linq;
using HearthMirror;
using HearthMirror.Objects;
using TwitchDeckOverlay.Models;
using TwitchDeckOverlay.Config;

namespace TwitchDeckOverlay.Services
{
    public class BlizzardApiService
    {
        private readonly string _bearerToken;
        private readonly HttpClient _httpClient;
        private readonly PluginConfig _config;

        public BlizzardApiService(string bearerToken, PluginConfig config)
        {
            _bearerToken = bearerToken;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
            _config = config;
            Log.Info("BlizzardApiService initialized");
        }

        public void UpdateBearerToken(string newToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
            Log.Info("Blizzard bearer token updated");
        }

        public async Task<DeckInfo> GetDeckInfoAsync(string deckCode)
        {
            try
            {
                var url = $"https://us.api.blizzard.com/hearthstone/deck?locale=en_US&code={deckCode}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return ParseDeckResponse(content);
            }
            catch (Exception ex)
            {
                Log.Error($"Error fetching deck info: {ex.Message}");
                return null;
            }
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

                var sideboardCards = jObject["sideboardCards"]?.ToObject<List<JObject>>();
                if (sideboardCards != null && sideboardCards.Any())
                {
                    foreach (var sideboard in sideboardCards)
                    {
                        var sideboardCard = sideboard["sideboardCard"];
                        var sideboardCardId = sideboardCard["id"]?.ToString();
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
            try
            {
                var collection = Reflection.Client.GetCollection();
                if (collection == null || !collection.Any())
                {
                    Log.Warn("Could not retrieve user's collection from Hearthstone Deck Tracker via HearthMirror.");
                    foreach (var card in deckInfo.Cards)
                    {
                        card.IsMissingInCollection = false;
                    }
                    return;
                }

                var collectionDict = new Dictionary<int, int>();
                foreach (HearthMirror.Objects.Card collectedCard in collection)
                {
                    var dbCard = Database.GetCardFromId(collectedCard.Id);
                    if (dbCard != null && dbCard.DbfId != 0)
                    {
                        if (collectionDict.ContainsKey(dbCard.DbfId))
                        {
                            collectionDict[dbCard.DbfId] += collectedCard.Count;
                        }
                        else
                        {
                            collectionDict[dbCard.DbfId] = collectedCard.Count;
                        }
                    }
                }

                int dustNeeded = 0;
                int totalDustCost = 0;

                foreach (var cardInfo in deckInfo.Cards)
                {
                    int dbfId = cardInfo.Id;

                    int dustCost = _config.CalculateTotalDustCostEnabled ? GetCraftingCost(cardInfo.RarityId, cardInfo.CardSetId) : 0;
                    totalDustCost += dustCost * cardInfo.Count;

                    if (!collectionDict.TryGetValue(dbfId, out int ownedCount) || ownedCount < cardInfo.Count)
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
                            bool isMissing = !collectionDict.TryGetValue(componentDbfId, out componentOwnedCount) || componentOwnedCount < component.Count;
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
    }
}