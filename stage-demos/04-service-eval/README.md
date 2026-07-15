# Stage 04 - Service and Evaluation

目标：把 demo 变成可服务化、可评估的工程。

默认运行本地确定性评估，不需要模型：

```powershell
dotnet run --project .\04-service-eval\ServiceEvalDemo.csproj
```

启动 HTTP 服务：

```powershell
dotnet run --project .\04-service-eval\ServiceEvalDemo.csproj -- --serve
```

浏览器访问：

```text
http://localhost:5088/ask?input=你好
```

练习：

- 把 eval case 放到 JSONL 文件。
- 增加“期望工具调用轨迹”的评估。
- 给 HTTP 请求加 trace id 和耗时日志。

