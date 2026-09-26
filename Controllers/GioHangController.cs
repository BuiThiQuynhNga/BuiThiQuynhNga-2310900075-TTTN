using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;

namespace ElectronicsShop.Controllers
{
    [Authorize]
    public class GioHangController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const decimal NguongMienPhiShip = 300000m;
        private const decimal PhiShipCoDinh = 30000m;

        public GioHangController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetMaNguoiDung()
        {
            var claim = User.FindFirst("MaNguoiDung");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        private async Task<Order> LayHoacTaoGioHang()
        {
            int maNguoiDung = GetMaNguoiDung();

            var gioHang = await _context.Orders
                .Include(d => d.OrderDetails)
                    .ThenInclude(ct => ct.Product)
                .Include(d => d.DiscountCode)
                .FirstOrDefaultAsync(d => d.UserID == maNguoiDung && d.OrderStatus == "Cart");

            if (gioHang == null)
            {
                gioHang = new Order
                {
                    UserID = maNguoiDung,
                    OrderDate = DateTime.Now,
                    TotalAmount = 0,
                    OrderStatus = "Cart",
                    ShippingAddress = "",
                    ReceiverPhone = ""
                };
                _context.Orders.Add(gioHang);
                await _context.SaveChangesAsync();
            }

            return gioHang;
        }

        private async Task CapNhatTongTien(Order gioHang)
        {
            decimal tongTruocGiam = gioHang.OrderDetails.Sum(ct => ct.Quantity * ct.UnitPrice);
            decimal soTienGiam = 0;

            if (gioHang.DiscountCode != null)
            {
                bool conHan = gioHang.DiscountCode.IsActive && gioHang.DiscountCode.ExpiryDate >= DateTime.Now;

                if (conHan)
                {
                    soTienGiam = tongTruocGiam * gioHang.DiscountCode.DiscountPercent / 100m;

                    if (gioHang.DiscountCode.MaxDiscountAmount.HasValue && soTienGiam > gioHang.DiscountCode.MaxDiscountAmount.Value)
                    {
                        soTienGiam = gioHang.DiscountCode.MaxDiscountAmount.Value;
                    }
                }
                else
                {
                    gioHang.DiscountCodeID = null;
                }
            }

            decimal tongSauGiam = tongTruocGiam - soTienGiam;

            gioHang.ShippingFee = (tongSauGiam >= NguongMienPhiShip || tongSauGiam <= 0) ? 0 : PhiShipCoDinh;

            gioHang.TotalAmount = tongSauGiam + gioHang.ShippingFee;
            _context.Orders.Update(gioHang);
            await _context.SaveChangesAsync();
        }

        public async Task<IActionResult> Index()
        {
            var gioHang = await LayHoacTaoGioHang();
            return View(gioHang);
        }

        [HttpPost]
        public async Task<IActionResult> ThemVaoGio(int maSanPham, int soLuong = 1)
        {
            var sanPham = await _context.Products.FindAsync(maSanPham);
            if (sanPham == null)
            {
                return NotFound();
            }

            var gioHang = await LayHoacTaoGioHang();

            var chiTiet = gioHang.OrderDetails.FirstOrDefault(ct => ct.ProductID == maSanPham);
            if (chiTiet != null)
            {
                chiTiet.Quantity += soLuong;
            }
            else
            {
                chiTiet = new OrderDetail
                {
                    OrderID = gioHang.OrderID,
                    ProductID = maSanPham,
                    Quantity = soLuong,
                    UnitPrice = sanPham.Price
                };
                _context.OrderDetails.Add(chiTiet);
            }

            await _context.SaveChangesAsync();

            gioHang.OrderDetails = await _context.OrderDetails
                .Where(ct => ct.OrderID == gioHang.OrderID)
                .ToListAsync();
            await CapNhatTongTien(gioHang);

            TempData["SuccessMessage"] = "Đã thêm sản phẩm vào giỏ hàng.";
            return RedirectToAction("Index", "SanPham");
        }

        [HttpPost]
        public async Task<IActionResult> CapNhatSoLuong(int maChiTiet, int soLuong)
        {
            var chiTiet = await _context.OrderDetails
                .Include(ct => ct.Order)
                .FirstOrDefaultAsync(ct => ct.OrderDetailID == maChiTiet);

            if (chiTiet == null || chiTiet.Order.UserID != GetMaNguoiDung())
            {
                return NotFound();
            }

            if (soLuong <= 0)
            {
                _context.OrderDetails.Remove(chiTiet);
            }
            else
            {
                chiTiet.Quantity = soLuong;
            }
            await _context.SaveChangesAsync();

            var gioHang = await LayHoacTaoGioHang();
            await CapNhatTongTien(gioHang);

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> XoaSanPham(int maChiTiet)
        {
            var chiTiet = await _context.OrderDetails
                .Include(ct => ct.Order)
                .FirstOrDefaultAsync(ct => ct.OrderDetailID == maChiTiet);

            if (chiTiet == null || chiTiet.Order.UserID != GetMaNguoiDung())
            {
                return NotFound();
            }

            _context.OrderDetails.Remove(chiTiet);
            await _context.SaveChangesAsync();

            var gioHang = await LayHoacTaoGioHang();
            await CapNhatTongTien(gioHang);

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ApDungMaGiamGia(string tenVoucher)
        {
            var gioHang = await LayHoacTaoGioHang();

            if (string.IsNullOrWhiteSpace(tenVoucher))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập mã giảm giá.";
                return RedirectToAction("Index");
            }

            var voucher = await _context.DiscountCodes
                .FirstOrDefaultAsync(v => v.CodeName.ToLower() == tenVoucher.Trim().ToLower());

            if (voucher == null)
            {
                TempData["ErrorMessage"] = "Mã giảm giá không tồn tại.";
                return RedirectToAction("Index");
            }

            if (!voucher.IsActive)
            {
                TempData["ErrorMessage"] = "Mã giảm giá này đã ngừng hoạt động.";
                return RedirectToAction("Index");
            }

            if (voucher.ExpiryDate < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Mã giảm giá này đã hết hạn.";
                return RedirectToAction("Index");
            }

            if (voucher.UsedQuantity >= voucher.TotalQuantity)
            {
                TempData["ErrorMessage"] = "Mã giảm giá này đã hết lượt sử dụng.";
                return RedirectToAction("Index");
            }

            gioHang.DiscountCodeID = voucher.DiscountCodeID;
            gioHang.DiscountCode = voucher;
            await CapNhatTongTien(gioHang);

            TempData["SuccessMessage"] = $"Áp dụng mã \"{voucher.CodeName}\" thành công! Giảm {voucher.DiscountPercent}%.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> GoMaGiamGia()
        {
            var gioHang = await LayHoacTaoGioHang();
            gioHang.DiscountCodeID = null;
            gioHang.DiscountCode = null;
            await CapNhatTongTien(gioHang);

            TempData["SuccessMessage"] = "Đã gỡ mã giảm giá.";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> ThanhToan()
        {
            var gioHang = await LayHoacTaoGioHang();
            if (!gioHang.OrderDetails.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            var thieuHang = gioHang.OrderDetails
                .Where(ct => ct.Quantity > ct.Product.Stock)
                .ToList();

            if (thieuHang.Any())
            {
                var tenSp = string.Join(", ", thieuHang.Select(ct => ct.Product.ProductName));
                TempData["ErrorMessage"] = $"Sản phẩm sau không đủ hàng trong kho: {tenSp}. Vui lòng cập nhật lại số lượng.";
                return RedirectToAction("Index");
            }

            return View(gioHang);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThanhToan(string diaChiGiaoHang, string soDienThoaiNhan)
        {
            var gioHang = await LayHoacTaoGioHang();

            if (string.IsNullOrWhiteSpace(diaChiGiaoHang) || string.IsNullOrWhiteSpace(soDienThoaiNhan))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ địa chỉ và số điện thoại nhận hàng.");
                return View(gioHang);
            }

            var thieuHang = gioHang.OrderDetails
                .Where(ct => ct.Quantity > ct.Product.Stock)
                .ToList();

            if (thieuHang.Any())
            {
                var tenSp = string.Join(", ", thieuHang.Select(ct => ct.Product.ProductName));
                TempData["ErrorMessage"] = $"Sản phẩm sau không đủ hàng trong kho: {tenSp}. Vui lòng cập nhật lại số lượng.";
                return RedirectToAction("Index");
            }

            foreach (var ct in gioHang.OrderDetails)
            {
                ct.Product.Stock -= ct.Quantity;
            }

            gioHang.ShippingAddress = diaChiGiaoHang;
            gioHang.ReceiverPhone = soDienThoaiNhan;
            gioHang.OrderStatus = "Pending";
            gioHang.OrderDate = DateTime.Now;

            if (gioHang.DiscountCode != null)
            {
                gioHang.DiscountCode.UsedQuantity += 1;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đặt hàng thành công!";
            return RedirectToAction("Index", "Home");
        }
    }
}