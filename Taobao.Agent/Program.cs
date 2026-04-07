using System.Diagnostics;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
Console.WriteLine("淘宝数据采集已启动");
var appData = Environment.GetEnvironmentVariable("APPDATA");
var locationFile = Path.Combine(appData, "taobao", "install-location.txt");
var exePath = "taobao-native.cmd";
if(File.Exists(locationFile))
{
    var location = File.ReadAllText(locationFile).Trim();
    exePath = Path.Combine(location,"bin","taobao-native.cmd");
}
var sourceApp = "TaobaoAgent";

Console.WriteLine(">>> [步骤 1/2] 正在命令淘宝客户端打开“我的订单”...");
// 猜测订单页面的预设名字是 order（如果不对，淘宝会返回正确的列表给你）
var navArgs = $"{{\"page\":\"order\",\"sourceApp\":\"{sourceApp}\"}}";
var navProcessInfo = new ProcessStartInfo
{
    FileName = exePath,
    WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
    Arguments = $"navigate --args '{navArgs}'",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    StandardOutputEncoding = System.Text.Encoding.UTF8,
    StandardErrorEncoding = System.Text.Encoding.UTF8,
    UseShellExecute = false,
    CreateNoWindow = true
};

try
{
    // 1. 发送跳转指令
    using var navProcess = Process.Start(navProcessInfo);
    var navOutput = await navProcess.StandardOutput.ReadToEndAsync();
    await navProcess.WaitForExitAsync();

    // 如果名字猜错了，把它打印出来看看正确的名字是什么
    if (navOutput.Contains("error") || navOutput.Contains("available"))
    {
        Console.WriteLine($"[提示] 淘宝反馈: {navOutput}");
    }

    // 核心细节：让 C# 程序睡 3 秒钟。因为页面打开和渲染需要时间，不能立刻去读！
    Console.WriteLine("页面跳转指令已发送，等待 3 秒钟让淘宝加载订单数据...");
    await Task.Delay(3000);
    var clickArgs = $"{{\"text\":\"查看物流\",\"sourceApp\":\"{sourceApp}\"}}";
    var clickProcessInfo = new ProcessStartInfo
    {
        FileName = exePath,
        WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
        Arguments = $"click_element --args '{clickArgs}'", // 使用 click_element 工具
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = System.Text.Encoding.UTF8,
        StandardErrorEncoding = System.Text.Encoding.UTF8,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    using var clickProcess = Process.Start(clickProcessInfo);
    await clickProcess.WaitForExitAsync();
    await Task.Delay(3000);

    Console.WriteLine(">>> [步骤 2/2] 正在扫描并提取页面上的订单内容...");

    // 遵从官方文档警告：数据量太大，必须用 -o 保存到本地文件
    var resultFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "my_orders_data.json");
    var readArgs = $"{{\"sourceApp\":\"{sourceApp}\"}}";

    var readProcessInfo = new ProcessStartInfo
    {
        FileName = exePath,
        WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
        Arguments = $"read_page_content --args '{readArgs}' -o \"{resultFile}\"", // 加上了 -o 参数，指定保存路径
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = System.Text.Encoding.UTF8,
        StandardErrorEncoding = System.Text.Encoding.UTF8,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    // 2. 发送读取页面的指令
    using var readProcess = Process.Start(readProcessInfo);
    var shortOutput = await readProcess.StandardOutput.ReadToEndAsync();
    await readProcess.WaitForExitAsync();

    // 3. 从生成的临时文件中，读取出完整、未被截断的真实页面数据
    if (File.Exists(resultFile))
    {
        var fullOutput = await File.ReadAllTextAsync(resultFile);
        Console.WriteLine("\n================ 截获的真实订单数据 ================");
        // 打印前 1500 个字符让你确认一下格式
        Console.WriteLine(fullOutput.Substring(0, Math.Min(fullOutput.Length, 1500)) + "....\n");
        Console.WriteLine("====================================================");
        var apiUrl = "https://localhost:7177/api/Orders/sync"; // 【修复1】这是你的大本营地址
        Console.WriteLine(">>> 正在提取信息...");

        var aiEndpoint = "https://integrate.api.nvidia.com/v1/chat/completions";
        var apiKey = "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";
        var modelName = "openai/gpt-oss-120b";

        var systemPrompt = @"你是一个数据提取专家。请从用户提供的淘宝页面复杂JSON数据中，提取出有效订单信息和深度物流轨迹。
请严格按照以下 JSON 数组的格式返回数据，不要输出任何多余的解释文字、也不要输出 Markdown 代码块符号(```json)：
[
  { 
    ""orderId"": ""订单号（如果没有提取到，请生成一个随机字符串）"", 
    ""title"": ""商品标题（如果有的话）"", 
    ""price"": 0, 
    ""buyTime"": """",
    ""status"": ""当前物流状态（如：已签收、派送中、已发货等）"",
    ""logistics"": ""请提取完整的物流时间线轨迹（如：2026-04-07 10:00 已签收... 2026-04-06 14:00 到达某分拨中心），请将所有轨迹节点按时间顺序用换行符 '\n' 拼接成一段完整的纯文本""
  }
]";

        // 组装发给 AI 的包裹
        var aiRequestBody = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = "以下是需要提取的淘宝原始JSON数据：\n" + fullOutput }
            },
            temperature = 0.1
        };

        using var aiClient = new HttpClient();
        aiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            // 向英伟达服务器发送请求
            var aiResponse = await aiClient.PostAsJsonAsync(aiEndpoint, aiRequestBody);
            if (aiResponse.IsSuccessStatusCode)
            {
                var aiResultJson = await aiResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(aiResultJson);
                var cleanJsonData = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                Console.WriteLine(">>> AI 提取成功！清洗后的纯净数据如下：");
                Console.WriteLine(cleanJsonData);

                Console.WriteLine($"\n>>> 正在将干净的订单数据同步至大本营: {apiUrl}");

                // 把干净的 JSON 发给你的 Web API
                var forwardContent = new StringContent(cleanJsonData, System.Text.Encoding.UTF8, "application/json");
                var syncResponse = await aiClient.PostAsync(apiUrl, forwardContent);

                if (syncResponse.IsSuccessStatusCode)
                {
                    var syncResult = await syncResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($">>> 同步成功！服务器反馈: {syncResult}");
                }
                else
                {
                    Console.WriteLine($">>> 同步到大本营失败，状态码: {syncResponse.StatusCode}");
                }
            }
            else
            {
                Console.WriteLine($"[错误] AI 接口调用失败: {aiResponse.StatusCode} - {await aiResponse.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[警告] 请求 AI 或同步数据时发生异常: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"[错误] 未能生成数据文件。淘宝返回的提示为: {shortOutput}");
    }

}
catch (Exception ex)
{
    Console.WriteLine($"[警告] 无法调用淘宝程序，报错: {ex.Message}");
    return;
}

Console.WriteLine("\n任务结束，按任意键返回");
Console.ReadKey();