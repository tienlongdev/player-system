using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlayerManagement.Helpers;
using PlayerManagement.Models;

namespace PlayerManagement.Pages
{
    public class LoginModel : PageModel
    {
        private readonly EntityContext _context;

        public LoginModel(EntityContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string Username { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // Nếu đã đăng nhập, redirect
            if (HttpContext.Session.GetString("UserId") != null)
            {
                var isAdmin = HttpContext.Session.GetString("IsAdmin") == "true";
                return RedirectToPage(isAdmin ? "/Admin/Dashboard" : "/Player/Dashboard");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập username và password";
                return Page();
            }

            var passwordHash = SecurityHelper.GetMD5Hash(Password);
            
            var playerLogin = await _context.PlayerLogins
                .Include(pl => pl.Player)
                .FirstOrDefaultAsync(pl => 
                    pl.Username == Username && 
                    pl.PasswordHash == passwordHash && 
                    pl.IsActive);

            if (playerLogin == null)
            {
                ErrorMessage = "Username hoặc password không đúng";
                return Page();
            }

            // Cập nhật last login
            playerLogin.LastLoginDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // Lưu session
            HttpContext.Session.SetString("UserId", playerLogin.PlayerLoginId.ToString());
            HttpContext.Session.SetString("PlayerId", playerLogin.PlayerId.ToString());
            HttpContext.Session.SetString("Username", playerLogin.Username);
            HttpContext.Session.SetString("IsAdmin", playerLogin.IsAdmin.ToString().ToLower());
            HttpContext.Session.SetString("PlayerName", $"{playerLogin.Player.FirstName} {playerLogin.Player.LastName}");

            // Redirect dựa trên role
            if (playerLogin.IsAdmin)
            {
                return RedirectToPage("/Admin/Dashboard");
            }
            else
            {
                return RedirectToPage("/Player/Dashboard");
            }
        }
    }
}