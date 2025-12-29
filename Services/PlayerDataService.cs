using System.Reflection;
using BasketballArchetypeCollector.Helpers;
using BasketballArchetypeCollector.Models;

namespace BasketballArchetypeCollector.Services;

public class PlayerDataService
{
    private List<Player> _players = new();
    private DatabaseMeta _meta = new();
    private bool _isLoaded;

    public IReadOnlyList<Player> Players => _players;
    public DatabaseMeta Meta => _meta;
    public bool IsLoaded => _isLoaded;

    public async Task LoadPlayersAsync()
    {
        if (_isLoaded) return;

        try
        {
            System.Diagnostics.Debug.WriteLine("[PlayerDataService] Loading players from embedded CSV...");

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "BasketballArchetypeCollector.Resources.Data.player_career_stats.csv";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerDataService] Resource not found: {resourceName}");
                throw new Exception($"Could not find embedded resource: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            var csvContent = await reader.ReadToEndAsync();
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                throw new Exception("CSV file is empty or has no data rows");
            }

            // Parse header
            var header = ParseCsvLine(lines[0]);
            var headerIndex = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++)
            {
                headerIndex[header[i].Trim().ToLowerInvariant()] = i;
            }

            var players = new List<Player>();
            int withStats = 0;
            int active = 0;

            // Parse data rows
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    var values = ParseCsvLine(line);
                    var player = ParsePlayer(values, headerIndex);

                    // Calculate overall rating
                    player.Overall = OverallCalculator.Calculate(
                        player.FirstName, player.LastName,
                        player.Ppg, player.Rpg, player.Apg,
                        player.Spg, player.Bpg, player.FgPct,
                        player.Games, player.DraftRound, player.DraftNumber);

                    // Set sort tiebreaker
                    player.SortTiebreaker = OverallCalculator.GetSortTiebreaker(player.FirstName, player.LastName);

                    // Determine rarity and era
                    player.Rarity = RarityConfig.DetermineRarity(player.Overall, player.FirstName, player.LastName);
                    player.Era = EraConfig.GetEra(player.DraftYear);

                    // Check if has stats
                    player.HasStats = player.ScrapeStatus == "found" && player.Games > 0;
                    if (player.HasStats) withStats++;

                    // Check if active
                    player.IsActive = !string.IsNullOrEmpty(player.TeamId);
                    if (player.IsActive) active++;

                    players.Add(player);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayerDataService] Error parsing line {i}: {ex.Message}");
                }
            }

            // Sort players: overall desc, tiebreaker desc, ppg desc
            players.Sort((a, b) =>
            {
                if (b.Overall != a.Overall) return b.Overall.CompareTo(a.Overall);
                if (b.SortTiebreaker != a.SortTiebreaker) return b.SortTiebreaker.CompareTo(a.SortTiebreaker);
                return b.Ppg.CompareTo(a.Ppg);
            });

            // Assign ranks (only to players with stats)
            int rank = 1;
            foreach (var player in players)
            {
                if (player.HasStats)
                {
                    player.Rank = rank++;
                }
            }

            _players = players;
            _meta = new DatabaseMeta
            {
                TotalPlayers = players.Count,
                PlayersWithStats = withStats,
                ActivePlayers = active
            };

            _isLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[PlayerDataService] Loaded {players.Count} players ({withStats} with stats)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerDataService] Error loading players: {ex.Message}");
            throw;
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var field = new System.Text.StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }
        result.Add(field.ToString());

        return result.ToArray();
    }

    private static Player ParsePlayer(string[] values, Dictionary<string, int> headerIndex)
    {
        string GetValue(string key) =>
            headerIndex.TryGetValue(key, out var idx) && idx < values.Length
                ? values[idx].Trim()
                : string.Empty;

        float ParseFloat(string key)
        {
            var val = GetValue(key);
            return float.TryParse(val, out var f) ? f : 0f;
        }

        int ParseInt(string key)
        {
            var val = GetValue(key);
            return int.TryParse(val, out var i) ? i : 0;
        }

        return new Player
        {
            Id = GetValue("id"),
            FirstName = GetValue("first_name"),
            LastName = GetValue("last_name"),
            TeamId = GetValue("team_id"),
            TeamAbbreviation = GetValue("team_abbreviation"),
            Position = GetValue("position"),
            Height = GetValue("height"),
            DraftYear = GetValue("draft_year"),
            DraftRound = GetValue("draft_round"),
            DraftNumber = GetValue("draft_number"),
            Games = ParseInt("games"),
            Ppg = ParseFloat("ppg"),
            Rpg = ParseFloat("rpg"),
            Apg = ParseFloat("apg"),
            Spg = ParseFloat("spg"),
            Bpg = ParseFloat("bpg"),
            FgPct = ParseFloat("fg_pct"),
            ScrapeStatus = GetValue("scrape_status")
        };
    }

    public Player? GetPlayerById(string id)
    {
        return _players.FirstOrDefault(p => p.Id == id);
    }

    public List<Player> GetPlayersByRarity(Rarity rarity)
    {
        return _players.Where(p => p.Rarity == rarity).ToList();
    }

    public List<Player> SearchPlayers(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _players.ToList();

        var lowerQuery = query.ToLowerInvariant();
        return _players.Where(p =>
            p.FullName.ToLowerInvariant().Contains(lowerQuery) ||
            p.TeamAbbreviation.ToLowerInvariant().Contains(lowerQuery))
            .ToList();
    }
}
