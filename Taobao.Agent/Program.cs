using System.Net.Http.Json;
Console.WriteLine("淘宝数据采集已启动");
var apiUrl = "https://localhost:7177/api/Orders/sync";
var mockOrders = new[]
{
    new
    {
        orderId = "Agent_" + Guid.NewGuid().ToString().Substring(0,8),
        title = "测试订单",
        price = 499m,
        buyTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
    }
};
