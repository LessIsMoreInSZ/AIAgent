# Stage 05 - MCP-style Tools

目标：理解 MCP 的核心思想：工具发现和工具调用应该走协议边界。

这个 demo 不实现完整 MCP，而是用一个极小 JSON-RPC 模拟：

- `tools/list`：列出工具名称、描述、参数。
- `tools/call`：调用指定工具并返回结果。

运行：

```powershell
dotnet run --project .\05-mcp-style-tools\McpStyleTools.csproj
```

练习：

- 给工具结果增加 `isError` 字段。
- 把 in-process server 拆成独立进程。
- 让 Stage 02 的 agent 通过这个协议调用工具，而不是直接引用工具函数。

