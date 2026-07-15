# Stage 01 - Basic Chat

目标：理解最小 AI 交互循环。

你会看到：

- `system/user/assistant` 消息如何保存上下文。
- C# 如何通过 `HttpClient` 调 Ollama `/api/chat`。
- `/reset` 清空短期记忆后，模型行为如何变化。

运行：

```powershell
dotnet run --project .\01-basic-chat\BasicChat.csproj
```

练习：

- 修改 system prompt，让它变成“严厉代码审查员”。
- 加一个 `/save` 命令，把历史消息保存到文件。
- 观察同一个问题在 reset 前后的回答差异。

