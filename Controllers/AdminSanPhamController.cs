using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;
using System.IO;

namespace ElectronicsShop.Controllers
{
    public class AdminSanPhamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminSanPhamController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: /AdminSanPham
        public async Task<IActionResult> Index()
        {
            var danhSach = await _context.Products
                .Include(s => s.Category)
                .OrderByDescending(s => s.ProductID)
                .ToListAsync();

            return View(danhSach);
        }

        // GET: /AdminSanPham/Them
        public async Task<IActionResult> Them()
        {
            ViewBag.DanhMucList = await _context.Categories.ToListAsync();
            return View();
        }

        // POST: /AdminSanPham/Them
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Them(Product product, IFormFile? anhUpload)
        {
            ModelState.Remove("Category");
            ModelState.Remove("Reviews");

            if (!ModelState.IsValid)
            {
                ViewBag.DanhMucList = await _context.Categories.ToListAsync();
                return View(product);
            }

            if (anhUpload != null && anhUpload.Length > 0)
            {
                product.ImageURL = await LuuAnh(anhUpload);
            }
            else
            {
                product.ImageURL = "";
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminSanPham/Sua/5
        public async Task<IActionResult> Sua(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewBag.DanhMucList = await _context.Categories.ToListAsync();
            return View(product);
        }

        // POST: /AdminSanPham/Sua/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sua(int id, Product product, IFormFile? anhUpload)
        {
            if (id != product.ProductID)
            {
                return NotFound();
            }

            ModelState.Remove("Category");
            ModelState.Remove("Reviews");
            ModelState.Remove("ImageURL");

            if (!ModelState.IsValid)
            {
                ViewBag.DanhMucList = await _context.Categories.ToListAsync();
                return View(product);
            }

            var spCu = await _context.Products.AsNoTracking().FirstOrDefaultAsync(s => s.ProductID == id);
            if (spCu == null)
            {
                return NotFound();
            }

            // Xử lý ảnh
            if (anhUpload != null && anhUpload.Length > 0)
            {
                product.ImageURL = await LuuAnh(anhUpload);
            }
            else
            {
                // Giữ nguyên ảnh cũ nếu không chọn ảnh mới
                product.ImageURL = spCu.ImageURL;
            }

            // Bổ sung ngày tạo cũ để không bị ghi đè
            product.CreatedAt = spCu.CreatedAt;

            _context.Update(product);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminSanPham/Xoa/5
        public async Task<IActionResult> Xoa(int id)
        {
            var product = await _context.Products
                .Include(s => s.Category)
                .FirstOrDefaultAsync(m => m.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // POST: /AdminSanPham/Xoa/5
        [HttpPost, ActionName("Xoa")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                TempData["ThongBao"] = "Xóa sản phẩm thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // Hàm hỗ trợ lưu ảnh
        private async Task<string> LuuAnh(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "images");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return uniqueFileName;
        }
    }
}