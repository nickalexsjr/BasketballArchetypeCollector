using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace BasketballArchetypeCollector.Services;

/// <summary>
/// Centralized service for all mini-game logic.
/// Handles cooldowns, prize calculations, and game state.
/// Uses Open Trivia DB API for sports questions.
/// </summary>
public class MiniGameService
{
    private readonly GameStateService _gameStateService;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    // Open Trivia DB API - Category 21 = Sports (free, no auth required)
    private const string TriviaApiUrl = "https://opentdb.com/api.php?amount=5&category=21&type=multiple";

    // Cooldown durations in hours
    public const double LuckySpinCooldownHours = 3;
    public const double MysteryBoxCooldownHours = 6;
    public const double CoinFlipCooldownHours = 4;
    public const double TriviaCooldownHours = 16;

    // Lucky Spin prize tiers (coins, weight)
    private readonly (int coins, int weight)[] _luckySpinPrizes = new[]
    {
        (50, 40),    // 40% chance
        (100, 30),   // 30% chance
        (250, 18),   // 18% chance
        (500, 9),    // 9% chance
        (1000, 3)    // 3% chance
    };

    // Mystery Box prize tiers
    private readonly (int coins, int weight)[] _mysteryBoxPrizes = new[]
    {
        (100, 35),   // 35% chance
        (200, 25),   // 25% chance
        (300, 20),   // 20% chance
        (500, 12),   // 12% chance
        (750, 5),    // 5% chance
        (2000, 3)    // 3% chance - jackpot!
    };


