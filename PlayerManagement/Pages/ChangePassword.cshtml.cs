using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlayerManagement.Helpers;
using PlayerManagement.Models;

namespace PlayerManagement.Pages
{
    public class ChangePasswordModel : PageModel
    {
        private readonly EntityContext _context;

        public ChangePasswordModel(EntityContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string CurrentPassword { get; set; }

        [BindProperty]
        public string NewPassword { get; set; }

        [BindProperty]
        public string ConfirmPassword { get; set; }

        public string ErrorMessage { get; set; }
        public string SuccessMessage { get; set; }
        public string Username { get; set; }
        public bool IsAdmin { get; set; }

        public IActionResult OnGet()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Login");
            }

            Username = HttpContext.Session.GetString("Username");
            IsAdmin = HttpContext.Session.GetString("IsAdmin") == "true";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Login");
            }

            Username = HttpContext.Session.GetString("Username");
            IsAdmin = HttpContext.Session.GetString("IsAdmin") == "true";

            if (string.IsNullOrWhiteSpace(CurrentPassword) || 
                string.IsNullOrWhiteSpace(NewPassword) || 
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Vui lòng điền đầy đủ thông tin";
                return Page();
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu mới và xác nhận mật khẩu không khớp";
                return Page();
            }

            if (NewPassword.Length < 6)
            {
                ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự";
                return Page();
            }

            var playerLoginId = Guid.Parse(userId);
            var playerLogin = await _context.PlayerLogins
                .FirstOrDefaultAsync(pl => pl.PlayerLoginId == playerLoginId);

            if (playerLogin == null)
            {
                ErrorMessage = "Không tìm thấy tài khoản";
                return Page();
            }

            // Verify current password
            var currentPasswordHash = SecurityHelper.GetMD5Hash(CurrentPassword);
            if (playerLogin.PasswordHash != currentPasswordHash)
            {
                ErrorMessage = "Mật khẩu hiện tại không đúng";
                return Page();
            }

            // Update password
            playerLogin.PasswordHash = SecurityHelper.GetMD5Hash(NewPassword);
            playerLogin.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            SuccessMessage = "Đổi mật khẩu thành công!";
            
            // Clear form
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;

            return Page();
        }
    }
}