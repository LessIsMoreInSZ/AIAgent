using System.Text;
using Agent.Common;

Console.OutputEncoding = Encoding.UTF8;

using var llm = new OllamaChatClient(args.FirstOrDefault());

var messages = new List<ChatMessage>
{
    new("system", "你是一个简洁、耐心的中文 C# AI Agent 学习助手。先讲核心概念，再给一个小例子。")
};

Console.WriteLine("Stage 01 - Basic Chat");
Console.WriteLine($"Model: {llm.Model}");
Console.WriteLine("Commands: /exit, /reset, /history");
Console.WriteLine();

while (true)
{
    Console.Write("You> ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    if (input.Equals("/reset", StringComparison.OrdinalIgnoreCase))
    {
        messages.RemoveRange(1, messages.Count - 1);
        Console.WriteLine("History cleared.");
        continue;
    }

    if (input.Equals("/history", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var message in messages)
        {
            Console.WriteLine($"[{message.Role}] {message.Content}");
        }

        continue;
    }

    messages.Add(new ChatMessage("user", input));

    try
    {
        var answer = await llm.CompleteAsync(messages);
        messages.Add(new ChatMessage("assistant", answer));
        Console.WriteLine($"Agent> {answer}");
    }
    catch (Exception ex)
    {
        PrintOllamaHelp(ex, llm.Model);
    }
}

static void PrintOllamaHelp(Exception ex, string model)
{
    Console.WriteLine();
    Console.WriteLine("Cannot reach Ollama.");
    Console.WriteLine($"Reason: {ex.Message}");
    Console.WriteLine("Setup:");
    Console.WriteLine("  ollama pull " + model);
    Console.WriteLine("  dotnet run");
    Console.WriteLine();
}
