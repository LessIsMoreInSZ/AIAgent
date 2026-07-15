using System.Text;
using Agent.Common;

Console.OutputEncoding = Encoding.UTF8;

using var llm = new OllamaChatClient(args.FirstOrDefault());

Console.WriteLine("Stage 06 - Multi-Agent Capstone");
Console.WriteLine($"Model: {llm.Model}");
Console.WriteLine("输入一个你想学的 AI Agent 主题，例如：RAG、工具调用、MCP、评估。");
Console.Write("Topic> ");

var topic = Console.ReadLine();
if (string.IsNullOrWhiteSpace(topic))
{
    topic = "C# AI Agent 工具调用";
}

var planner = new RoleAgent("Planner", "你是学习规划师。把主题拆成 3 个循序渐进的学习任务。");
var tutor = new RoleAgent("Tutor", "你是 C# AI Agent 导师。用清楚的中文讲解，并给一个小练习。");
var reviewer = new RoleAgent("Reviewer", "你是严格的学习复盘官。指出学习者下一步最该验证的 3 个问题。");

var plan = await AskRoleAsync(llm, planner, $"主题：{topic}");
Console.WriteLine();
Console.WriteLine("Planner>");
Console.WriteLine(plan);

var lesson = await AskRoleAsync(llm, tutor, $"主题：{topic}\n学习计划：{plan}");
Console.WriteLine();
Console.WriteLine("Tutor>");
Console.WriteLine(lesson);

var review = await AskRoleAsync(llm, reviewer, $"主题：{topic}\n学习计划：{plan}\n讲解：{lesson}");
Console.WriteLine();
Console.WriteLine("Reviewer>");
Console.WriteLine(review);

Console.WriteLine();
Console.WriteLine("Capstone idea: replace these sequential calls with Planner -> Executor -> Reviewer over real tools.");

static async Task<string> AskRoleAsync(OllamaChatClient llm, RoleAgent agent, string input)
{
    var messages = new[]
    {
        new ChatMessage("system", agent.SystemPrompt),
        new ChatMessage("user", input)
    };

    try
    {
        return await llm.CompleteAsync(messages);
    }
    catch
    {
        return Fallback(agent.Name, input);
    }
}

static string Fallback(string role, string input)
{
    return role switch
    {
        "Planner" => $"离线示例计划：1. 澄清概念；2. 写一个最小 demo；3. 加日志和测试。输入：{input}",
        "Tutor" => "离线示例讲解：Agent 的关键是让模型做判断，让程序做执行。小练习：新增一个 search_notes 工具并记录工具调用日志。",
        "Reviewer" => "离线示例复盘：1. 工具参数是否校验？2. 模型输出错了会怎样？3. 这个流程能否用固定用例回归测试？",
        _ => "离线示例输出。"
    };
}

sealed record RoleAgent(string Name, string SystemPrompt);
