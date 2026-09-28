using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElectronicsShop.Models
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int OrderID { get; set; }

        public int UserID { get; set; }

        public int? DiscountCodeID { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; }

        public string OrderStatus { get; set; } = "Pending";

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        public string ShippingAddress { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string ReceiverPhone { get; set; }

        // MỚI: phương thức thanh toán ("COD" hoặc "QR")
        [StringLength(20)]
        public string? PaymentMethod { get; set; }

        // MỚI: thời điểm mã QR hết hiệu lực (lúc tạo mã + 10 phút)
        public DateTime? QrExpiresAt { get; set; }

        // Navigation properties
        [ForeignKey("UserID")]
        public User User { get; set; }

        [ForeignKey("DiscountCodeID")]
        public DiscountCode DiscountCode { get; set; }

        // Khởi tạo danh sách rỗng để giỏ hàng mới tạo không bị null
        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}