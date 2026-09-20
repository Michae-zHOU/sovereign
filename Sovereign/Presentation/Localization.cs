using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sovereign.Presentation;

/// <summary>Presentation-only Simplified Chinese localization; save IDs and simulation rules remain invariant.</summary>
public static class Localization
{
    private sealed record Template(Regex Pattern, string Translation, string[] Tokens);
    private static readonly Dictionary<string,string> Text = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Template> Templates = new();
    private static KeyValuePair<string,string>[] _phrases = Array.Empty<KeyValuePair<string,string>>();
    private static Regex? _phrasePattern;
    private static bool _loaded;
    public static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

    public static void Initialize()
    {
        if (_loaded) return;
        foreach (string group in new[] { "ui", "places", "simulation", "history", "qing" })
        {
            string path = $"res://Assets/localization/zh_CN_{group}.json";
            if (!Godot.FileAccess.FileExists(path)) throw new InvalidOperationException("Missing Chinese language resource: " + path);
            var values = JsonSerializer.Deserialize<Dictionary<string,string>>(Godot.FileAccess.GetFileAsString(path))!;
            foreach (var pair in values) Text[pair.Key] = pair.Value;
        }
        foreach (var pair in Text.Where(p => p.Key.Contains('{')).OrderByDescending(p => p.Key.Length))
        {
            var matches = Regex.Matches(pair.Key, @"\{[^{}]+\}");
            if (matches.Count == 0) continue;
            var pattern = new StringBuilder("^"); int position = 0;
            foreach (Match match in matches)
            {
                pattern.Append(Regex.Escape(pair.Key[position..match.Index]));
                pattern.Append("(.+?)"); position = match.Index + match.Length;
            }
            pattern.Append(Regex.Escape(pair.Key[position..])); pattern.Append('$');
            Templates.Add(new Template(new Regex(pattern.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline), pair.Value, matches.Select(m => m.Value).ToArray()));
        }
        _phrases = Text.Where(p => !p.Key.Contains('{') && p.Key.Length > 1).OrderByDescending(p => p.Key.Length).ToArray();
        _phrasePattern = new Regex(@"(?<![A-Za-z])(?:" + string.Join("|", _phrases.Select(p => Regex.Escape(p.Key))) + @")(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        _loaded = true;
    }

    public static string Tr(string? value) => Translate(value ?? "", 0);
    private static string Translate(string value, int depth)
    {
        if (value.Length == 0) return value;
        if (!_loaded) Initialize();
        if (Text.TryGetValue(value, out var exact)) return exact;
        // Journal records retain their invariant date and original wording in saves.
        // Translate the message separately so full-sentence templates still match.
        if (value.Length > 14 && value.Substring(11, 3) == " · " &&
            DateTime.TryParseExact(value[..11], "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var logDate))
            return Date(logDate) + " · " + Translate(value[14..], depth);
        if (depth < 2)
        foreach (var template in Templates)
        {
            var match = template.Pattern.Match(value);
            if (!match.Success) continue;
            string translated = template.Translation;
            for (int i = 0; i < template.Tokens.Length; i++) translated = translated.Replace(template.Tokens[i], Translate(match.Groups[i + 1].Value, depth + 1), StringComparison.Ordinal);
            return translated;
        }
        string result = _phrasePattern!.Replace(value, match => Text[match.Value]);
        result = Regex.Replace(result, @"(?<=\d)d\b", "天");
        return result;
    }

    public static string Date(DateTime date) => date.ToString("yyyy年M月d日", Chinese);
    public static string Number(decimal value) => Math.Abs(value) >= 100_000_000m ? $"{value / 100_000_000m:0.00}亿" : Math.Abs(value) >= 10_000m ? $"{value / 10_000m:0.0}万" : value.ToString("0", CultureInfo.InvariantCulture);
}