    public MiniGameService(GameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    #region Cooldown Checks

    public (bool canPlay, string status) CheckLuckySpinCooldown()
    {
        return CheckCooldown(_gameStateService.CurrentState.LastLuckySpinUtc, LuckySpinCooldownHours);
    }

    public (bool canPlay, string status) CheckMysteryBoxCooldown()
    {
        return CheckCooldown(_gameStateService.CurrentState.LastMysteryBoxUtc, MysteryBoxCooldownHours);
    }

    public (bool canPlay, string status) CheckCoinFlipCooldown()
    {
        return CheckCooldown(_gameStateService.CurrentState.LastCoinFlipUtc, CoinFlipCooldownHours);
    }

    public (bool canPlay, string status) CheckTriviaCooldown()
    {
        return CheckCooldown(_gameStateService.CurrentState.LastTriviaUtc, TriviaCooldownHours);
    }

    private (bool canPlay, string status) CheckCooldown(DateTime? lastPlayUtc, double cooldownHours)
    {
        if (lastPlayUtc == null)
            return (true, "Ready to play!");

        var hoursSince = (DateTime.UtcNow - lastPlayUtc.Value).TotalHours;
        if (hoursSince >= cooldownHours)
            return (true, "Ready to play!");

        var remaining = cooldownHours - hoursSince;
        return (false, $"Available in {FormatTime(remaining)}");
    }

    private string FormatTime(double hours)
    {
        if (hours < 1)
            return $"{(int)(hours * 60)}m";
        return $"{(int)hours}h {(int)((hours % 1) * 60)}m";
    }

    #endregion

    #region Lucky Spin

    public int RollLuckySpin()
    {
        return RollWeightedPrize(_luckySpinPrizes);
    }

    public async Task<int> CompleteLuckySpin()
    {
        int prize = RollLuckySpin();
        await _gameStateService.AddCoins(prize);
        _gameStateService.CurrentState.LastLuckySpinUtc = DateTime.UtcNow;
        await _gameStateService.SaveStateAsync();
        return prize;
    }

    #endregion

    #region Mystery Box

    public int[] GenerateMysteryBoxPrizes(int boxCount = 6)
    {
        var prizes = new int[boxCount];
        for (int i = 0; i < boxCount; i++)
        {
            prizes[i] = RollWeightedPrize(_mysteryBoxPrizes);
        }
        return prizes;
    }

    public async Task<int> CompleteMysteryBox(int prize)
    {
        await _gameStateService.AddCoins(prize);
        _gameStateService.CurrentState.LastMysteryBoxUtc = DateTime.UtcNow;
        await _gameStateService.SaveStateAsync();
        return prize;
    }

    #endregion

    #region Coin Flip

    public const int MinBet = 25;
    public const int MaxBet = 2500;
    public const int BetStep = 25;

    public bool CanAffordBet(int amount)
    {
        return _gameStateService.CurrentState.Coins >= amount;
    }

    public int GetCurrentCoins()
    {
        return _gameStateService.CurrentState.Coins;
    }

    public (bool isHeads, bool won) FlipCoin(bool choseHeads)
    {
        bool isHeads = _random.Next(2) == 0;
        bool won = (choseHeads && isHeads) || (!choseHeads && !isHeads);
        return (isHeads, won);
    }

    public async Task<int> CompleteCoinFlip(int betAmount, bool won)
    {
        int result;
        if (won)
        {
            // Win: gain the bet amount (net +bet since we're adding, not subtracting first)
            await _gameStateService.AddCoins(betAmount);
            result = betAmount;
        }
        else
        {
            // Lose: subtract the bet
            _gameStateService.CurrentState.Coins -= betAmount;
            result = -betAmount;
        }

        _gameStateService.CurrentState.LastCoinFlipUtc = DateTime.UtcNow;
        await _gameStateService.SaveStateAsync();
        return result;
    }

    #endregion

    #region Trivia

    public const int TriviaQuestionCount = 5;
    public const int PointsPerCorrectAnswer = 100;

    /// <summary>
    /// Fetches sports trivia questions from Open Trivia DB API.
    /// Throws exception if API fails - no hardcoded fallback.
    /// </summary>
    public async Task<List<TriviaQuestion>> GetRandomTriviaQuestionsAsync()
    {
        var response = await _httpClient.GetStringAsync(TriviaApiUrl);
        var jsonDoc = JsonDocument.Parse(response);
        var root = jsonDoc.RootElement;

        // Check response code (0 = success)
        var responseCode = root.GetProperty("response_code").GetInt32();
        if (responseCode != 0)
        {
            throw new Exception($"Trivia API returned error code: {responseCode}");
        }

        var results = root.GetProperty("results");
        var questions = new List<TriviaQuestion>();

        foreach (var item in results.EnumerateArray())
        {
            // Decode HTML entities in question and answers
            var questionText = DecodeHtml(item.GetProperty("question").GetString() ?? "");
            var correctAnswer = DecodeHtml(item.GetProperty("correct_answer").GetString() ?? "");

            var incorrectAnswers = item.GetProperty("incorrect_answers")
                .EnumerateArray()
                .Select(a => DecodeHtml(a.GetString() ?? ""))
                .ToList();

            // Combine all answers with correct at index 0
            var allAnswers = new List<string> { correctAnswer };
            allAnswers.AddRange(incorrectAnswers);

            questions.Add(new TriviaQuestion(questionText, allAnswers.ToArray(), 0));
        }

        if (questions.Count < TriviaQuestionCount)
        {
            throw new Exception($"API returned only {questions.Count} questions, need {TriviaQuestionCount}");
        }

        return questions.Take(TriviaQuestionCount).ToList();
    }

    /// <summary>
    /// Synchronous wrapper - prefer async version.
    /// </summary>
    public List<TriviaQuestion> GetRandomTriviaQuestions()
    {
        return GetRandomTriviaQuestionsAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Decodes HTML entities like &quot; &amp; etc.
    /// </summary>
    private string DecodeHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        return HttpUtility.HtmlDecode(html);
    }

    public (string[] shuffledAnswers, int correctIndex) ShuffleAnswers(TriviaQuestion question)
    {
        var indexed = question.Answers.Select((a, i) => (answer: a, originalIndex: i)).ToList();
        var shuffled = indexed.OrderBy(_ => _random.Next()).ToList();
        var newCorrectIndex = shuffled.FindIndex(x => x.originalIndex == question.CorrectIndex);
        return (shuffled.Select(x => x.answer).ToArray(), newCorrectIndex);
    }

    public async Task<int> CompleteTrivia(int score)
    {
        if (score > 0)
        {
            await _gameStateService.AddCoins(score);
        }
        _gameStateService.CurrentState.LastTriviaUtc = DateTime.UtcNow;
        await _gameStateService.SaveStateAsync();
        return score;
    }

    #endregion

    #region Helpers

    private int RollWeightedPrize((int coins, int weight)[] prizes)
    {
        int totalWeight = prizes.Sum(p => p.weight);
        int roll = _random.Next(totalWeight);
        int cumulative = 0;

        foreach (var (coins, weight) in prizes)
        {
            cumulative += weight;
            if (roll < cumulative)
                return coins;
        }

        return prizes[0].coins;
    }

    #endregion
}

public class TriviaQuestion
{
    public string Question { get; }
    public string[] Answers { get; }
    public int CorrectIndex { get; }

    public TriviaQuestion(string question, string[] answers, int correctIndex)
    {
        Question = question;
        Answers = answers;
        CorrectIndex = correctIndex;
    }
}
