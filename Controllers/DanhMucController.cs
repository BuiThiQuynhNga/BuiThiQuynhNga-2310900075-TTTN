using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;

namespace ElectronicsShop.Controllers
{
    public class DanhMucController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DanhMucController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /DanhMuc
        // Ai cũng xem được, không cần đăng nhập
        public async Task<IActionResult> Index()
        {
            var danhSach = await _context.Categories.OrderByDescending(d => d.CategoryID).ToListAsync();
            return View(danhSach);
        }

        // GET: /DanhMuc/Them
        [Authorize(Roles = "Admin")]
        public IActionResult Them()
        {
            return View();
        }

        // POST: /DanhMuc/Them
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Them(Category model)
        {
            ModelState.Remove("Products");

            if (ModelState.IsValid)
            {
                _context.Categories.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm danh mục thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: /DanhMuc/Sua/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Sua(int id)
        {
            var danhMuc = await _context.Categories.FindAsync(id);
            if (danhMuc == null) return NotFound();
            return View(danhMuc);
        }

        // POST: /DanhMuc/Sua/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sua(int id, Category model)
        {
            if (id != model.CategoryID) return NotFound();

            ModelState.Remove("Products");

            if (ModelState.IsValid)
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: /DanhMuc/Xoa/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Xoa(int id)
        {
            var danhMuc = await _context.Categories.FindAsync(id);
            if (danhMuc == null) return NotFound();
            return View(danhMuc);
        }

        // POST: /DanhMuc/Xoa/5
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaConfirmed(int id)
        {
            var danhMuc = await _context.Categories.FindAsync(id);
            if (danhMuc != null)
            {
                // Kiểm tra xem có sản phẩm nào thuộc danh mục này không
                bool coSanPham = await _context.Products.AnyAsync(sp => sp.CategoryID == id);
                if (coSanPham)
                {
                    TempData["ErrorMessage"] = "Không thể xóa danh mục đang có sản phẩm!";
                    return RedirectToAction(nameof(Index));
                }

                _context.Categories.Remove(danhMuc);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Xóa danh mục thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}