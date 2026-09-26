using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;

namespace ElectronicsShop.Controllers
{
    public class MaGiamGiaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MaGiamGiaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /MaGiamGia — CÔNG KHAI, ai cũng xem được, chỉ thấy tên/%/còn-hết
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var danhSach = await _context.DiscountCodes
                .Where(v => v.IsActive && v.ExpiryDate >= DateTime.Now)
                .OrderByDescending(v => v.DiscountCodeID)
                .ToListAsync();
            return View(danhSach);
        }

        // GET: /MaGiamGia/QuanLy — CHỈ QUẢN TRỊ, xem đầy đủ + thao tác
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> QuanLy()
        {
            var danhSach = await _context.DiscountCodes
                .OrderByDescending(v => v.DiscountCodeID)
                .ToListAsync();
            return View(danhSach);
        }

        // GET: /MaGiamGia/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new DiscountCode
            {
                ExpiryDate = DateTime.Now.AddDays(7),
                IsActive = true,
                TotalQuantity = 100
            });
        }

        // POST: /MaGiamGia/Create
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DiscountCode model)
        {
            ModelState.Remove("Orders");

            if (string.IsNullOrWhiteSpace(model.CodeName))
            {
                ModelState.AddModelError(nameof(model.CodeName), "Vui lòng nhập mã voucher.");
            }
            else
            {
                bool trung = await _context.DiscountCodes
                    .AnyAsync(v => v.CodeName.ToLower() == model.CodeName.Trim().ToLower());
                if (trung)
                {
                    ModelState.AddModelError(nameof(model.CodeName), "Mã voucher này đã tồn tại.");
                }
            }

            if (model.DiscountPercent <= 0 || model.DiscountPercent > 100)
            {
                ModelState.AddModelError(nameof(model.DiscountPercent), "Phần trăm giảm phải trong khoảng 1-100.");
            }

            if (model.MaxDiscountAmount.HasValue && model.MaxDiscountAmount <= 0)
            {
                ModelState.AddModelError(nameof(model.MaxDiscountAmount), "Giá trị giảm tối đa phải lớn hơn 0.");
            }

            if (model.TotalQuantity <= 0)
            {
                ModelState.AddModelError(nameof(model.TotalQuantity), "Số lượng mã phải lớn hơn 0.");
            }

            if (model.ExpiryDate <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(model.ExpiryDate), "Ngày hết hạn phải ở tương lai.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.CodeName = model.CodeName.Trim().ToUpper();
            model.UsedQuantity = 0;
            _context.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo mã giảm giá \"{model.CodeName}\".";
            return RedirectToAction(nameof(QuanLy));
        }

        // GET: /MaGiamGia/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var voucher = await _context.DiscountCodes.FindAsync(id);
            if (voucher == null) return NotFound();
            return View(voucher);
        }

        // POST: /MaGiamGia/Edit/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DiscountCode model)
        {
            if (id != model.DiscountCodeID) return NotFound();

            ModelState.Remove("Orders");

            var voucher = await _context.DiscountCodes.FindAsync(id);
            if (voucher == null) return NotFound();

            if (model.DiscountPercent <= 0 || model.DiscountPercent > 100)
            {
                ModelState.AddModelError(nameof(model.DiscountPercent), "Phần trăm giảm phải trong khoảng 1-100.");
            }

            if (model.TotalQuantity < voucher.UsedQuantity)
            {
                ModelState.AddModelError(nameof(model.TotalQuantity), $"Số lượng mới không được nhỏ hơn số đã dùng ({voucher.UsedQuantity}).");
            }

            if (!ModelState.IsValid)
            {
                model.CodeName = voucher.CodeName;
                model.UsedQuantity = voucher.UsedQuantity;
                return View(model);
            }

            voucher.DiscountPercent = model.DiscountPercent;
            voucher.MaxDiscountAmount = model.MaxDiscountAmount;
            voucher.ExpiryDate = model.ExpiryDate;
            voucher.IsActive = model.IsActive;
            voucher.TotalQuantity = model.TotalQuantity;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật mã giảm giá.";
            return RedirectToAction(nameof(QuanLy));
        }

        // POST: /MaGiamGia/ToggleTrangThai/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ToggleTrangThai(int id)
        {
            var voucher = await _context.DiscountCodes.FindAsync(id);
            if (voucher == null) return NotFound();

            voucher.IsActive = !voucher.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(QuanLy));
        }

        // POST: /MaGiamGia/Delete/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var voucher = await _context.DiscountCodes.FindAsync(id);
            if (voucher == null) return NotFound();

            bool dangDuocDung = await _context.Orders.AnyAsync(d => d.DiscountCodeID == id);
            if (dangDuocDung)
            {
                TempData["ErrorMessage"] = "Không thể xóa: mã này đang được gắn với đơn hàng/giỏ hàng. Hãy tắt trạng thái thay vì xóa.";
                return RedirectToAction(nameof(QuanLy));
            }

            _context.Remove(voucher);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa mã giảm giá.";
            return RedirectToAction(nameof(QuanLy));
        }
    }
}