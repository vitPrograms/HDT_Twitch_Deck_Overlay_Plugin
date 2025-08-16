using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Utility.Logging;
using TwitchDeckOverlay.Models;
using TwitchDeckOverlay.Config;

namespace TwitchDeckOverlay.Services
{
    public class DeckStatisticsInfo
    {
        public double WinRate { get; set; }
        public int TotalGames { get; set; }
        public Dictionary<string, double> ClassMatchups { get; set; } = new Dictionary<string, double>();
        public string Tier { get; set; }
        public string DeckName { get; set; }
        public double AverageTurns { get; set; }
        public string ArchetypeCategory { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class HSGuruService
    {
        private static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        private static readonly Dictionary<string, DeckStatisticsInfo> _cache = new Dictionary<string, DeckStatisticsInfo>();
        private static readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(30);
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3, 3); // Максимум 3 одночасні запити
        
        static HSGuruService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        public static async Task<DeckStatisticsInfo> GetDeckStatisticsAsync(string deckCode)
        {
            Log.Info($"HSGuruService: GetDeckStatisticsAsync called with deckCode: {deckCode}");
            
            if (string.IsNullOrEmpty(deckCode))
            {
                Log.Info("HSGuruService: Empty deck code, returning null");
                return null;
            }

            // Create cache key that includes filter parameters
            var config = PluginConfig.Instance;
            var cacheKey = $"{deckCode}_{config.HSGuruRankFilter}_{config.HSGuruPeriodFilter}";

            // Перевіряємо кеш
            if (_cache.TryGetValue(cacheKey, out var cachedStats))
            {
                if (DateTime.Now - cachedStats.LastUpdated < _cacheExpiry)
                {
                    Log.Info("HSGuruService: Returning cached statistics");
                    return cachedStats;
                }
                _cache.Remove(cacheKey);
                Log.Info("HSGuruService: Cache expired, removing old entry");
            }

            // Обмежуємо кількість одночасних запитів
            if (!await _semaphore.WaitAsync(100))
            {
                Log.Info("HSGuruService: Too many concurrent requests, skipping");
                return null;
            }

            try
            {
                // Build URL with filter parameters
                var url = $"https://www.hsguru.com/deck/{Uri.EscapeDataString(deckCode)}";
                
                // Add rank filter if specified
                if (!string.IsNullOrEmpty(config.HSGuruRankFilter) && config.HSGuruRankFilter != "all")
                {
                    url += $"?rank={Uri.EscapeDataString(config.HSGuruRankFilter)}";
                }
                
                // Add period filter if specified
                if (!string.IsNullOrEmpty(config.HSGuruPeriodFilter) && config.HSGuruPeriodFilter != "past_week")
                {
                    var separator = url.Contains("?") ? "&" : "?";
                    url += $"{separator}period={Uri.EscapeDataString(config.HSGuruPeriodFilter)}";
                }
                
                Log.Info($"HSGuruService: Fetching statistics from {url}");

                var response = await _httpClient.GetStringAsync(url);
                Log.Info($"HSGuruService: Received response, length: {response.Length} characters");
                
                var stats = ParseStatistics(response);
                
                if (stats != null)
                {
                    // Спробуємо отримати архетип з мета сторінки
                    await TryGetArchetypeFromMeta(stats);
                    
                    _cache[cacheKey] = stats;
                    Log.Info($"HSGuruService: Successfully parsed statistics - WinRate: {stats.WinRate}%, Games: {stats.TotalGames}, Archetype: {stats.ArchetypeCategory}");
                }
                else
                {
                    Log.Info("HSGuruService: Failed to parse statistics from response");
                }

                return stats;
            }
            catch (Exception ex)
            {
                Log.Error($"HSGuruService: Error fetching statistics: {ex.Message}");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static DeckStatisticsInfo ParseStatistics(string html)
        {
            try
            {
                var stats = new DeckStatisticsInfo();

                // DEBUG: Логуємо частину HTML навколо Total row для діагностики
                Log.Info("=== DEBUG: HTML PARSING ===");
                var totalRowMatch = Regex.Match(html, @"<tr[^>]*>.*?<td[^>]*>Total</td>.*?</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (totalRowMatch.Success)
                {
                    Log.Info($"DEBUG: Found Total row HTML: {totalRowMatch.Value}");
                }
                else
                {
                    Log.Warn("DEBUG: Could not find Total row in HTML");
                    // Шукаємо будь-які рядки з "Total"
                    var totalMatches = Regex.Matches(html, @".{0,100}Total.{0,100}", RegexOptions.IgnoreCase);
                    Log.Info($"DEBUG: Found {totalMatches.Count} occurrences of 'Total' in HTML:");
                    for (int i = 0; i < Math.Min(totalMatches.Count, 3); i++)
                    {
                        Log.Info($"DEBUG: Total occurrence {i + 1}: {totalMatches[i].Value}");
                    }
                }

                // Парсимо загальний винрейт з таблиці (Total row)
                // Нова структура: <tr><td>Total</td><td><span class="tag" style="..."><span class="basic-black-text">49.2</span></span></td><td>44551</td></tr>
                var totalWinRatePattern = @"<tr>\s*<td>Total</td>\s*<td>.*?<span[^>]*>\s*<span[^>]*>\s*(\d+\.?\d*)\s*</span>";
                var totalWinRateMatch = Regex.Match(html, totalWinRatePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (totalWinRateMatch.Success)
                {
                    Log.Info($"DEBUG: Primary pattern matched: {totalWinRateMatch.Groups[1].Value}");
                    if (double.TryParse(totalWinRateMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double winRate))
                    {
                        stats.WinRate = winRate;
                        Log.Info($"HSGuruService: Successfully parsed winrate: {winRate}%");
                    }
                    else
                    {
                        Log.Warn($"DEBUG: Failed to parse winrate value: '{totalWinRateMatch.Groups[1].Value}'");
                    }
                }
                else
                {
                    Log.Info("DEBUG: Primary pattern failed, trying alternative patterns...");
                    
                    // Пробуємо альтернативний патерн для нової структури HTML
                    var alternativePattern = @"<td>Total</td>\s*<td>.*?<span[^>]*basic-black-text[^>]*>\s*(\d+\.?\d*)\s*</span>";
                    var alternativeMatch = Regex.Match(html, alternativePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (alternativeMatch.Success)
                    {
                        Log.Info($"DEBUG: Alternative pattern matched: {alternativeMatch.Groups[1].Value}");
                        if (double.TryParse(alternativeMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double winRate))
                        {
                            stats.WinRate = winRate;
                            Log.Info($"HSGuruService: Successfully parsed winrate (alternative pattern): {winRate}%");
                        }
                        else
                        {
                            Log.Warn($"DEBUG: Failed to parse alternative winrate value: '{alternativeMatch.Groups[1].Value}'");
                        }
                    }
                    else
                    {
                        Log.Info("DEBUG: Alternative pattern also failed, trying more patterns...");
                        
                        // Ще один патерн - просто шукаємо числа після Total
                        var simplePattern = @"Total</td>.*?(\d+\.?\d*)";
                        var simpleMatch = Regex.Match(html, simplePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (simpleMatch.Success)
                        {
                            Log.Info($"DEBUG: Simple pattern found number: {simpleMatch.Groups[1].Value}");
                            // Перевіряємо чи це винрейт (має бути менше 100)
                            if (double.TryParse(simpleMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double possibleWinRate) && possibleWinRate <= 100)
                            {
                                stats.WinRate = possibleWinRate;
                                Log.Info($"HSGuruService: Successfully parsed winrate (simple pattern): {possibleWinRate}%");
                            }
                            else
                            {
                                Log.Warn($"DEBUG: Failed to parse simple winrate value: '{simpleMatch.Groups[1].Value}'");
                            }
                        }
                        else
                        {
                            Log.Warn("HSGuruService: All patterns failed to parse winrate from Total row");
                        }
                    }
                }

                // Парсимо загальну кількість ігор з Total row
                var totalGamesPattern = @"<tr>\s*<td>Total</td>.*?<td>(\d+)</td>\s*</tr>";
                var totalGamesMatch = Regex.Match(html, totalGamesPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (totalGamesMatch.Success)
                {
                    if (int.TryParse(totalGamesMatch.Groups[1].Value, out int games))
                    {
                        stats.TotalGames = games;
                    }
                }

                // Парсимо назву колоди з заголовка
                var titleMatch = Regex.Match(html, @"<div class=""title is-2"">([^<]+)</div>", RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    var deckName = titleMatch.Groups[1].Value.Trim();
                    // Видаляємо формат гри з кінця назви (Standard, Wild, Classic)
                    deckName = Regex.Replace(deckName, @"\s+(Standard|Wild|Classic)$", "", RegexOptions.IgnoreCase).Trim();
                    stats.DeckName = deckName;
                    Log.Info($"HSGuruService: Found deck name: {stats.DeckName}");
                }
                else
                {
                    // Альтернативний пошук в title тегу
                    var titleTagMatch = Regex.Match(html, @"<title>([^<]+)</title>", RegexOptions.IgnoreCase);
                    if (titleTagMatch.Success)
                    {
                        var fullTitle = titleTagMatch.Groups[1].Value.Trim();
                        // Видаляємо " - HSGuru" з кінця
                        if (fullTitle.EndsWith(" - HSGuru"))
                        {
                            var deckName = fullTitle.Substring(0, fullTitle.Length - " - HSGuru".Length).Trim();
                            // Видаляємо формат гри з кінця назви
                            deckName = Regex.Replace(deckName, @"\s+(Standard|Wild|Classic)$", "", RegexOptions.IgnoreCase).Trim();
                            stats.DeckName = deckName;
                            Log.Info($"HSGuruService: Found deck name from title: {stats.DeckName}");
                        }
                    }
                }

                // Парсимо матчапи проти класів
                ParseClassMatchups(html, stats);

                Log.Debug($"HSGuruService: Parsed - WinRate: {stats.WinRate}%, Games: {stats.TotalGames}");
                return stats.WinRate > 0 || stats.TotalGames > 0 ? stats : null;
            }
            catch (Exception ex)
            {
                Log.Error($"HSGuruService: Error parsing statistics: {ex.Message}");
                return null;
            }
        }

        private static void ParseClassMatchups(string html, DeckStatisticsInfo stats)
        {
            try
            {
                Log.Info("HSGuruService: Starting to parse class matchups...");
                
                // Новий патерн на основі реального HTML з логів
                // Шукаємо рядки з класами (не Total) та їх винрейти
                var matchupPattern = @"<td><span class=""tag player-name \w+""><span class=""basic-black-text"">([^<]+)</span></span></td>\s*<td>\s*<span[^>]*>\s*<span class=""basic-black-text"">\s*(\d+\.?\d*)\s*</span>";
                var matches = Regex.Matches(html, matchupPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                Log.Info($"HSGuruService: Found {matches.Count} potential matchup matches with new pattern");

                foreach (Match match in matches)
                {
                    var className = match.Groups[1].Value.Trim();
                    var winRateStr = match.Groups[2].Value.Trim();
                    
                    Log.Info($"HSGuruService: Processing matchup - Class: '{className}', WinRate: '{winRateStr}'");
                    
                    // Пропускаємо рядок Total
                    if (className.Equals("Total", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Info($"HSGuruService: Skipping Total row");
                        continue;
                    }
                    
                    if (double.TryParse(winRateStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double winRate))
                    {
                        // Конвертуємо назви класів до стандартного формату
                        var standardClassName = NormalizeClassName(className);
                        if (!string.IsNullOrEmpty(standardClassName))
                        {
                            stats.ClassMatchups[standardClassName] = winRate;
                            Log.Info($"HSGuruService: Added matchup - {standardClassName}: {winRate}%");
                        }
                        else
                        {
                            Log.Warn($"HSGuruService: Could not normalize class name: '{className}'");
                        }
                    }
                    else
                    {
                        Log.Warn($"HSGuruService: Could not parse win rate: '{winRateStr}'");
                    }
                }
                
                Log.Info($"HSGuruService: Successfully parsed {stats.ClassMatchups.Count} class matchups");
                
                // Логуємо всі знайдені матчапи
                foreach (var matchup in stats.ClassMatchups)
                {
                    Log.Info($"HSGuruService: Final matchup - {matchup.Key}: {matchup.Value}%");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"HSGuruService: Error parsing class matchups: {ex.Message}");
            }
        }

        private static string NormalizeClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
                return className;

            // Конвертуємо до стандартних назв класів Hearthstone
            switch (className.ToLower())
            {
                case "warrior": return "Warrior";
                case "shaman": return "Shaman";
                case "rogue": return "Rogue";
                case "paladin": return "Paladin";
                case "hunter": return "Hunter";
                case "druid": return "Druid";
                case "warlock": return "Warlock";
                case "mage": return "Mage";
                case "priest": return "Priest";
                case "demon hunter": return "DemonHunter";
                case "death knight": return "DeathKnight";
                default: return className;
            }
        }

        private static async Task TryGetArchetypeFromMeta(DeckStatisticsInfo stats)
        {
            try
            {
                if (string.IsNullOrEmpty(stats.DeckName))
                {
                    Log.Info("HSGuruService: No deck name available for archetype lookup");
                    return;
                }

                // Визначаємо формат для запиту (поки що використовуємо Standard за замовчуванням)
                var format = 2; // Standard
                var metaUrl = $"https://www.hsguru.com/meta?format={format}";
                
                Log.Info($"HSGuruService: Fetching meta data from {metaUrl} to find archetype for '{stats.DeckName}'");
                
                var metaResponse = await _httpClient.GetStringAsync(metaUrl);
                var archetype = ParseArchetypeFromMeta(metaResponse, stats.DeckName);
                
                if (archetype != null)
                {
                    stats.AverageTurns = archetype.AverageTurns;
                    stats.ArchetypeCategory = DetermineArchetypeCategory(archetype.AverageTurns);
                    Log.Info($"HSGuruService: Found archetype data - Turns: {stats.AverageTurns}, Category: {stats.ArchetypeCategory}");
                }
                else
                {
                    Log.Info($"HSGuruService: Could not find archetype data for '{stats.DeckName}'");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"HSGuruService: Error getting archetype from meta: {ex.Message}");
            }
        }

        private static ArchetypeInfo ParseArchetypeFromMeta(string html, string deckName)
        {
            try
            {
                // Шукаємо рядок таблиці з назвою архетипу
                var pattern = $@"<td[^>]*class=""decklist-info[^>]*>\s*<a[^>]*href=""/archetype/[^""]*""[^>]*>\s*{Regex.Escape(deckName)}\s*</a>\s*</td>.*?<td[^>]*>([\d.]+)</td>";
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double turns))
                    {
                        Log.Info($"HSGuruService: Found exact match for '{deckName}' with {turns} average turns");
                        return new ArchetypeInfo { AverageTurns = turns };
                    }
                }
                
                // Якщо точне співпадіння не знайдено, спробуємо часткове
                var partialPattern = @"<td[^>]*class=""decklist-info[^>]*>\s*<a[^>]*href=""/archetype/[^""]*""[^>]*>\s*([^<]+)\s*</a>\s*</td>.*?<td[^>]*>([\d.]+)</td>";
                var matches = Regex.Matches(html, partialPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                foreach (Match partialMatch in matches)
                {
                    var archetypeName = partialMatch.Groups[1].Value.Trim();
                    var turnsStr = partialMatch.Groups[2].Value.Trim();
                    
                    // Перевіряємо чи містить назва архетипу частину назви нашої колоди
                    if (archetypeName.IndexOf(deckName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        deckName.IndexOf(archetypeName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (double.TryParse(turnsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double turns))
                        {
                            Log.Info($"HSGuruService: Found partial match '{archetypeName}' for '{deckName}' with {turns} average turns");
                            return new ArchetypeInfo { AverageTurns = turns };
                        }
                    }
                }
                
                Log.Info($"HSGuruService: No archetype match found for '{deckName}'");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"HSGuruService: Error parsing archetype from meta: {ex.Message}");
                return null;
            }
        }

        private static string DetermineArchetypeCategory(double averageTurns)
        {
            if (averageTurns <= 0)
                return null;
                
            if (averageTurns <= 7.0)
                return "Aggro";
            else if (averageTurns <= 9.0)
                return "Midrange";
            else
                return "Control/Combo";
        }

        private class ArchetypeInfo
        {
            public double AverageTurns { get; set; }
        }

        public static void ClearCache()
        {
            _cache.Clear();
            Log.Info("HSGuruService: Cache cleared");
        }
        
        // DEBUG: Метод для очищення кешу конкретної колоди
        public static void ClearDeckCache(string deckCode)
        {
            if (_cache.ContainsKey(deckCode))
            {
                _cache.Remove(deckCode);
                Log.Info($"HSGuruService: Cleared cache for deck: {deckCode}");
            }
            else
            {
                Log.Info($"HSGuruService: No cache entry found for deck: {deckCode}");
            }
        }
    }
}