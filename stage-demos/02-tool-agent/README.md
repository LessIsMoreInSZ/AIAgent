# Stage 02 - Tool Agent

目标：让模型选择工具，让 C# 执行工具。

你会看到：

- 模型只输出 JSON 指令。
- 程序解析 JSON 并调用白名单工具。
- 工具结果再返回给模型，模型继续决策或最终回答。

运行：

```powershell
dotnet run --project .\02-tool-agent\ToolAgent.csproj
```

可以试：

```text
帮我算一下 (18+24)*3，然后写到 math.md
现在几点？写入 today.md
列出我的笔记
```

练习：

- 新增 `append_note` 工具。
- 给写文件工具增加用户确认。
- 给每次工具调用记录耗时。

