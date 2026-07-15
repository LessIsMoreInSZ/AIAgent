using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Common;

public sealed record ChatMessage(string Role, string Content);

public sealed record ChatRequest(string Model, IReadOnlyList<ChatMessage> Messages, bool Stream);

public sealed record ChatResponse(ChatMessage? Message, string? Error);

public sealed class OllamaChatClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaChatClient(string? model = null, string? baseUrl = null)
    {
        _model = model
            ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL")
            ?? "qwen3:0.6b";

        var url = baseUrl
            ?? Environment.GetEnvironmentVariable("OLLAMA_URL")
            ?? "http://localhost:11434";

        _http = new HttpClient
        {
            BaseAddress = new Uri(url),
            Timeout = TimeSpan.FromMinutes(3)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public string Model => _model;

    public async Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages)
    {
        var request = new ChatRequest(_model, messages, false);
        using var response = await _http.PostAsJsonAsync("/api/chat", request, _jsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        var chat = JsonSerializer.Deserialize<ChatResponse>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Ollama returned an empty response.");

        if (!string.IsNullOrWhiteSpace(chat.Error))
        {
            throw new InvalidOperationException(chat.Error);
        }

        return chat.Message?.Content?.Trim()
            ?? throw new InvalidOperationException("Ollama returned no message content.");
    }

    public void Dispose() => _http.Dispose();
}

public sealed class AgentCommand
{
    public required string Type { get; init; }
    public string? Tool { get; init; }
    public string? Answer { get; init; }
    public JsonElement Args { get; init; } = JsonElementFactory.EmptyObject();

    public static AgentCommand? TryParse(string text)
    {
        var json = ExtractJsonObject(text);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            root.TryGetProperty("args", out var args);

            return new AgentCommand
            {
                Type = type.GetString()?.Trim().ToLowerInvariant() ?? "",
                Tool = root.TryGetProperty("tool", out var tool) ? tool.GetString() : null,
                Answer = root.TryGetProperty("answer", out var answer) ? answer.GetString() : null,
                Args = args.ValueKind == JsonValueKind.Undefined ? JsonElementFactory.EmptyObject() : args.Clone()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string text)
    {
        var cleaned = text.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "")
            .Trim();

        var start = cleaned.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < cleaned.Length; i++)
        {
            var c = cleaned[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return cleaned[start..(i + 1)];
                }
            }
        }

        return null;
    }
}

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ToolDefinition> _tools = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ToolDefinition> Tools => _tools.Values;

    public void Register(string name, string description, string argsShape, Func<JsonElement, Task<string>> handler)
    {
        _tools[name] = new ToolDefinition(name, description, argsShape, handler);
    }

    public async Task<string> InvokeAsync(string name, JsonElement args)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            return JsonSerializer.Serialize(new { ok = false, error = $"Unknown tool: {name}" });
        }

        try
        {
            var result = await tool.Handler(args);
            return JsonSerializer.Serialize(new { ok = true, result });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = ex.Message });
        }
    }

    public string ToPromptBlock()
    {
        var builder = new StringBuilder();

        foreach (var tool in _tools.Values.OrderBy(x => x.Name))
        {
            builder.AppendLine($"- {tool.Name}: {tool.Description}. args: {tool.ArgsShape}");
        }

        return builder.ToString().TrimEnd();
    }
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    string ArgsShape,
    Func<JsonElement, Task<string>> Handler);

public static class DemoToolkits
{
    public static ToolRegistry BasicTools(string notesRoot)
    {
        Directory.CreateDirectory(notesRoot);

        var tools = new ToolRegistry();
        var notes = new FileNoteStore(notesRoot);

        tools.Register("now", "获取当前本地时间", "{}", args =>
        {
            var value = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
            return Task.FromResult(JsonSerializer.Serialize(new { localTime = value }));
        });

        tools.Register("calc", "计算安全的四则表达式", "{\"expression\":\"(12+3)*4\"}", args =>
        {
            var value = SimpleCalculator.Evaluate(JsonArgs.RequiredString(args, "expression"));
            return Task.FromResult(JsonSerializer.Serialize(new { value }));
        });

        tools.Register("list_notes", "列出 Markdown 笔记", "{}", args =>
        {
            return Task.FromResult(JsonSerializer.Serialize(new { files = notes.List() }));
        });

        tools.Register("read_note", "读取一条 Markdown 笔记", "{\"fileName\":\"todo.md\"}", args =>
        {
            var fileName = JsonArgs.RequiredString(args, "fileName");
            return Task.FromResult(JsonSerializer.Serialize(new { fileName, content = notes.Read(fileName) }));
        });

        tools.Register("write_note", "写入一条 Markdown 笔记", "{\"fileName\":\"idea.md\",\"content\":\"...\"}", async args =>
        {
            var fileName = JsonArgs.RequiredString(args, "fileName");
            var content = JsonArgs.RequiredString(args, "content");
            await notes.WriteAsync(fileName, content);
            return JsonSerializer.Serialize(new { fileName, bytes = Encoding.UTF8.GetByteCount(content) });
        });

        tools.Register("search_notes", "按关键词搜索 Markdown 笔记", "{\"query\":\"agent\"}", args =>
        {
            var query = JsonArgs.RequiredString(args, "query");
            return Task.FromResult(JsonSerializer.Serialize(new { hits = notes.Search(query, 5) }));
        });

        return tools;
    }
}

