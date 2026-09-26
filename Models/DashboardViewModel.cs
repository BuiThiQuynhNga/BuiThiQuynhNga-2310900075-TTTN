using System.Collections.Generic;

namespace ElectronicsShop.Models
{
    public class DashboardViewModel
    {
        public int TongSanPham { get; set; }
        public int TongDanhMuc { get; set; }
        public int TongKhachHang { get; set; }
        public int TongDonHang { get; set; }
        public int DonChoXacNhan { get; set; }
        public int DonDangGiao { get; set; }
        public decimal DoanhThu { get; set; }
        public List<Product> SanPhamSapHet { get; set; } = new();
        public List<Order> DonHangGanDay { get; set; } = new();
    }
}