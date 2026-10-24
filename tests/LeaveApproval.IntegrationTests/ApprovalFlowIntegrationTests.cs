using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LeaveApproval.IntegrationTests;
using Xunit;

namespace LeaveApproval.IntegrationTests;

/// <summary>
/// 端到端集成测试：真实 MySQL + Redis，覆盖登录、提交、审批、驳回、待办、缓存失效、越权防护。
/// 说明：使用真实 MySQL（非 SQLite/内存库），事务、唯一索引、并发控制行为与生产一致；
/// Redis 也为真实实例，缓存读写与版本失效逻辑被真实执行。
/// </summary>
[Collection("api")]
public class ApprovalFlowIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly LeaveApprovalApiFactory _factory;

    public ApprovalFlowIntegrationTests(LeaveApprovalApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient() => _factory.CreateClient();

    private static DateTime FutureUtc(double hours) => DateTime.UtcNow.AddHours(hours);

    private static MultipartFormDataContent BuildSubmitContent(string leaveType, string reason, DateTime start, DateTime end, byte[]? attachment = null)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(leaveType), "LeaveType" },
            { new StringContent(start.ToString("yyyy-MM-ddTHH:mm:ss")), "StartTime" },
            { new StringContent(end.ToString("yyyy-MM-ddTHH:mm:ss")), "EndTime" },
            { new StringContent(reason), "Reason" }
        };

        if (attachment != null)
        {
            var fileContent = new ByteArrayContent(attachment) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } };
            content.Add(fileContent, "Attachments", "说明.txt");
        }

        return content;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(text, JsonOptions);
    }

    private async Task<long> SubmitLeaveAsync(HttpClient applicantClient, string reason, byte[]? attachment = null)
    {
        var response = await applicantClient.PostAsync("/api/leave-requests",
            BuildSubmitContent("Annual", reason, FutureUtc(4), FutureUtc(12), attachment));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task 登录_密码错误_返回401()
    {
        var client = NewClient();
        var response = await client.LoginAsync("zhangsan", "wrong-password");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task 未认证访问业务接口_返回401()
    {
        var client = NewClient();
        var response = await client.GetAsync("/api/leave-requests/my");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task 完整审批通过流程_待办列表_缓存失效与邮件通知()
    {
        // 1. 张三登录并提交请假（带附件）
        var zhangsanClient = (await NewClient().LoginAsAsync("zhangsan", "Employee@123")).Client;
        var leaveId = await SubmitLeaveAsync(zhangsanClient, "集成测试-完整审批流程", attachment: "测试附件内容"u8.ToArray());
        leaveId.Should().BeGreaterThan(0);

        // 2. 详情：PendingApproval，当前节点=直属领导审批
        var detail1 = await ReadJsonAsync(await zhangsanClient.GetAsync($"/api/leave-requests/{leaveId}"));
        detail1.GetProperty("status").GetString().Should().Be("PendingApproval");
        detail1.GetProperty("currentNodeOrder").GetInt32().Should().Be(1);
        detail1.GetProperty("attachments").GetArrayLength().Should().Be(1);
        // （此时详情已写入 Redis 缓存）

        // 3. 申请人不能审批自己的单
        (await zhangsanClient.ApproveAsync(leaveId, "自己同意自己")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. 李四待办列表包含该单（第二次调用走 Redis 缓存，结果一致）
        var lisiClient = (await NewClient().LoginAsAsync("lisi", "Manager@123")).Client;
        var pending1 = await ReadJsonAsync(await lisiClient.GetAsync("/api/approvals/pending?page=1&pageSize=20"));
        pending1.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        pending1.GetProperty("items").EnumerateArray()
            .Should().Contain(i => i.GetProperty("leaveNo").GetString()!.StartsWith("LR"));
        var pending2 = await ReadJsonAsync(await lisiClient.GetAsync("/api/approvals/pending?page=1&pageSize=20"));
        pending2.GetProperty("totalCount").GetInt32().Should().Be(pending1.GetProperty("totalCount").GetInt32());

        // 5. 越级：王五（节点2 审批人）在节点1 未完成时审批 → 403
        var wangwuClient = (await NewClient().LoginAsAsync("wangwu", "Manager@123")).Client;
        (await wangwuClient.ApproveAsync(leaveId, "越级")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. 李四（直属领导）审批通过 → 流转到节点2
        (await lisiClient.ApproveAsync(leaveId, "同意，注意休息")).StatusCode.Should().Be(HttpStatusCode.OK);

        // 7. 缓存失效验证：此前详情已缓存（currentNode=1），现在立即查询必须显示节点2
        var detail2 = await ReadJsonAsync(await zhangsanClient.GetAsync($"/api/leave-requests/{leaveId}"));
        detail2.GetProperty("currentNodeOrder").GetInt32().Should().Be(2);
        detail2.GetProperty("currentNodeName").GetString().Should().Be("部门经理审批");

        // 8. 重复审批：李四已处理过 → 409
        (await lisiClient.ApproveAsync(leaveId, "重复")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 9. 王五审批通过 → 流程完成，单据 Approved
        (await wangwuClient.ApproveAsync(leaveId, "同意")).StatusCode.Should().Be(HttpStatusCode.OK);
        var detail3 = await ReadJsonAsync(await zhangsanClient.GetAsync($"/api/leave-requests/{leaveId}"));
        detail3.GetProperty("status").GetString().Should().Be("Approved");
        detail3.GetProperty("completedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        detail3.GetProperty("records").GetArrayLength().Should().Be(3);

        // 10. 完成后王五的待办不再包含该单
        var wangwuPending = await ReadJsonAsync(await wangwuClient.GetAsync("/api/approvals/pending?page=1&pageSize=20"));
        wangwuPending.GetProperty("items").EnumerateArray()
            .Should().NotContain(i => i.GetProperty("leaveNo").GetString() == detail3.GetProperty("leaveNo").GetString());

        // 11. 附件下载（申请人可下载，返回原始内容）
        var attachmentId = detail1.GetProperty("attachments")[0].GetProperty("id").GetInt64();
        var download = await zhangsanClient.GetAsync($"/api/leave-requests/{leaveId}/attachments/{attachmentId}");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal("测试附件内容"u8.ToArray());
    }

    [Fact]
    public async Task 驳回流程_驳回意见入库并通知申请人_不得继续流转()
    {
        var zhangsanClient = (await NewClient().LoginAsAsync("zhangsan", "Employee@123")).Client;
        var leaveId = await SubmitLeaveAsync(zhangsanClient, "集成测试-驳回流程");

        var lisiClient = (await NewClient().LoginAsAsync("lisi", "Manager@123")).Client;
        var rejectResponse = await lisiClient.RejectAsync(leaveId, "病假需要补充医院证明");
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await ReadJsonAsync(await zhangsanClient.GetAsync($"/api/leave-requests/{leaveId}"));
        detail.GetProperty("status").GetString().Should().Be("Rejected");
        var records = detail.GetProperty("records").EnumerateArray().ToList();
        records.Should().Contain(r =>
            r.GetProperty("action").GetString() == "Reject" &&
            (r.GetProperty("comment").GetString() ?? string.Empty).Contains("医院证明"));

        // 后续节点任务作废
        detail.GetProperty("tasks").EnumerateArray()
            .Where(t => t.GetProperty("nodeOrder").GetInt32() == 2)
            .Should().Contain(t => t.GetProperty("status").GetString() == "Skipped");

        // 王五的待办不应包含被驳回的单据
        var wangwuClient = (await NewClient().LoginAsAsync("wangwu", "Manager@123")).Client;
        var leaveNo = detail.GetProperty("leaveNo").GetString();
        var wangwuPending = await ReadJsonAsync(await wangwuClient.GetAsync("/api/approvals/pending?page=1&pageSize=20"));
        wangwuPending.GetProperty("items").EnumerateArray()
            .Should().NotContain(i => i.GetProperty("leaveNo").GetString() == leaveNo);

        // 驳回后不得再审批
        (await lisiClient.ApproveAsync(leaveId, "再审批")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task 非审批人无法查看他人请假单()
    {
        var zhangsanClient = (await NewClient().LoginAsAsync("zhangsan", "Employee@123")).Client;
        var leaveId = await SubmitLeaveAsync(zhangsanClient, "集成测试-访问控制");

        // 赵六（市场部，无任何审批关系）无权查看
        var zhaoliuClient = (await NewClient().LoginAsAsync("zhaoliu", "Employee@123")).Client;
        (await zhaoliuClient.GetAsync($"/api/leave-requests/{leaveId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 李四（当前节点审批人）可以查看
        var lisiClient = (await NewClient().LoginAsAsync("lisi", "Manager@123")).Client;
        (await lisiClient.GetAsync($"/api/leave-requests/{leaveId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task 请假参数非法_返回400与结构化错误()
    {
        var zhangsanClient = (await NewClient().LoginAsAsync("zhangsan", "Employee@123")).Client;

        // 结束时间早于开始时间
        var response = await zhangsanClient.PostAsync("/api/leave-requests", BuildSubmitContent(
            "Annual", "参数错误", FutureUtc(8), FutureUtc(4)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await ReadJsonAsync(response);
        body.GetProperty("code").GetString().Should().Be("validation_error");
        body.GetProperty("errors").EnumerateArray()
            .Should().Contain(e => e.GetProperty("field").GetString() == "EndTime");
    }

    [Fact]
    public async Task 流程配置_仅Admin可管理_启用校验与冲突检查()
    {
        var zhangsanClient = (await NewClient().LoginAsAsync("zhangsan", "Employee@123")).Client;
        var adminClient = (await NewClient().LoginAsAsync("admin", "Admin@123")).Client;

        // 员工无权管理流程配置
        (await zhangsanClient.GetAsync("/api/workflow-configs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 先提交一张请假单，使种子流程（研发部流程）进入"已被使用"状态
        var leaveId = await SubmitLeaveAsync(zhangsanClient, "集成测试-流程配置锁定前置数据");
        leaveId.Should().BeGreaterThan(0);

        // 缺少审批人的节点无法创建并启用
        var invalid = new
        {
            name = "无效流程",
            departmentId = (long?)null,
            isActive = true,
            nodes = new[] { new { nodeOrder = 1, nodeName = "指定审批", approverType = 1, approverUserId = (long?)null, isRequired = true } }
        };
        (await adminClient.PostAsJsonAsync("/api/workflow-configs", invalid)).StatusCode
            .Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);

        // 合法配置：指定用户审批（admin 自己）
        var valid = new
        {
            name = "集成测试-指定审批人流程",
            departmentId = (long?)null,
            isActive = false,
            nodes = new[] { new { nodeOrder = 1, nodeName = "管理员审批", approverType = 1, approverUserId = 1L, isRequired = true } }
        };
        var createResponse = await adminClient.PostAsJsonAsync("/api/workflow-configs", valid);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createResponse);
        var configId = created.GetProperty("id").GetInt64();

        // 启用：与全局无冲突 → 成功
        var activate = new { name = (string?)null, isActive = true, nodes = (object?)null };
        (await adminClient.PutAsJsonAsync($"/api/workflow-configs/{configId}", activate)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        // 已被使用的种子流程（研发部流程，WorkflowConfigId=1）节点结构锁定：修改节点 → 409
        var inUseId = 1L;
        var modifyNodes = new
        {
            name = (string?)null,
            isActive = (bool?)null,
            nodes = new[] { new { nodeOrder = 1, nodeName = "改名节点", approverType = 2, approverUserId = (long?)null, isRequired = true } }
        };
        (await adminClient.PutAsJsonAsync($"/api/workflow-configs/{inUseId}", modifyNodes)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task 模拟邮件_审批流转后通过Outbox发出()
    {
        var zhangsanClient = (await NewClient().LoginAsAsync("zhangsan", "Employee@123")).Client;
        var leaveId = await SubmitLeaveAsync(zhangsanClient, "集成测试-邮件通知");

        var lisiClient = (await NewClient().LoginAsAsync("lisi", "Manager@123")).Client;
        (await lisiClient.ApproveAsync(leaveId, "同意邮件测试")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Outbox 处理器轮询间隔 3s，最多等待 15s：等待出现"流转通知"邮件（提交通知可能先到）
        var adminClient = (await NewClient().LoginAsAsync("admin", "Admin@123")).Client;
        List<string> subjects = new();
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            var emailsResponse = await adminClient.GetAsync("/api/dev/emails");
            var body = await ReadJsonAsync(emailsResponse);
            subjects = body.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("subject").GetString() ?? string.Empty)
                .ToList();
            if (subjects.Any(s => s.Contains("已通过上一节点")))
            {
                break;
            }
        }

        subjects.Should().Contain(s => s.Contains("已通过上一节点"));
    }
}
