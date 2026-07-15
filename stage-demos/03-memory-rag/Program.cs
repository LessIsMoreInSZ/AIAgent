using System.Text;
using Agent.Common;

Console.OutputEncoding = Encoding.UTF8;

var knowledgeRoot = Path.Combine(AppContext.BaseDirectory, "knowledge");
var notes = new FileNoteStore(knowledgeRoot);
await SeedKnowledgeAsync(notes);

var tools = DemoToolkits.BasicTools(knowledgeRoot);
using var llm = new OllamaChatClient(args.FirstOrDefault());

var systemPrompt = string.Join('\n', new[]
{
    "你是一个带本地知识库的 C# 学习助理。",
    "",
    "可用工具：",
    tools.ToPromptBlock(),
    "",
    "答题策略：",
    "- 涉及 C# agent 学习资料时，先用 search_notes 检索。",
    "- 回答时说明你参考了哪些文件名。",
    "- 不确定就说不确定，不要编造。",
    "",
    "工具调用 JSON：",
    "{\"type\":\"tool\",\"tool\":\"search_notes\",\"args\":{\"query\":\"RAG\"}}",
    "",
    "最终回答 JSON：",
    "{\"type\":\"final\",\"answer\":\"中文回答\"}"
});

var messages = new List<ChatMessage>
{
    new("system", systemPrompt)
};

Console.WriteLine("Stage 03 - Memory and RAG");
Console.WriteLine($"Model: {llm.Model}");
Console.WriteLine($"Knowledge: {knowledgeRoot}");
Console.WriteLine("Try: RAG 和普通聊天有什么区别？");
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

    if (input.StartsWith("/search ", StringComparison.OrdinalIgnoreCase))
    {
        var query = input["/search ".Length..];
        foreach (var hit in notes.Search(query, 5))
        {
            Console.WriteLine($"{hit.FileName} score={hit.Score} {hit.Snippet}");
        }

        continue;
    }

    messages.Add(new ChatMessage("user", input));
    await RunAgentTurnAsync(llm, tools, messages);
}

static async Task SeedKnowledgeAsync(FileNoteStore notes)
{
    if (notes.List().Length > 0)
    {
        return;
    }

    await notes.WriteAsync("agent-loop.md", """
        Agent loop 是模型和程序交替工作的循环。模型负责理解意图和选择下一步，程序负责执行工具、校验参数、记录日志和保护权限边界。
        """);

    await notes.WriteAsync("tools.md", """
        工具调用应该使用白名单。每个工具都需要清晰的描述、参数 schema、错误返回和权限等级。写文件、删文件、发网络请求属于高风险工具。
        """);

    await notes.WriteAsync("rag.md", """
        RAG 是 retrieval augmented generation。常见流程是文档切分、建立索引、检索相关片段、把片段放入 prompt，再让模型基于资料回答。
        """);
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
            Console.WriteLine("Agent> " + raw);
            messages.Add(new ChatMessage("assistant", raw));
            return;
        }

        if (command.Type == "final")
        {
            Console.WriteLine("Agent> " + command.Answer);
            messages.Add(new ChatMessage("assistant", raw));
            return;
        }

        if (command.Type != "tool" || string.IsNullOrWhiteSpace(command.Tool))
        {
            Console.WriteLine("Unknown command: " + raw);
            return;
        }

        var result = await tools.InvokeAsync(command.Tool, command.Args);
        Console.WriteLine($"Tool> {command.Tool}");
        Console.WriteLine("Tool result> " + result);
        messages.Add(new ChatMessage("assistant", raw));
        messages.Add(new ChatMessage("user", $"工具 {command.Tool} 返回：{result}\n请继续。"));
    }
}
