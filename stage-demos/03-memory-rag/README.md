# Stage 03 - Memory and RAG

目标：把 agent 从“只靠上下文聊天”升级为“会检索本地资料再回答”。

你会看到：

- `knowledge` 目录作为最小知识库。
- `search_notes` 工具返回命中文件和片段。
- 模型基于检索结果生成回答。

运行：

```powershell
dotnet run --project .\03-memory-rag\MemoryRagAgent.csproj
```

可以试：

```text
RAG 和普通聊天有什么区别？
/search 工具
工具调用为什么要白名单？
```

练习：

- 把自己的 Markdown 学习笔记放进 `knowledge`。
- 改进搜索评分，支持多个关键词。
- 让最终回答强制列出参考文件名。

