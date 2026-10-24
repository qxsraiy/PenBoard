using System.Globalization;
using System.Numerics;
using System.Windows.Ink;
using Windows.UI.Input.Inking;

namespace PenBoard.Services;

public enum RecognitionLanguage
{
    Chinese,
    English
}

public sealed class InkRecognitionService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InkRecognizerContainer? _recognizer;
    private IReadOnlyList<InkRecognizer> _recognizers = Array.Empty<InkRecognizer>();
    private RecognitionLanguage _language = RecognitionLanguage.Chinese;
    private RecognitionLanguage? _pendingLanguage;

    public InkRecognitionService()
    {
        try
        {
            _recognizer = new InkRecognizerContainer();
            _recognizers = _recognizer.GetRecognizers().ToList();
            SetLanguage(RecognitionLanguage.Chinese);
        }
        catch (Exception ex)
        {
            _recognizer = null;
            Logger.Log("InkRecognitionService: " + ex);
        }
    }

    public RecognitionLanguage Language => _language;

    public string CurrentRecognizerName { get; private set; } = "";

    public bool IsAvailable => _recognizer is not null && _recognizers.Count > 0;

    public bool SetLanguage(RecognitionLanguage language)
    {
        _language = language;
        if (!_gate.Wait(0))
        {
            _pendingLanguage = language;
            Logger.Log($"SetLanguage: recognition busy, deferring {language}.");
            return true;
        }

        try
        {
            return SetLanguageCore(language);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool SetLanguageCore(RecognitionLanguage language)
    {
        _language = language;
        if (_recognizer is null || _recognizers.Count == 0)
        {
            Logger.Log("SetLanguage: no recognizers installed.");
            return false;
        }

        var preferred = language == RecognitionLanguage.Chinese
            ? _recognizers.FirstOrDefault(r =>
                r.Name.Contains("中文", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains("Chinese", StringComparison.OrdinalIgnoreCase))
            : _recognizers.FirstOrDefault(r =>
                r.Name.Contains("English (US)", StringComparison.OrdinalIgnoreCase))
              ?? _recognizers.FirstOrDefault(r =>
                  r.Name.Contains("English", StringComparison.OrdinalIgnoreCase));

        if (preferred is null)
        {
            Logger.Log($"SetLanguage: no {language} recognizer. Available: {string.Join(", ", _recognizers.Select(r => r.Name))}");
            return false;
        }

        _recognizer.SetDefaultRecognizer(preferred);
        CurrentRecognizerName = preferred.Name;
        Logger.Log($"Recognizer selected: {preferred.Name} ({language})");
        return true;
    }

    public IReadOnlyList<string> GetRecognizerNames()
    {
        try
        {
            return _recognizers.Select(r => r.Name).ToList();
        }
        catch (Exception ex)
        {
            Logger.Log("GetRecognizerNames: " + ex.Message);
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<string>> RecognizeAsync(StrokeCollection strokes)
    {
        if (strokes.Count == 0 || _recognizer is null)
        {
            return Array.Empty<string>();
        }

        // The WinRT recognizer is not agile and rejects overlapping calls with
        // "Operation is not valid due to the current state of the object".
        // Serialize recognition and language changes.
        await _gate.WaitAsync();
        try
        {
            return await RecognizeCoreAsync(strokes);
        }
        finally
        {
            if (_pendingLanguage is { } pending)
            {
                _pendingLanguage = null;
                SetLanguageCore(pending);
            }
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<string>> RecognizeCoreAsync(StrokeCollection strokes)
    {
        try
        {
            var container = new InkStrokeContainer();
            var builder = new InkStrokeBuilder();
            foreach (var stroke in strokes)
            {
                if (stroke.StylusPoints.Count == 0)
                {
                    continue;
                }

                var points = new List<InkPoint>(stroke.StylusPoints.Count);
                foreach (var point in stroke.StylusPoints)
                {
                    points.Add(new InkPoint(
                        new Windows.Foundation.Point(point.X, point.Y),
                        point.PressureFactor));
                }

                var inkStroke = builder.CreateStrokeFromInkPoints(points, Matrix3x2.Identity);
                container.AddStroke(inkStroke);
            }

            if (container.GetStrokes().Count == 0)
            {
                return Array.Empty<string>();
            }

            var results = await _recognizer!.RecognizeAsync(container, InkRecognitionTarget.All);
            var candidates = new List<string>();
            if (results is not null)
            {
                foreach (var result in results)
                {
                    candidates.AddRange(result.GetTextCandidates());
                }
            }

            return candidates
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => RecognitionTextNormalizer.Normalize(c, _language))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Log("RecognizeAsync: " + ex);

            // If a previous recognition got into a bad state, rebuild the
            // container so the next attempt can recover without restarting.
            if (ex is InvalidOperationException)
            {
                RecreateRecognizerCore();
            }

            return Array.Empty<string>();
        }
    }

    private void RecreateRecognizerCore()
    {
        try
        {
            _recognizer = new InkRecognizerContainer();
            _recognizers = _recognizer.GetRecognizers().ToList();
            SetLanguageCore(_language);
            Logger.Log("Recognizer recreated after invalid state.");
        }
        catch (Exception ex)
        {
            Logger.Log("RecreateRecognizer: " + ex);
        }
    }

    public string Describe()
    {
        var names = GetRecognizerNames();
        var culture = CultureInfo.CurrentUICulture.Name;
        return $"culture={culture}; language={_language}; current={CurrentRecognizerName}; recognizers=[{string.Join(", ", names)}]";
    }
}
