using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElectronicsShop.Models
{
    [Table("DiscountCodes")]
    public class DiscountCode
    {
        [Key]
        public int DiscountCodeID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã code")]
        public string CodeName { get; set; }

        public int DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        public int TotalQuantity { get; set; }

        public int UsedQuantity { get; set; }

        // Navigation property
        public ICollection<Order> Orders { get; set; }
    }
}