using System.Text;
using Agent.Common;

Console.OutputEncoding = Encoding.UTF8;

var notesRoot = Path.Combine(AppContext.BaseDirectory, "notes");
var tools = DemoToolkits.BasicTools(notesRoot);

using var llm = new OllamaChatClient(args.FirstOrDefault());

var systemPrompt = string.Join('\n', new[]
{
    "你是一个 C# 工具型 agent。你必须用中文服务用户。",
    "",
    "可用工具：",
    tools.ToPromptBlock(),
    "",
    "如果需要工具，只能输出 JSON：",
    "{\"type\":\"tool\",\"tool\":\"calc\",\"args\":{\"expression\":\"1+2*3\"}}",
    "",
    "如果可以最终回答，只能输出 JSON：",
    "{\"type\":\"final\",\"answer\":\"你的中文回答\"}",
    "",
    "不要输出 Markdown 代码块。不要编造工具结果。"
});

var messages = new List<ChatMessage>
{
    new("system", systemPrompt)
};

Console.WriteLine("Stage 02 - Tool Agent");
Console.WriteLine($"Model: {llm.Model}");
Console.WriteLine($"Notes: {notesRoot}");
Console.WriteLine("Try: 帮我算一下 (18+24)*3，然后写到 math.md");
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

    messages.Add(new ChatMessage("user", input));
    await RunAgentTurnAsync(llm, tools, messages);
}

static async Task RunAgentTurnAsync(OllamaChatClient llm, ToolRegistry tools, List<ChatMessage> messages)
{
    for (var step = 1; step <= 6; step++)
    {
        string raw;

        try
        {
            raw = await llm.CompleteAsync(messages);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Cannot reach Ollama: " + ex.Message);
            return;
        }

        var command = AgentCommand.TryParse(raw);
        if (command is null)
        {
            messages.Add(new ChatMessage("assistant", raw));
            Console.WriteLine("Agent> " + raw);
            return;
        }

        if (command.Type == "final")
        {
            messages.Add(new ChatMessage("assistant", raw));
            Console.WriteLine("Agent> " + command.Answer);
            return;
        }

        if (command.Type != "tool" || string.IsNullOrWhiteSpace(command.Tool))
        {
            Console.WriteLine("Agent produced an unknown command: " + raw);
            return;
        }

        Console.WriteLine("Tool> " + command.Tool);
        var result = await tools.InvokeAsync(command.Tool, command.Args);
        Console.WriteLine("Tool result> " + result);

        messages.Add(new ChatMessage("assistant", raw));
        messages.Add(new ChatMessage("user", $"工具 {command.Tool} 返回：{result}\n请继续。"));
    }

    Console.WriteLine("Agent stopped after too many tool calls.");
}
