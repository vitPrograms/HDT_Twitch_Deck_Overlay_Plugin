using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HearthDb;
using Hearthstone_Deck_Tracker.Utility.Logging;
using TwitchDeckOverlay.Models;

namespace TwitchDeckOverlay.Services
{
    public class HearthDbDeckParser
    {
        private static Type _deckSerializerType;
        private static MethodInfo _deserializeMethod;
        
        static HearthDbDeckParser()
        {
            try
            {
                // Шукаємо DeckSerializer в HearthDb assembly
                var assembly = typeof(Cards).Assembly;
                var allTypes = assembly.GetTypes();
                
                Log.Info($"HearthDbDeckParser: Searching in assembly: {assembly.FullName}");
                
                // Знаходимо тип DeckSerializer
                _deckSerializerType = allTypes.FirstOrDefault(t => t.Name == "DeckSerializer");
                
                if (_deckSerializerType != null)
                {
                    Log.Info($"HearthDbDeckParser: Found DeckSerializer type: {_deckSerializerType.FullName}");
                    
                    _deserializeMethod = _deckSerializerType.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
                    
                    if (_deserializeMethod != null)
                    {
                        Log.Info("HearthDbDeckParser: DeckSerializer found and initialized successfully");
                    }
                    else
                    {
                        Log.Warn("HearthDbDeckParser: Deserialize method not found");
                    }
                }
                else
                {
                    Log.Warn("HearthDbDeckParser: DeckSerializer type not found");
                    
                    // Логуємо всі типи що містять "Deck" для дебагу
                    var deckTypes = allTypes.Where(t => t.Name.Contains("Deck")).ToList();
                    Log.Info($"HearthDbDeckParser: Found {deckTypes.Count} types containing 'Deck':");
                    foreach (var type in deckTypes)
                    {
                        Log.Info($"  - {type.FullName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"HearthDbDeckParser initialization error: {ex.Message}");
            }
        }
        
        public static bool IsAvailable => _deserializeMethod != null;
        
        public static DeckInfo ParseDeckCode(string deckCode, string author = "Unknown")
        {
            if (!IsAvailable)
            {
                Log.Warn("HearthDbDeckParser: DeckSerializer not available");
                return null;
            }
            
            try
            {
                Log.Info($"HearthDbDeckParser: Parsing deck code: {deckCode}");
                
                var result = _deserializeMethod.Invoke(null, new object[] { deckCode });
                if (result == null)
                {
                    Log.Warn("HearthDbDeckParser: DeckSerializer returned null");
                    return null;
                }
                
                Log.Info($"HearthDbDeckParser: Successfully parsed deck, result type: {result.GetType().Name}");
                
                // Конвертуємо результат в DeckInfo
                var deckInfo = ConvertToDeckInfo(result, author);
                
                Log.Info($"HearthDbDeckParser: Converted to DeckInfo with {deckInfo?.Cards?.Count ?? 0} cards");
                return deckInfo;
            }
            catch (Exception ex)
            {
                Log.Error($"HearthDbDeckParser: Error parsing deck code: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log.Error($"HearthDbDeckParser: Inner exception: {ex.InnerException.Message}");
                }
                Log.Error($"HearthDbDeckParser: Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        private static DeckInfo ConvertToDeckInfo(object hearthDbDeck, string author)
        {
            try
            {
                var deckType = hearthDbDeck.GetType();
                var deckInfo = new DeckInfo
                {
                    Author = author,
                    DeckCode = "", // Буде встановлено пізніше
                    TimeAdded = DateTime.Now,
                    Cards = new List<CardInfo>()
                };
                
                Log.Debug($"HearthDbDeckParser: Analyzing deck type: {deckType.FullName}");
                
                // Отримуємо властивості HearthDb.Deck (правильні назви)
                var heroProperty = deckType.GetProperty("HeroDbfId");
                var cardsProperty = deckType.GetProperty("CardDbfIds");
                var formatProperty = deckType.GetProperty("Format");
                
                // Встановлюємо клас героя
                if (heroProperty != null)
                {
                    var heroId = heroProperty.GetValue(hearthDbDeck);
                    Log.Info($"HearthDbDeckParser: Hero ID: {heroId}");
                    
                    if (heroId != null)
                    {
                        var heroCard = Cards.All.Values.FirstOrDefault(c => c.DbfId == (int)heroId);
                        if (heroCard != null)
                        {
                            deckInfo.Class = heroCard.Class.ToString();
                            deckInfo.HeroImage = $"https://art.hearthstonejson.com/v1/256x/{heroCard.Id}.jpg";
                            Log.Info($"HearthDbDeckParser: Found hero: {heroCard.Name}, Class: {deckInfo.Class}");
                        }
                        else
                        {
                            Log.Warn($"HearthDbDeckParser: Hero card not found for ID: {heroId}");
                        }
                    }
                }
                else
                {
                    Log.Warn("HearthDbDeckParser: HeroDbfId property not found");
                }
                
                // Встановлюємо режим гри
                if (formatProperty != null)
                {
                    var format = formatProperty.GetValue(hearthDbDeck);
                    deckInfo.Mode = format?.ToString() ?? "Unknown";
                    Log.Info($"HearthDbDeckParser: Format: {deckInfo.Mode}");
                }
                else
                {
                    Log.Warn("HearthDbDeckParser: Format property not found");
                }
                
                // Отримуємо карти
                if (cardsProperty != null)
                {
                    var cards = cardsProperty.GetValue(hearthDbDeck);
                    Log.Info($"HearthDbDeckParser: Cards object type: {cards?.GetType().Name}");
                    
                    if (cards is System.Collections.Generic.Dictionary<int, int> cardDict)
                    {
                        Log.Info($"HearthDbDeckParser: Processing {cardDict.Count} card entries");
                        
                        foreach (var entry in cardDict)
                        {
                            var cardId = entry.Key;
                            var count = entry.Value;
                            
                            // Log.Debug($"HearthDbDeckParser: Processing card ID: {cardId}, Count: {count}");
                            
                            var card = Cards.All.Values.FirstOrDefault(c => c.DbfId == cardId);
                            if (card != null)
                            {
                                var cardInfo = new CardInfo
                                {
                                    Id = card.DbfId,
                                    Name = card.Name,
                                    Count = count,
                                    Cost = card.Cost,
                                    ImageUrl = $"https://art.hearthstonejson.com/v1/256x/{card.Id}.jpg",
                                    RarityId = (int)card.Rarity,
                                    CardSetId = (int)card.Set
                                };
                                
                                deckInfo.Cards.Add(cardInfo);
                                // Log.Debug($"HearthDbDeckParser: Added card: {card.Name} x{count}");
                            }
                            else
                            {
                                Log.Debug($"HearthDbDeckParser: Card not found for ID: {cardId}");
                            }
                        }
                    }
                    else
                    {
                        Log.Warn($"HearthDbDeckParser: Cards is not Dictionary<int,int>, type: {cards?.GetType().Name}");
                    }
                }
                else
                {
                    Log.Warn("HearthDbDeckParser: CardDbfIds property not found");
                }
                
                Log.Info($"HearthDbDeckParser: Converted deck - Class: {deckInfo.Class}, Cards: {deckInfo.Cards.Count}, Mode: {deckInfo.Mode}");
                return deckInfo;
            }
            catch (Exception ex)
            {
                Log.Error($"HearthDbDeckParser: Error converting to DeckInfo: {ex.Message}");
                Log.Error($"HearthDbDeckParser: Stack trace: {ex.StackTrace}");
                return null;
            }
        }
    }
}