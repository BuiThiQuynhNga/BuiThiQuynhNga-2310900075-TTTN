using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ElectronicsShop.Data;
using ElectronicsShop.Models;

namespace ElectronicsShop.Controllers
{
    [Authorize]
    public class GioHangController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        private const decimal NguongMienPhiShip = 300000m;
        private const decimal PhiShipCoDinh = 30000m;
        private const int PhutHieuLucQr = 10;

        public GioHangController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
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

            // Giỏ hàng thay đổi -> mã QR cũ không còn đúng số tiền, vô hiệu hóa
            gioHang.PaymentMethod = null;
            gioHang.QrExpiresAt = null;

            _context.Orders.Update(gioHang);
            await _context.SaveChangesAsync();
        }

        // Trả về thông báo lỗi nếu có sản phẩm vượt quá tồn kho, ngược lại trả về null
        private string? TimSanPhamThieuHang(Order gioHang)
        {
            var thieuHang = gioHang.OrderDetails
                .Where(ct => ct.Quantity > ct.Product.Stock)
                .ToList();

            if (!thieuHang.Any()) return null;

            var tenSp = string.Join(", ", thieuHang.Select(ct => ct.Product.ProductName));
            return $"Sản phẩm sau không đủ hàng trong kho: {tenSp}. Vui lòng cập nhật lại số lượng.";
        }

        // Chốt đơn: trừ kho, tăng lượt dùng mã giảm giá, chuyển trạng thái sang Pending
        private async Task HoanTatDatHang(Order gioHang)
        {
            foreach (var ct in gioHang.OrderDetails)
            {
                ct.Product.Stock -= ct.Quantity;
            }

            gioHang.OrderStatus = "Pending";
            gioHang.OrderDate = DateTime.Now;
            gioHang.QrExpiresAt = null;

            if (gioHang.DiscountCode != null)
            {
                gioHang.DiscountCode.UsedQuantity += 1;
            }

            await _context.SaveChangesAsync();
        }

        private static string TaoUrlVietQr(string bankId, string soTaiKhoan, string tenChuTk, long soTien, string noiDung)
        {
            var url = $"https://img.vietqr.io/image/{Uri.EscapeDataString(bankId)}-{Uri.EscapeDataString(soTaiKhoan)}-compact2.png"
                    + $"?amount={soTien}&addInfo={Uri.EscapeDataString(noiDung)}";

            if (!string.IsNullOrWhiteSpace(tenChuTk))
            {
                url += $"&accountName={Uri.EscapeDataString(tenChuTk)}";
            }
            return url;
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

            // Quay lại đúng trang người dùng vừa bấm (danh sách hoặc chi tiết)
            var referer = Request.Headers["Referer"].ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                // Chỉ lấy phần đường dẫn nên không bị chuyển hướng sang trang ngoài
                return LocalRedirect(uri.PathAndQuery);
            }

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

        // ================== THANH TOÁN ==================

        // GET: trang chọn phương thức thanh toán
        public async Task<IActionResult> ThanhToan()
        {
            var gioHang = await LayHoacTaoGioHang();
            if (!gioHang.OrderDetails.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            var loi = TimSanPhamThieuHang(gioHang);
            if (loi != null)
            {
                TempData["ErrorMessage"] = loi;
                return RedirectToAction("Index");
            }

            return View(gioHang);
        }

        // POST: COD -> đặt hàng luôn; QR -> sang trang hiện mã QR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ThanhToan(string diaChiGiaoHang, string soDienThoaiNhan, string phuongThuc = "COD")
        {
            var gioHang = await LayHoacTaoGioHang();

            if (!gioHang.OrderDetails.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(diaChiGiaoHang) || string.IsNullOrWhiteSpace(soDienThoaiNhan))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ địa chỉ và số điện thoại nhận hàng.");
                ViewBag.DiaChi = diaChiGiaoHang;
                ViewBag.Sdt = soDienThoaiNhan;
                ViewBag.PhuongThuc = phuongThuc;
                return View(gioHang);
            }

            var loi = TimSanPhamThieuHang(gioHang);
            if (loi != null)
            {
                TempData["ErrorMessage"] = loi;
                return RedirectToAction("Index");
            }

            gioHang.ShippingAddress = diaChiGiaoHang.Trim();
            gioHang.ReceiverPhone = soDienThoaiNhan.Trim();

            if (phuongThuc == "QR")
            {
                gioHang.PaymentMethod = "QR";
                gioHang.QrExpiresAt = DateTime.Now.AddMinutes(PhutHieuLucQr);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(ThanhToanQr));
            }

            gioHang.PaymentMethod = "COD";
            await HoanTatDatHang(gioHang);

            TempData["SuccessMessage"] = "Đặt hàng thành công!";
            return RedirectToAction("Index", "Home");
        }

        // GET: trang hiện mã QR (có hiệu lực 10 phút)
        public async Task<IActionResult> ThanhToanQr()
        {
            var gioHang = await LayHoacTaoGioHang();

            if (!gioHang.OrderDetails.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            // Chưa chọn QR, hoặc đã bấm Quay lại / đổi giỏ hàng -> về trang chọn phương thức
            if (gioHang.PaymentMethod != "QR" || gioHang.QrExpiresAt == null)
            {
                return RedirectToAction(nameof(ThanhToan));
            }

            string bankId = _config["ThanhToanQR:BankId"] ?? "";
            string soTaiKhoan = _config["ThanhToanQR:AccountNo"] ?? "";
            string tenChuTk = _config["ThanhToanQR:AccountName"] ?? "";

            long soTien = (long)Math.Round(gioHang.TotalAmount);
            string noiDung = "DH" + gioHang.OrderID;

            TimeSpan conLai = gioHang.QrExpiresAt.Value - DateTime.Now;
            bool hetHan = conLai <= TimeSpan.Zero;
            bool thieuCauHinh = string.IsNullOrWhiteSpace(bankId) || string.IsNullOrWhiteSpace(soTaiKhoan);

            ViewBag.HetHan = hetHan;
            ViewBag.ThieuCauHinh = thieuCauHinh;
            ViewBag.SoGiayConLai = hetHan ? 0 : (int)Math.Ceiling(conLai.TotalSeconds);
            ViewBag.NoiDung = noiDung;
            ViewBag.SoTien = soTien;
            ViewBag.BankId = bankId;
            ViewBag.SoTaiKhoan = soTaiKhoan;
            ViewBag.TenChuTk = tenChuTk;
            ViewBag.QrUrl = (hetHan || thieuCauHinh)
                ? ""
                : TaoUrlVietQr(bankId, soTaiKhoan, tenChuTk, soTien, noiDung);

            return View(gioHang);
        }

        // POST: mã hết hạn -> tạo mã mới (thêm 10 phút)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoLaiMaQr()
        {
            var gioHang = await LayHoacTaoGioHang();

            if (!gioHang.OrderDetails.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            var loi = TimSanPhamThieuHang(gioHang);
            if (loi != null)
            {
                TempData["ErrorMessage"] = loi;
                return RedirectToAction("Index");
            }

            gioHang.PaymentMethod = "QR";
            gioHang.QrExpiresAt = DateTime.Now.AddMinutes(PhutHieuLucQr);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ThanhToanQr));
        }

        // POST: nút Quay lại -> hủy mã QR, về trang chọn phương thức (giỏ hàng vẫn còn)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuayLaiTuQr()
        {
            var gioHang = await LayHoacTaoGioHang();
            gioHang.PaymentMethod = null;
            gioHang.QrExpiresAt = null;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ThanhToan));
        }

        // POST: khách bấm "Tôi đã chuyển khoản" -> chốt đơn (admin đối chiếu sao kê sau)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanDaChuyenKhoan()
        {
            var gioHang = await LayHoacTaoGioHang();

            if (!gioHang.OrderDetails.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index");
            }

            if (gioHang.PaymentMethod != "QR" || gioHang.QrExpiresAt == null)
            {
                return RedirectToAction(nameof(ThanhToan));
            }

            // Mã hết hiệu lực thì không cho xác nhận
            if (gioHang.QrExpiresAt.Value < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Mã QR đã hết hiệu lực. Vui lòng tạo mã mới.";
                return RedirectToAction(nameof(ThanhToanQr));
            }

            var loi = TimSanPhamThieuHang(gioHang);
            if (loi != null)
            {
                TempData["ErrorMessage"] = loi;
                return RedirectToAction("Index");
            }

            await HoanTatDatHang(gioHang);

            TempData["SuccessMessage"] = "Đã ghi nhận đơn hàng chuyển khoản. Cửa hàng sẽ xác nhận sau khi nhận được tiền.";
            return RedirectToAction("Index", "Home");
        }
    }
}