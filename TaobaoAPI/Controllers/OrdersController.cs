using Microsoft.AspNetCore.Mvc;
using TaobaoAPI.Models;
using Microsoft.EntityFrameworkCore;
using TaobaoAPI.Data;
namespace TaobaoAPI.Controllers
{
    [ApiController] // 标记为API控制器
    [Route("api/[controller]")] // 定义路由，访问路径为/api/orders
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public OrdersController(AppDbContext context)
        {
            _context = context;

        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncOrders([FromBody] List<OrderDto> orders)
        {
            if (orders == null || orders.Count == 0)
            {
                return BadRequest("没有任何订单数据");
            }
            foreach (var order in orders)
            {
                Console.WriteLine($"[抓取成功] 订单号:{order.OrderId} | 商品:{order.Title} | 价格:{order.price}");
                var existingOrder = await _context.Orders.FindAsync(order.OrderId);
                if(existingOrder ==  null)
                {
                    _context.Orders.Add(order);
                }
                else
                {
                    existingOrder.Title = order.Title;
                    existingOrder.price = order.price;
                    existingOrder.BuyTime = order.BuyTime;
                }
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = $"成功同步了{orders.Count}条记录!" });
        }
        [HttpGet]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders.ToListAsync();
            return Ok(orders);
        }

    }
}