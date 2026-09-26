using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ElectronicsShop.Controllers
{
    [Authorize]
    public class DonHangController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DonHangController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetMaNguoiDung()
        {
            var claim = User.FindFirst("MaNguoiDung");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        // GET: /DonHang (Admin xem toàn bộ đơn hàng)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var danhSach = await _context.Orders
                .Include(d => d.User)
                .Where(d => d.OrderStatus != "Cart")
                .OrderByDescending(d => d.OrderDate)
                .ToListAsync();

            return View(danhSach);
        }

        // POST: /DonHang/CapNhatTrangThai (Admin đổi trạng thái đơn hàng)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatTrangThai(int id, string trangThai)
        {
            var donHang = await _context.Orders.FindAsync(id);
            if (donHang == null)
            {
                return NotFound();
            }

            var trangThaiHopLe = new[] { "Pending", "Processing", "Shipping", "Success", "Cancelled" };
            if (!trangThaiHopLe.Contains(trangThai))
            {
                return BadRequest();
            }

            donHang.OrderStatus = trangThai;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật đơn hàng #{donHang.OrderID} sang trạng thái \"{trangThai}\".";
            return RedirectToAction("Index");
        }

        // GET: /DonHang/LichSu (Khách xem đơn hàng của chính mình)
        public async Task<IActionResult> LichSu()
        {
            int maNguoiDung = GetMaNguoiDung();

            var danhSach = await _context.Orders
                .Include(d => d.OrderDetails)
                    .ThenInclude(ct => ct.Product)
                .Where(d => d.UserID == maNguoiDung && d.OrderStatus != "Cart")
                .OrderByDescending(d => d.OrderDate)
                .ToListAsync();

            return View(danhSach);
        }

        // GET: /DonHang/ChiTiet/5
        public async Task<IActionResult> ChiTiet(int id)
        {
            var donHang = await _context.Orders
                .Include(d => d.User)
                .Include(d => d.OrderDetails)
                    .ThenInclude(ct => ct.Product)
                .FirstOrDefaultAsync(d => d.OrderID == id);

            if (donHang == null)
            {
                return NotFound();
            }

            bool laAdmin = User.IsInRole("Admin");
            if (!laAdmin && donHang.UserID != GetMaNguoiDung())
            {
                return Forbid();
            }

            return View(donHang);
        }
    }
}