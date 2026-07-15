# AIAgent Stage Demos

这组项目把三个月学习路线拆成 6 个可运行阶段。每个阶段都是一个独立 C# console demo，共享 `src/Agent.Common`。

## 运行顺序

1. `01-basic-chat`：最小聊天循环，理解 messages 和 Ollama HTTP 调用。
2. `02-tool-agent`：结构化 JSON 输出和工具调用循环。
3. `03-memory-rag`：文件记忆、关键词检索、RAG 思路。
4. `04-service-eval`：HTTP 服务化和可重复评估。
5. `05-mcp-style-tools`：MCP 风格的工具发现与调用协议。
6. `06-multi-agent-capstone`：Planner/Tutor/Reviewer 多 Agent 毕业项目雏形。

## 一次性编译

```powershell
cd C:\Users\25348\Desktop\AIAgent\stage-demos
dotnet build AIAgent.Stages.slnx
```

## 模型准备

默认模型是本地 Ollama 的 `qwen3:0.6b`：

```powershell
ollama pull qwen3:0.6b
```

换模型：

```powershell
$env:OLLAMA_MODEL = "qwen3:1.7b"
dotnet run --project .\01-basic-chat\BasicChat.csproj
```

## 学习方法

- 先跑通，再读代码。
- 每次只改一个变量，例如 system prompt、工具描述、最大工具调用次数。
- 每个阶段都记录一次失败案例，因为 agent 工程最重要的是处理不稳定输出。
- 不要急着上框架。先理解模型、工具、上下文、日志和评估的边界。

