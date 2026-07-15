using System.Net;
using System.Text;
using System.Text.Json;
using Agent.Common;

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--serve", StringComparer.OrdinalIgnoreCase))
{
    await RunServerAsync();
}
else
{
    await RunEvalAsync();
}

static async Task RunEvalAsync()
{
    Console.WriteLine("Stage 04 - Service and Evaluation");
    Console.WriteLine("Default mode runs local deterministic evals. Use --serve to start a tiny HTTP API.");
    Console.WriteLine();

    var cases = new[]
    {
        new EvalCase("calculator precedence", () => SimpleCalculator.Evaluate("1+2*3") == 7),
        new EvalCase("calculator parentheses", () => SimpleCalculator.Evaluate("(1+2)*3") == 9),
        new EvalCase("parse tool command", () =>
        {
            var command = AgentCommand.TryParse("{\"type\":\"tool\",\"tool\":\"calc\",\"args\":{\"expression\":\"1+1\"}}");
            return command?.Type == "tool" && command.Tool == "calc";
        }),
        new EvalCase("parse final command", () =>
        {
            var command = AgentCommand.TryParse("{\"type\":\"final\",\"answer\":\"ok\"}");
            return command?.Type == "final" && command.Answer == "ok";
        })
    };

    var passed = 0;

    foreach (var item in cases)
    {
        var ok = item.Test();
        passed += ok ? 1 : 0;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {item.Name}");
    }

    Console.WriteLine();
    Console.WriteLine($"Result: {passed}/{cases.Length} passed");
    await Task.CompletedTask;
}

static async Task RunServerAsync()
{
    using var llm = new OllamaChatClient();
    using var listener = new HttpListener();
    listener.Prefixes.Add("http://localhost:5088/");
    listener.Start();

    Console.WriteLine("Stage 04 - HTTP Service");
    Console.WriteLine("Listening: http://localhost:5088/ask?input=你好");
    Console.WriteLine("Press Ctrl+C to stop.");

    while (true)
    {
        var context = await listener.GetContextAsync();

        _ = Task.Run(async () =>
        {
            var input = context.Request.QueryString["input"] ?? "";
            var response = context.Response;
            response.ContentType = "application/json; charset=utf-8";

            try
            {
                var messages = new[]
                {
                    new ChatMessage("system", "你是一个通过 HTTP 暴露的中文 C# AI Agent。回答要短。"),
                    new ChatMessage("user", input)
                };

                var answer = await llm.CompleteAsync(messages);
                await WriteJsonAsync(response, new { ok = true, answer });
            }
            catch (Exception ex)
            {
                await WriteJsonAsync(response, new { ok = false, error = ex.Message });
            }
        });
    }
}

static async Task WriteJsonAsync(HttpListenerResponse response, object payload)
{
    var json = JsonSerializer.Serialize(payload);
    var bytes = Encoding.UTF8.GetBytes(json);
    response.ContentLength64 = bytes.Length;
    await response.OutputStream.WriteAsync(bytes);
    response.Close();
}

sealed record EvalCase(string Name, Func<bool> Test);
