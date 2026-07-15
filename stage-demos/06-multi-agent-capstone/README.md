# Stage 06 - Multi-Agent Capstone

目标：做一个毕业项目雏形：多个角色协作完成学习任务。

角色：

- `Planner`：拆学习计划。
- `Tutor`：讲解和布置练习。
- `Reviewer`：复盘风险和验证问题。

运行：

```powershell
dotnet run --project .\06-multi-agent-capstone\MultiAgentCapstone.csproj
```

没有 Ollama 时，程序会使用离线 fallback，方便你先看流程。

练习：

- 让 `Planner` 输出 JSON 计划。
- 让 `Tutor` 可以调用 Stage 03 的知识库工具。
- 让 `Reviewer` 生成 eval cases，回流到 Stage 04。

