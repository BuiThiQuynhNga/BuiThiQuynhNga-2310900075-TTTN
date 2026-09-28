using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;
using System.IO;

namespace ElectronicsShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminSanPhamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AdminSanPhamController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Chỉ giữ lại kiểm tra cho các trường thực sự có trên form.
        // Mọi trường khác (Category, Reviews, OrderDetails, ImageURL, ColorVariant...) bị bỏ qua.
        private void ChiKiemTraTruongTrenForm()
        {
            var truongTrenForm = new[]
            {
                "ProductID", "ProductName", "CategoryID", "Description", "Price", "Stock"
            };

            foreach (var key in ModelState.Keys.ToList())
            {
                if (!truongTrenForm.Contains(key))
                {
                    ModelState.Remove(key);
                }
            }
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
            ChiKiemTraTruongTrenForm();

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

            // Hàng điện tử không dùng mã màu, gán rỗng để cột trong database không bị null
            product.ColorVariant = "";

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

            ChiKiemTraTruongTrenForm();

            if (!ModelState.IsValid)
            {
                ViewBag.DanhMucList = await _context.Categories.ToListAsync();
                return View(product);
            }

            var sp = await _context.Products.FindAsync(id);
            if (sp == null)
            {
                return NotFound();
            }

            sp.ProductName = product.ProductName;
            sp.CategoryID = product.CategoryID;
            sp.Description = product.Description;
            sp.Price = product.Price;
            sp.Stock = product.Stock;

            if (anhUpload != null && anhUpload.Length > 0)
            {
                string anhMoi = await LuuAnh(anhUpload);
                if (!string.IsNullOrEmpty(anhMoi))
                {
                    sp.ImageURL = anhMoi;
                }
            }

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
                try
                {
                    _context.Products.Remove(product);
                    await _context.SaveChangesAsync();
                    TempData["ThongBao"] = "Xóa sản phẩm thành công!";
                }
                catch (DbUpdateException)
                {
                    TempData["LoiXoa"] = "Không thể xóa: sản phẩm này đã có trong đơn hàng hoặc đánh giá.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // Lưu ảnh với tên file an toàn: chỉ gồm mã ngẫu nhiên + đuôi file
        private async Task<string> LuuAnh(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "images");
            Directory.CreateDirectory(uploadsFolder);

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            string[] choPhep = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            if (!choPhep.Contains(ext))
            {
                return "";
            }

            string uniqueFileName = Guid.NewGuid().ToString("N") + ext;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            return uniqueFileName;
        }
    }
}