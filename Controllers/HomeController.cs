using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;

namespace ElectronicsShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Nếu là Admin đã đăng nhập -> hiện Dashboard quản trị
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin"))
            {
                var thongKe = new DashboardViewModel
                {
                    TongSanPham = await _context.Products.CountAsync(),
                    TongDanhMuc = await _context.Categories.CountAsync(),
                    TongKhachHang = await _context.Users.CountAsync(n => n.Role == "Customer"),

                    TongDonHang = await _context.Orders.CountAsync(d => d.OrderStatus != "Cart"),
                    DonChoXacNhan = await _context.Orders.CountAsync(d => d.OrderStatus == "Pending"),
                    DonDangGiao = await _context.Orders.CountAsync(d => d.OrderStatus == "Shipping"),

                    DoanhThu = await _context.Orders
                        .Where(d => d.OrderStatus == "Success")
                        .SumAsync(d => (decimal?)d.TotalAmount) ?? 0,

                    SanPhamSapHet = await _context.Products
                        .Where(s => s.Stock <= 10)
                        .OrderBy(s => s.Stock)
                        .Take(5)
                        .ToListAsync(),

                    DonHangGanDay = await _context.Orders
                        .Include(d => d.User)
                        .Where(d => d.OrderStatus != "Cart")
                        .OrderByDescending(d => d.OrderDate)
                        .Take(5)
                        .ToListAsync()
                };

                return View("Dashboard", thongKe);
            }

            // Khách hàng / khách vãng lai -> trang chủ bán hàng như cũ
            var danhMucList = await _context.Categories.ToListAsync();
            var sanPhamList = await _context.Products.Take(6).ToListAsync();

            ViewBag.DanhMucList = danhMucList;
            return View(sanPhamList);
        }
    }
}