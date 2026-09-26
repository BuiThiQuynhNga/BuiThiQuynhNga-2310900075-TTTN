using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ElectronicsShop.Data;

namespace ElectronicsShop.Components
{
    public class GioHangCountViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public GioHangCountViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int soLuong = 0;

            if (UserClaimsPrincipal.Identity != null && UserClaimsPrincipal.Identity.IsAuthenticated)
            {
                var claim = UserClaimsPrincipal.FindFirst("MaNguoiDung");
                if (claim != null)
                {
                    int maNguoiDung = int.Parse(claim.Value);

                    soLuong = await _context.OrderDetails
                        .Where(ct => ct.Order.UserID == maNguoiDung
                                  && ct.Order.OrderStatus == "Cart")
                        .SumAsync(ct => (int?)ct.Quantity) ?? 0;
                }
            }

            return View(soLuong);
        }
    }
}