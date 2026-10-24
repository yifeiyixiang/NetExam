# LeaveApproval 请假审批系统

基于 **.NET 8 + CQRS + MediatR** 的企业级请假审批系统。严格分层（Domain / Application / Infrastructure / Api），使用 EF Core 8（MySQL 5.7）、Redis（StackExchange.Redis）、Autofac、FluentValidation、AutoMapper、JWT Bearer、Serilog、Swagger、xUnit。

> 本项目为技术测试交付物：完整源代码、数据库迁移、单元/集成测试、配置示例与部署文档均已包含，且**构建与测试均为实际执行结果**（见文末验收报告）。

---

## 1. 快速开始

### 1.1 环境要求

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| .NET SDK | 8.0+ | `dotnet --list-sdks` 确认 |
| MySQL | 5.7 | 本测试环境：`localhost:10010`，库 `local`，账号 `root/root` |
| Redis | 5.x | 本测试环境：`localhost:6379`（如 `D:\Code\mqttc\Redis-x64-5.0.14.1\redis-server.exe redis.windows.conf`） |

也可以用 Docker Compose 启动依赖服务（端口与本配置一致）：

```bash
docker compose up -d mysql redis
```

### 1.2 初始化数据库并启动

```bash
# 生成迁移（如需重新生成）
dotnet ef migrations add InitialCreate \
  --project src/LeaveApproval.Infrastructure \
  --startup-project src/LeaveApproval.Api \
  --output-dir Persistence/Migrations

# 手动执行迁移（也可跳过：开发环境启动时自动迁移+种子数据）
dotnet ef database update \
  --project src/LeaveApproval.Infrastructure \
  --startup-project src/LeaveApproval.Api

# 启动 API（Development 环境自动执行 Migration + 种子数据）
cd src/LeaveApproval.Api
dotnet run
```

- Swagger：<http://localhost:5000/swagger>
- 健康检查：<http://localhost:5000/health>
- 日志文件：`src/LeaveApproval.Api/logs/leaveapproval-*.log`

### 1.3 测试账号（仅开发/测试环境，生产环境禁止使用默认密码）

| 用户名 | 密码 | 角色 | 部门 | 直属领导 | 说明 |
| --- | --- | --- | --- | --- | --- |
| admin | Admin@123 | Admin | 研发部 | - | 系统管理员 |
| zhangsan | Employee@123 | Employee | 研发部 | lisi | 普通员工（申请人） |
| lisi | Manager@123 | Manager | 研发部 | wangwu | 直属领导（节点1） |
| wangwu | Manager@123 | Manager | 研发部 | - | 研发部经理（节点2） |
| sunqi | Manager@123 | Manager | 市场部 | - | 市场部经理 |
| zhaoliu | Employee@123 | Employee | 市场部 | sunqi | 普通员工 |

种子流程配置：`研发部请假审批流程`（研发部启用中）——节点1 `直属领导审批`（ApproverType=DirectLeader，运行时解析为申请人 DirectManagerId），节点2 `部门经理审批`（ApproverType=DepartmentManager，运行时解析为 Department.ManagerUserId）。

---

## 2. 端到端测试步骤

1. 打开 Swagger，`POST /api/auth/login` 用 `zhangsan/Employee@123` 登录，点击右上角 **Authorize** 填入返回的 `accessToken`。
2. `POST /api/leave-requests`（multipart/form-data）：字段 `LeaveType=Annual`、`StartTime`、`EndTime`、`Reason`、可选多文件字段 `Attachments`。返回 `201` 与单号 `LR...`。
3. `GET /api/leave-requests/{id}` 查看详情（状态 PendingApproval、当前节点 直属领导审批、附件列表）。再次调用命中 Redis 缓存。
4. 换 `lisi/Manager@123` 登录，`GET /api/approvals/pending?page=1&pageSize=20` 可见待办；`POST /api/leave-requests/{id}/approve`（`{"comment":"同意"}`）通过节点1。
5. 换 `wangwu/Manager@123` 登录，重复步骤 4 → 流程结束，单据 `Approved`。
6. 驳回场景：提交新单，用 lisi 调 `POST /api/leave-requests/{id}/reject`（`{"comment":"需要补充材料"}`，必填）→ 单据 `Rejected`，审批记录包含驳回意见，后续节点任务作废。
7. `admin` 登录后 `GET /api/dev/emails` 查看模拟邮件（提交通知/流转通知/驳回通知/完成通知）。

请求/响应示例：

