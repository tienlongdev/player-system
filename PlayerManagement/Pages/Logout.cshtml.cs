using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PlayerManagement.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            return OnPost();
        }

        public IActionResult OnPost()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }
    }
}