using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;
using ElectronicsShop.Models;
using System.Security.Claims;

namespace ElectronicsShop.Controllers
{
    public class TaiKhoanController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaiKhoanController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult DangKy()
        {
            return View(new User());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangKy(
            string fullName,
            string email,
            string matKhau,
            string XacNhanMatKhau,
            string? phone,
            string? address)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("Email", "Vui lòng nhập email.");
            }

            if (string.IsNullOrWhiteSpace(matKhau))
            {
                ModelState.AddModelError("matKhau", "Vui lòng nhập mật khẩu.");
            }

            if (string.IsNullOrWhiteSpace(XacNhanMatKhau))
            {
                ModelState.AddModelError("XacNhanMatKhau", "Vui lòng xác nhận mật khẩu.");
            }

            if (!string.IsNullOrWhiteSpace(matKhau) &&
                !string.IsNullOrWhiteSpace(XacNhanMatKhau) &&
                matKhau != XacNhanMatKhau)
            {
                ModelState.AddModelError("XacNhanMatKhau", "Mật khẩu xác nhận không khớp.");
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                email = email.Trim();

                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                }
            }

            if (!ModelState.IsValid)
            {
                var viewModel = new User
                {
                    FullName = fullName ?? "",
                    Email = email ?? "",
                    Phone = phone ?? "",
                    Address = address ?? ""
                };

                return View(viewModel);
            }

            var user = new User
            {
                FullName = fullName.Trim(),
                Email = email.Trim(),
                Phone = phone?.Trim() ?? "",
                Address = address?.Trim() ?? "",
                Role = "Customer",
                CreatedAt = DateTime.Now
            };

            // Luu mat khau dang thuong (khong hash) - chi de test/hoc tap
            user.PasswordHash = matKhau;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";

            return RedirectToAction("DangNhap");
        }

        [HttpGet]
        public IActionResult DangNhap()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangNhap(string email, string matKhau)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(matKhau))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ email và mật khẩu.");
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email.Trim());

            if (user == null)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác.");
                return View();
            }

            // So sanh mat khau dang thuong (khong hash) - chi de test/hoc tap
            if (user.PasswordHash != matKhau)
            {
                ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác.");
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("MaNguoiDung", user.UserID.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var principal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal
            );

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> DangXuat()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            return RedirectToAction("Index", "Home");
        }

        public IActionResult KhongCoQuyen()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> ThongTin()
        {
            var maNguoiDungStr = User.FindFirst("MaNguoiDung")?.Value;

            if (string.IsNullOrEmpty(maNguoiDungStr) ||
                !int.TryParse(maNguoiDungStr, out int maNguoiDung))
            {
                return RedirectToAction("DangNhap");
            }

            var nguoiDung = await _context.Users
                .FirstOrDefaultAsync(u => u.UserID == maNguoiDung);

            if (nguoiDung == null)
            {
                return NotFound();
            }

            return View(nguoiDung);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhatThongTin(
            int maNguoiDung,
            string hoTen,
            string? soDienThoai,
            string? diaChi)
        {
            var maNguoiDungClaim = User.FindFirst("MaNguoiDung")?.Value;

            if (string.IsNullOrEmpty(maNguoiDungClaim) ||
                !int.TryParse(maNguoiDungClaim, out int maHienTai) ||
                maHienTai != maNguoiDung)
            {
                return Forbid();
            }

            var nguoiDung = await _context.Users.FindAsync(maNguoiDung);

            if (nguoiDung == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(hoTen))
            {
                ModelState.AddModelError("", "Họ tên không được để trống.");
                return View("ThongTin", nguoiDung);
            }

            nguoiDung.FullName = hoTen.Trim();
            nguoiDung.Phone = soDienThoai?.Trim() ?? "";
            nguoiDung.Address = diaChi?.Trim() ?? "";

            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Cập nhật thông tin thành công!";

            return RedirectToAction("ThongTin");
        }
    }
}