```bash
# 登录
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"zhangsan","password":"Employee@123"}'

# 提交请假（multipart）
curl -X POST http://localhost:5000/api/leave-requests \
  -H "Authorization: Bearer $TOKEN" \
  -F "LeaveType=Annual" -F "StartTime=2026-10-26T09:00:00" \
  -F "EndTime=2026-10-27T18:00:00" -F "Reason=年假" \
  -F "Attachments=@说明.txt"

# 审批通过
curl -X POST http://localhost:5000/api/leave-requests/1/approve \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"comment":"同意"}'
```

错误响应统一为：`{ "traceId": "...", "code": "validation_error|unauthorized|forbidden|not_found|conflict|task_already_processed|concurrency_conflict|internal_error", "message": "...", "errors": [{"field","code","message"}] }`。

---

## 3. API 接口清单

| 方法 | 路径 | 权限 | 说明 |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | 匿名 | 登录，返回 JWT（sub/name/role/jti）、过期时间、用户信息 |
| POST | `/api/leave-requests` | 登录 | 提交请假（multipart，可多附件） |
| GET | `/api/leave-requests/{id}` | 申请人/审批人/Admin | 详情（Redis 缓存优先，TTL 10 分钟） |
| GET | `/api/leave-requests/my` | 登录 | 我的申请（分页） |
| POST | `/api/leave-requests/{id}/approve` | 当前节点审批人 | 同意（对象级授权） |
| POST | `/api/leave-requests/{id}/reject` | 当前节点审批人 | 驳回（意见必填） |
| GET | `/api/leave-requests/{id}/attachments/{attachmentId}` | 申请人/审批人/Admin | 附件下载（不暴露物理路径） |
| GET | `/api/approvals/pending` | 登录 | 我的待审批（分页，版本号缓存，TTL 5 分钟） |
| POST/PUT/GET | `/api/workflow-configs...` | Admin | 流程配置管理 |
| GET | `/api/dev/emails` | Admin（开发辅助） | 查看模拟邮件 |

状态码约定：400 参数校验失败 / 401 未认证 / 403 无权访问 / 404 不存在 / 409 状态冲突、重复审批、并发冲突、唯一键冲突 / 500 未预期异常。

---

## 4. 架构说明

```
LeaveApproval.sln
├─ src/LeaveApproval.Domain          # 实体、枚举、领域异常、审批流规则（无外部依赖）
├─ src/LeaveApproval.Application     # CQRS 请求/Handler、验证器、管道行为、DTO、AutoMapper、接口抽象、Autofac 模块
├─ src/LeaveApproval.Infrastructure  # EF Core(MySQL)、Redis、本地文件存储、模拟邮件+Outbox、JWT/BCrypt、BaseRepository
├─ src/LeaveApproval.Api             # Controller、中间件、Filter、Program、配置
└─ tests/
   ├─ LeaveApproval.UnitTests        # 领域规则 + 验证器（31 例）
   └─ LeaveApproval.IntegrationTests # 真实 MySQL/Redis 端到端（8 例）
```

依赖方向：Api → Application + Infrastructure；Infrastructure → Application + Domain；Application → Domain；Domain 零依赖。Controller 只做 HTTP 绑定与 MediatR 转发；Handler 通过 `ICurrentUserService` 获取身份，不触碰 HttpContext。

### 4.1 CQRS 与管道（Autofac 注册顺序即执行顺序）

1. **LoggingBehavior**：记录请求名、操作人、耗时、成败；单据号在 Handler 内生成后通过 `IHaveLeaveNo` 补充进日志属性。
2. **ValidationBehavior**：执行当前请求的所有 FluentValidation 验证器，失败抛 `ValidationException` → 400。
3. **TransactionBehavior**：仅对实现 `ITransactionalRequest` 的写操作开启数据库事务；提交成功后才执行 `IPostCommitExecutor` 回调（缓存失效）。查询不开启事务。

### 4.2 审批流转规则

- 提交时**冻结流程快照**：按当前启用的流程配置为所有节点生成 `LeaveApprovalTask`（审批人快照、节点顺序/名称快照），仅节点1激活为 `Pending`，其余 `Upcoming`；单据记录 `WorkflowConfigId + WorkflowVersion`。**模板后续修改不影响已提交单据**（且已被使用的流程节点结构锁定，修改节点返回 409，需新建流程）。
- ApproverType 三种：`SpecificUser`（绑定具体用户）、`DirectLeader`（解析申请人直属领导）、`DepartmentManager`（解析部门经理）。
- 同意：当前节点任一审批人通过即流转，其余同节点任务 `Skipped`；激活下一节点；最后一节点通过后单据 `Approved`。**不允许跳过节点、不允许客户端指定审批人**。
- 驳回：意见必填；当前任务 `Rejected`，其余未处理任务全部 `Skipped`，单据 `Rejected`，不得继续流转。
- 单据状态：`Draft`（保留未开放）/`PendingApproval`/`Approved`/`Rejected`/`Cancelled`（保留可扩展）；任务状态：`Upcoming/Pending/Approved/Rejected/Skipped`。
- **并发控制**：`LeaveRequest`/`LeaveApprovalTask` 使用 `Version` 并发令牌（条件 UPDATE），并发审批同一任务仅一个成功（另一个 409 `task_already_processed`/`concurrency_conflict`）；已处理过的任务再次操作返回 409。