public sealed class FileNoteStore
{
    private readonly string _root;

    public FileNoteStore(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public string[] List()
    {
        return Directory.EnumerateFiles(_root, "*.md")
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .OrderBy(x => x)
            .Cast<string>()
            .ToArray();
    }

    public async Task WriteAsync(string fileName, string content)
    {
        var path = GetPath(fileName);
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
    }

    public async Task AppendAsync(string fileName, string content)
    {
        var path = GetPath(fileName);
        await File.AppendAllTextAsync(path, content + Environment.NewLine, Encoding.UTF8);
    }

    public string Read(string fileName)
    {
        var path = GetPath(fileName);
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
    }

    public SearchHit[] Search(string query, int limit)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            return [];
        }

        return Directory.EnumerateFiles(_root, "*.md")
            .Select(file =>
            {
                var text = File.ReadAllText(file, Encoding.UTF8);
                var score = terms.Sum(term => CountOccurrences(text, term));
                return new SearchHit(Path.GetFileName(file), score, MakeSnippet(text, terms));
            })
            .Where(hit => hit.Score > 0)
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.FileName)
            .Take(limit)
            .ToArray();
    }

    private string GetPath(string fileName)
    {
        fileName = fileName.Replace('\\', '/').Split('/').Last().Trim();

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".md";
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid note file name.");
        }

        var path = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escaped the note directory.");
        }

        return path;
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }

        return count;
    }

    private static string MakeSnippet(string text, string[] terms)
    {
        var first = terms
            .Select(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();

        var start = Math.Max(0, first - 60);
        var length = Math.Min(180, text.Length - start);
        return text.Substring(start, length).ReplaceLineEndings(" ");
    }
}

public sealed record SearchHit(string? FileName, int Score, string Snippet);

public static class JsonArgs
{
    public static string RequiredString(JsonElement args, string propertyName)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ArgumentException($"Missing args.{propertyName}");
        }

        return value.GetString()!;
    }
}

public static class JsonElementFactory
{
    public static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}

public sealed class SimpleCalculator
{
    private readonly string _text;
    private int _index;

    private SimpleCalculator(string text)
    {
        _text = text;
    }

    public static double Evaluate(string expression)
    {
        var parser = new SimpleCalculator(expression);
        var value = parser.ParseExpression();
        parser.SkipWhitespace();

        if (parser._index != parser._text.Length)
        {
            throw new ArgumentException($"Unexpected character: {parser._text[parser._index]}");
        }

        return value;
    }

    private double ParseExpression()
    {
        var value = ParseTerm();

        while (true)
        {
            SkipWhitespace();

            if (Match('+'))
            {
                value += ParseTerm();
            }
            else if (Match('-'))
            {
                value -= ParseTerm();
            }
            else
            {
                return value;
            }
        }
    }

    private double ParseTerm()
    {
        var value = ParseFactor();

        while (true)
        {
            SkipWhitespace();

            if (Match('*'))
            {
                value *= ParseFactor();
            }
            else if (Match('/'))
            {
                var divisor = ParseFactor();
                if (divisor == 0)
                {
                    throw new DivideByZeroException();
                }

                value /= divisor;
            }
            else
            {
                return value;
            }
        }
    }

    private double ParseFactor()
    {
        SkipWhitespace();

        if (Match('+'))
        {
            return ParseFactor();
        }

        if (Match('-'))
        {
            return -ParseFactor();
        }

        if (Match('('))
        {
            var value = ParseExpression();
            SkipWhitespace();

            if (!Match(')'))
            {
                throw new ArgumentException("Missing closing parenthesis.");
            }

            return value;
        }

        return ParseNumber();
    }

    private double ParseNumber()
    {
        SkipWhitespace();
        var start = _index;

        while (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
        {
            _index++;
        }

        if (start == _index)
        {
            throw new ArgumentException("Expected a number.");
        }

        return double.Parse(_text[start.._index], CultureInfo.InvariantCulture);
    }

    private bool Match(char expected)
    {
        if (_index >= _text.Length || _text[_index] != expected)
        {
            return false;
        }

        _index++;
        return true;
    }

    private void SkipWhitespace()
    {
        while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
        {
            _index++;
        }
    }
}
