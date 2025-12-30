using BasketballArchetypeCollector.Services;

namespace BasketballArchetypeCollector.Views.Popups;

public partial class TriviaPopup : ContentPage
{
    private readonly MiniGameService _miniGameService;

    private int _currentQuestion = 0;
    private int _score = 0;
    private int _correctAnswerIndex = 0;
    private bool _isAnswering = false;
    private bool _gameEnded = false;

    private List<TriviaQuestion> _gameQuestions = new();

    public TriviaPopup(MiniGameService miniGameService)
    {
        InitializeComponent();
        _miniGameService = miniGameService;

        // Load questions asynchronously
        LoadQuestionsAsync();
    }

    private async void LoadQuestionsAsync()
    {
        try
        {
            QuestionLabel.Text = "Loading sports trivia...";

            // Disable answer buttons while loading
            SetAnswerButtonsEnabled(false);

            // Fetch questions from API (or fallback)
            _gameQuestions = await _miniGameService.GetRandomTriviaQuestionsAsync();

            LoadQuestion();
        }
        catch
        {
            // If all else fails, use sync fallback
            _gameQuestions = _miniGameService.GetRandomTriviaQuestions();
            LoadQuestion();
        }
    }

    private void SetAnswerButtonsEnabled(bool enabled)
    {
        Answer1Btn.IsEnabled = enabled;
        Answer2Btn.IsEnabled = enabled;
        Answer3Btn.IsEnabled = enabled;
        Answer4Btn.IsEnabled = enabled;
    }

    private void LoadQuestion()
    {
        // Re-enable buttons when loading a question
        SetAnswerButtonsEnabled(true);

        if (_currentQuestion >= MiniGameService.TriviaQuestionCount)
        {
            EndGame();
            return;
        }

        var q = _gameQuestions[_currentQuestion];

        QuestionNumLabel.Text = $"Q{_currentQuestion + 1}/{MiniGameService.TriviaQuestionCount}";
        QuestionLabel.Text = q.Question;

        // Shuffle answers via service
        var (shuffledAnswers, correctIdx) = _miniGameService.ShuffleAnswers(q);
        _correctAnswerIndex = correctIdx;

        Answer1Btn.Text = shuffledAnswers[0];
        Answer2Btn.Text = shuffledAnswers[1];
        Answer3Btn.Text = shuffledAnswers[2];
        Answer4Btn.Text = shuffledAnswers[3];

        // Reset button colors
        ResetAnswerButtons();

        ActionButton.Text = "Skip Question";
        _isAnswering = false;
    }

    private void ResetAnswerButtons()
    {
        var buttons = new[] { Answer1Btn, Answer2Btn, Answer3Btn, Answer4Btn };
        foreach (var btn in buttons)
        {
            btn.BackgroundColor = Color.FromArgb("#1e3a5f");
            btn.TextColor = Color.FromArgb("#7dd3fc");
            btn.IsEnabled = true;
        }
    }

    private async void OnAnswerClicked(object? sender, EventArgs e)
    {
        if (_isAnswering || _gameEnded) return;
        if (sender is not Button clickedBtn) return;

        _isAnswering = true;

        var buttons = new[] { Answer1Btn, Answer2Btn, Answer3Btn, Answer4Btn };
        int selectedIndex = Array.IndexOf(buttons, clickedBtn);

        // Disable all buttons
        foreach (var btn in buttons)
        {
            btn.IsEnabled = false;
        }

        // Highlight correct answer
        buttons[_correctAnswerIndex].BackgroundColor = Color.FromArgb("#22c55e");
        buttons[_correctAnswerIndex].TextColor = Colors.White;

        if (selectedIndex == _correctAnswerIndex)
        {
            // Correct!
            _score += MiniGameService.PointsPerCorrectAnswer;
            ScoreLabel.Text = _score.ToString();
            SubtitleLabel.Text = "Correct! 🎉";
            SubtitleLabel.TextColor = Color.FromArgb("#22c55e");
        }
        else
        {
            // Wrong
            clickedBtn.BackgroundColor = Color.FromArgb("#dc2626");
            clickedBtn.TextColor = Colors.White;
            SubtitleLabel.Text = "Wrong! 😢";
            SubtitleLabel.TextColor = Color.FromArgb("#f87171");
        }

        await Task.Delay(1200);

        _currentQuestion++;
        SubtitleLabel.Text = "Answer correctly to win coins!";
        SubtitleLabel.TextColor = Color.FromArgb("#94a3b8");

        LoadQuestion();
    }

    private async void OnActionClicked(object sender, EventArgs e)
    {
        if (_gameEnded)
        {
            await Navigation.PopModalAsync();
            return;
        }

        // Skip question
        _currentQuestion++;
        LoadQuestion();
    }

    private async void EndGame()
    {
        _gameEnded = true;

        // Hide question UI
        AnswerStack.IsVisible = false;

        // Complete trivia via service
        await _miniGameService.CompleteTrivia(_score);

        // Show results
        ResultStack.IsVisible = true;

        int correctCount = _score / MiniGameService.PointsPerCorrectAnswer;

        if (correctCount == MiniGameService.TriviaQuestionCount)
        {
            ResultEmoji.Text = "🏆";
            ResultLabel.Text = "PERFECT!";
            ResultLabel.TextColor = Color.FromArgb("#fbbf24");
        }
        else if (correctCount >= 3)
        {
            ResultEmoji.Text = "🎉";
            ResultLabel.Text = "Great Job!";
            ResultLabel.TextColor = Color.FromArgb("#22c55e");
        }
        else if (correctCount > 0)
        {
            ResultEmoji.Text = "👍";
            ResultLabel.Text = "Not Bad!";
            ResultLabel.TextColor = Color.FromArgb("#3b82f6");
        }
        else
        {
            ResultEmoji.Text = "📚";
            ResultLabel.Text = "Study Up!";
            ResultLabel.TextColor = Color.FromArgb("#94a3b8");
        }

        ResultDetailsLabel.Text = $"{correctCount}/{MiniGameService.TriviaQuestionCount} correct = +{_score} coins";
        QuestionLabel.Text = "Game Over!";

        ActionButton.Text = "Collect & Close";
        ActionButton.BackgroundColor = Color.FromArgb("#22c55e");
        ActionButton.TextColor = Colors.White;
    }
}