### 4.3 请假时长口径

**按自然小时计算**（不区分工作日/节假日，未实现工作日历）：`Duration = round((EndTime - StartTime).TotalHours, 1)`，上限 `Leave:MaxDurationHours`（默认 720 小时，可配置）。README 明确本测试版本不引入工作日历。

### 4.4 缓存策略与一致性边界

- 详情缓存：`leave:detail:{id}`，TTL 10 分钟；读取时先查缓存（权限校验仍基于缓存中的申请人/审批人字段执行），未命中查库回填。
- 待办缓存：`leave:pending:{userId}:v{version}:p{page}:s{pageSize}`；每个审批人维护版本键 `leave:pending:ver:{userId}`，流转/驳回时 `INCR` 一次性失效该用户全部分页键（O(1)，无 KEYS/SCAN 扫描）。`RemoveByPrefixAsync` 亦提供基于 SCAN 的前缀清理。
- 失效时机：**数据库事务提交成功之后**（TransactionBehavior → PostCommit），覆盖详情缓存与相关审批人待办版本。
- **已知边界**：Redis 失效操作失败时仅记录告警（业务不受影响），依赖 TTL 兜底，最迟 10 分钟后一致；应用与 Redis 不做分布式事务（也不应声称有）。

### 4.5 通知可靠性（Outbox）

邮件不直接发送：`EmailOutboxMessage` 与业务数据**同事务**写入（消息类型、业务单号、收件人、主题、正文、状态、尝试次数、下次重试时间、错误信息），后台 `EmailOutboxProcessor` 每 3 秒轮询发送（模拟实现：结构化日志 + 内存邮箱 `/api/dev/emails`），失败按 1/2/4/8/16 分钟退避重试，5 次后标记 `Failed`。**数据库回滚则通知绝不会发出；邮件失败不影响业务数据**。当前简化边界：Outbox 为进程内轮询（非消息队列），应用停机期间到期消息会在重启后补发。

### 4.6 文件存储

`IFileStorageService` 本地磁盘实现：存储键为随机 GUID+白名单扩展名（原始文件名仅展示）、读写均校验键格式防路径穿越、数据库失败时补偿删除已保存文件（尽力而为，删除失败记录告警）。限制：数量 ≤ `Attachment:MaxCount`（默认 5）、单文件 ≤ `Attachment:MaxSizeBytes`（默认 10MB）、扩展名白名单。

### 4.7 日志

Serilog：控制台 + 按日滚动文件（`logs/leaveapproval-.log`，保留 31 天）。结构化属性包含 CorrelationId（`X-Correlation-Id` 请求头或自动生成，并写入响应头）、RequestPath、操作人 ID/姓名、MediatR 请求名、LeaveNo、耗时、异常类型与堆栈。不记录明文密码/Token/附件内容，不整包序列化请求 DTO；业务异常由日志管道记录一次，未预期异常由全局异常中间件记录。

---

## 5. 配置

全部可通过 `appsettings.json` / `appsettings.Development.json` / 环境变量覆盖（环境变量用 `__` 分隔，如 `ConnectionStrings__Default`）：

| 配置项 | 环境变量 | 说明 |
| --- | --- | --- |
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | MySQL 连接串 |
| `Redis:ConnectionString` | `Redis__ConnectionString` | Redis 地址 |
| `Jwt:SecretKey` | `Jwt__SecretKey` | JWT 签名密钥（≥32 字符，生产必须更换，不得提交 Git） |
| `Jwt:Issuer / Audience / ExpiresInMinutes` | 同上格式 | 签发方/受众/有效期（默认 480 分钟） |
| `FileStorage:RootPath` | `FileStorage__RootPath` | 附件存储根目录（默认 `uploads`） |
| `Attachment:MaxCount / MaxSizeBytes / AllowedExtensions` | 同上格式 | 附件限制 |
| `Leave:MaxDurationHours` | `Leave__MaxDurationHours` | 单次请假上限 |
| `Database:InitializeOnStartup` | `Database__InitializeOnStartup` | 启动时迁移+种子（Development 默认开） |
| `Serilog:MinimumLevel:Default` | `Serilog__MinimumLevel__Default` | 日志级别 |
| `Swagger:Enabled` | `Swagger__Enabled` | 生产建议关闭 |

