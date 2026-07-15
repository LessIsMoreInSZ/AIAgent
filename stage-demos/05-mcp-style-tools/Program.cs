using System.Text;
using System.Text.Json;
using Agent.Common;

Console.OutputEncoding = Encoding.UTF8;

var tools = DemoToolkits.BasicTools(Path.Combine(AppContext.BaseDirectory, "mcp-notes"));

Console.WriteLine("Stage 05 - MCP-style Tools");
Console.WriteLine("This demo simulates a tiny JSON-RPC tool server in-process.");
Console.WriteLine();

var requests = new[]
{
    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\",\"params\":{}}",
    "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"calc\",\"arguments\":{\"expression\":\"(8+4)/3\"}}}",
    "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"write_note\",\"arguments\":{\"fileName\":\"mcp.md\",\"content\":\"MCP 把工具能力通过协议暴露给 agent。\"}}}",
    "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"list_notes\",\"arguments\":{}}}"
};

foreach (var request in requests)
{
    Console.WriteLine("Client> " + request);
    Console.WriteLine("Server> " + await HandleJsonRpcAsync(tools, request));
    Console.WriteLine();
}

Console.WriteLine("Study point: tools/list is discovery, tools/call is execution. The model should never execute raw code.");

static async Task<string> HandleJsonRpcAsync(ToolRegistry tools, string requestJson)
{
    using var doc = JsonDocument.Parse(requestJson);
    var root = doc.RootElement;
    var id = root.GetProperty("id").GetInt32();
    var method = root.GetProperty("method").GetString();
    var parameters = root.GetProperty("params");

    object response = method switch
    {
        "tools/list" => new
        {
            jsonrpc = "2.0",
            id,
            result = tools.Tools.Select(tool => new
            {
                name = tool.Name,
                description = tool.Description,
                args = tool.ArgsShape
            })
        },
        "tools/call" => await CallToolAsync(tools, id, parameters),
        _ => new
        {
            jsonrpc = "2.0",
            id,
            error = new { code = -32601, message = "Method not found" }
        }
    };

    return JsonSerializer.Serialize(response);
}

static async Task<object> CallToolAsync(ToolRegistry tools, int id, JsonElement parameters)
{
    var name = parameters.GetProperty("name").GetString() ?? "";
    var arguments = parameters.TryGetProperty("arguments", out var args)
        ? args
        : JsonElementFactory.EmptyObject();

    var result = await tools.InvokeAsync(name, arguments);

    return new
    {
        jsonrpc = "2.0",
        id,
        result = JsonDocument.Parse(result).RootElement.Clone()
    };
}
