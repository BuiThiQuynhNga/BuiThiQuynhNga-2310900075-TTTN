using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElectronicsShop.Models
{
    [Table("Reviews")]
    public class Review
    {
        [Key]
        public int ReviewID { get; set; }

        public int ProductID { get; set; }

        public int UserID { get; set; }

        public int Rating { get; set; }

        public string Comment { get; set; }

        public DateTime ReviewDate { get; set; } = DateTime.Now;

        [ForeignKey("ProductID")]
        public Product Product { get; set; }

        [ForeignKey("UserID")]
        public User User { get; set; }
    }
}