**生产密钥管理**：连接串/密码/密钥一律通过环境变量或密钥管理服务注入；仓库中的 `appsettings.json` 仅含开发默认值。

---

## 6. 部署

### 6.1 Windows Server + IIS

1. 安装 [.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0)（含 IIS ASP.NET Core Module v2）。
2. 发布：`dotnet publish src/LeaveApproval.Api -c Release -o publish`。
3. IIS 建站：物理目录指向 `publish`，应用程序池设为 **无托管代码**；服务器变量 `ASPNETCORE_ENVIRONMENT=Production`。
4. 环境变量（web.config `<environmentVariables>` 或系统级）：`ConnectionStrings__Default`、`Redis__ConnectionString`、`Jwt__SecretKey`、`FileStorage__RootPath`（建议指向磁盘绝对路径）、`ASPNETCORE_URLS`。
5. 执行迁移：`dotnet ef database update --project src/LeaveApproval.Infrastructure --startup-project src/LeaveApproval.Api`（或在发布目录运行 app 一次，配合 `Database__InitializeOnStartup=true`）。
6. 日志位于 `publish\logs\`；附件位于 `FileStorage:RootPath`；常见故障：500.30（配置/依赖注入错误，查 Windows 事件查看器与 `stdoutLogEnabled`）、Redis 连不上（缓存自动降级为直查数据库，仅影响性能）。

### 6.2 Linux + Kestrel + systemd

```bash
dotnet publish src/LeaveApproval.Api -c Release -o /opt/leaveapproval
```

`/etc/systemd/system/leaveapproval.service`：

```ini
[Unit]
Description=LeaveApproval API
After=network.target mysql.service redis.service

[Service]
WorkingDirectory=/opt/leaveapproval
ExecStart=/usr/bin/dotnet /opt/leaveapproval/LeaveApproval.Api.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=ConnectionStrings__Default=Server=localhost;Port=3306;Database=local;User=root;Password=***;charset=utf8mb4
Environment=Redis__ConnectionString=localhost:6379
Environment=Jwt__SecretKey=Production_Secret_At_Least_32_Characters_Long
Environment=FileStorage__RootPath=/var/lib/leaveapproval/uploads
Restart=always
RestartSec=5
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload && sudo systemctl enable --now leaveapproval
sudo journalctl -u leaveapproval -f     # 系统日志；文件日志在 /opt/leaveapproval/logs/
```

反向代理（Nginx）转发到 5000 端口即可。

---

## 7. 测试

```bash
# 单元测试（领域规则 + 验证器，不依赖外部环境）
dotnet test tests/LeaveApproval.UnitTests

# 集成测试（真实 MySQL + Redis；会重建 leaveapproval_test 库并清空本地 Redis 的 0 号库）
dotnet test tests/LeaveApproval.IntegrationTests
```

集成测试使用**真实 MySQL**（`leaveapproval_test` 库，测试前自动重建）与**真实 Redis**（启动时 `FLUSHDB` 清空本地实例的 0 号库，请勿指向生产实例），覆盖：登录/JWT、提交（含附件）、详情缓存、两节点审批流转、缓存失效、驳回、越权/重复审批防护、待办分页、流程配置管理、Outbox 邮件通知。未使用 SQLite/内存库，因此事务、唯一索引、并发令牌行为与生产一致。

---

## 8. 已知边界与说明

- 邮件为**模拟实现**（结构化日志 + 内存邮箱），不要求配置 SMTP；接入真实 SMTP 只需替换 `IEmailService` 实现。
- 单据状态 `Draft`/`Cancelled` 与任务取消接口为预留扩展，当前未开放提交/取消入口。
- 缓存失效失败依赖 TTL 兜底（最长 10 分钟），不保证强一致（Redis 与 MySQL 之间本就无分布式事务）。
- 文件补偿删除为"尽力而为"，极端情况下（进程崩溃在事务回滚后、补偿前）可能残留孤立文件，可定期清理存储目录中无对应元数据的键。
- 本环境的 MediatR 包为仓库定制构建（构造函数注入 `IServiceProvider`），Autofac 注册方式已适配，不影响 IRequestHandler / IPipelineBehavior 用法。
- 生产环境必须：更换 `Jwt:SecretKey` 与全部默认密码、关闭 Swagger（或加访问控制）、限制 `/api/dev/*` 访问。
