using System.Text;

namespace PenBoard.Services;

public static class RecognitionTextNormalizer
{
    public static string Normalize(string text, RecognitionLanguage language)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var hasCjk = text.Any(IsCjk);
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (language == RecognitionLanguage.Chinese && hasCjk)
            {
                if ((ch == '.' || ch == ',') &&
                    i > 0 && i + 1 < text.Length &&
                    char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                {
                    // Keep decimal separators and thousands separators inside numbers.
                    builder.Append(ch);
                    continue;
                }

                builder.Append(ToChinesePunctuation(ch));
            }
            else if (language == RecognitionLanguage.English)
            {
                builder.Append(ToEnglishPunctuation(ch));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Trim();
    }

    private static bool IsCjk(char ch)
        => ch is >= '\u3400' and <= '\u9FFF'
           or >= '\uF900' and <= '\uFAFF';

    private static char ToChinesePunctuation(char ch) => ch switch
    {
        ',' => '，',
        '.' => '。',
        '!' => '！',
        '?' => '？',
        ':' => '：',
        ';' => '；',
        '(' => '（',
        ')' => '）',
        '[' => '【',
        ']' => '】',
        _ => ch
    };

    private static char ToEnglishPunctuation(char ch) => ch switch
    {
        '，' => ',',
        '。' => '.',
        '！' => '!',
        '？' => '?',
        '：' => ':',
        '；' => ';',
        '（' => '(',
        '）' => ')',
        '【' => '[',
        '】' => ']',
        '、' => ',',
        '“' or '”' => '"',
        '‘' or '’' => '\'',
        _ => ch
    };
}
