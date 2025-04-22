using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;
using Newtonsoft.Json.Linq;
using TwitchDeckOverlay.Models;

namespace TwitchDeckOverlay.Services
{
    public class BlizzardApiService
    {
        private readonly string _bearerToken;
        private readonly HttpClient _httpClient;

        public BlizzardApiService(string bearerToken)
        {
            _bearerToken = bearerToken;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
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

        private DeckInfo ParseDeckResponse(string json)
        {
            try
            {
                var jObject = JObject.Parse(json);

                var deckInfo = new DeckInfo
                {
                    Class = jObject["class"]?["name"]?.ToString() ?? "Unknown",
                    Mode = jObject["format"]?.ToString() ?? "standard",
                    DeckCode = jObject["deckCode"]?.ToString(),
                    Cards = new List<CardInfo>(),
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
                        Name = card["name"]?.ToString() ?? "Unknown",
                        Cost = (int?)card["manaCost"] ?? 0,
                        Count = group.Count,
                        ImageUrl = card["image"]?.ToString(),
                        CropImage = card["cropImage"]?.ToString(),
                        RarityId = (int?)card["rarityId"] ?? 1,
                        HasComponents = false,
                        Components = new List<CardInfo>()
                    };
                    deckInfo.Cards.Add(cardInfo);
                }

                // Обробка sideboardCards
                var sideboardCards = jObject["sideboardCards"]?.ToObject<List<JObject>>();
                if (sideboardCards != null && sideboardCards.Any())
                {
                    foreach (var sideboard in sideboardCards)
                    {
                        var sideboardCard = sideboard["sideboardCard"];
                        var sideboardCardId = sideboardCard["id"]?.ToString();
                        var cardsInSideboard = sideboard["cardsInSideboard"]?.ToObject<List<JObject>>();

                        // Знаходимо карту в списку cards
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
                                        Name = componentCard["name"]?.ToString() ?? "Unknown",
                                        Cost = (int?)componentCard["manaCost"] ?? 0,
                                        Count = 1,
                                        ImageUrl = componentCard["image"]?.ToString(),
                                        CropImage = componentCard["cropImage"]?.ToString(),
                                        RarityId = (int?)componentCard["rarityId"] ?? 1,
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

                return deckInfo;
            }
            catch (Exception ex)
            {
                Log.Error($"Parse error: {ex.Message}");
                return null;
            }
        }
    }
}