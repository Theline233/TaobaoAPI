using System.ComponentModel.DataAnnotations;

namespace TaobaoAPI.Models
{
    public class OrderDto
    {
        [Key]
        public string OrderId { get; set; }  // 订单ID
        public string Title { get; set; }   // 商品标题
        public decimal price { get; set; }  // 商品价格
        public string BuyTime { get; set; }   // 购买时间
    }
}
