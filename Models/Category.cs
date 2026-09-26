using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElectronicsShop.Models
{
    [Table("Categories")]
    public class Category
    {
        [Key]
        public int CategoryID { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
        public string CategoryName { get; set; }

        public string Description { get; set; }

        // Navigation property
        public ICollection<Product> Products { get; set; }
    }
}