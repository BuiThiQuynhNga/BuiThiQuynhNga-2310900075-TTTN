using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;

namespace ElectronicsShop.Controllers
{
    public class SanPhamController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SanPhamController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Danh sách sản phẩm (có thể lọc theo danh mục hoặc từ khóa)
        public async Task<IActionResult> Index(int? maDanhMuc, string? keyword)
        {
            var query = _context.Products
                .Include(s => s.Category)
                .AsQueryable();

            if (maDanhMuc.HasValue)
            {
                query = query.Where(s => s.CategoryID == maDanhMuc.Value);
                var danhMuc = await _context.Categories.FindAsync(maDanhMuc.Value);
                ViewBag.TenDanhMuc = danhMuc?.CategoryName;
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(s => s.ProductName.Contains(keyword));
            }

            var danhSachSanPham = await query.ToListAsync();
            return View(danhSachSanPham);
        }

        // Chi tiết sản phẩm
        public async Task<IActionResult> ChiTiet(int id)
        {
            var sanPham = await _context.Products
                .Include(s => s.Category)
                .FirstOrDefaultAsync(s => s.ProductID == id);

            if (sanPham == null)
            {
                return NotFound();
            }

            var danhGiaList = await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductID == id)
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();

            ViewBag.DanhGiaList = danhGiaList;
            return View(sanPham);
        }
    }
}