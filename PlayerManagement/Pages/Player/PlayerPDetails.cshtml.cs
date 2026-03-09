using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlayerManagement.Models;

namespace PlayerManagement.Pages.Player
{
    public class PlayerPDetailsModel : PageModel
    {
        private readonly EntityContext _context;

        public PlayerPDetailsModel(EntityContext context)
        {
            _context = context;
        }

        public Models.Player PlayerInfo { get; set; }
        public List<PlayerPTransactionViewModel> Transactions { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            // Kiểm tra đăng nhập
            var playerId = HttpContext.Session.GetString("PlayerId");
            if (string.IsNullOrEmpty(playerId))
            {
                return RedirectToPage("/Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var currentPlayerGuid = Guid.Parse(playerId);

            // Lấy thông tin current player để verify quyền truy cập
            var currentPlayer = await _context.Players
                .FirstOrDefaultAsync(p => p.PlayerId == currentPlayerGuid);

            if (currentPlayer == null || currentPlayer.NickName != "A")
            {
                return Forbid(); // Chỉ cho phép player có NickName = 'A' truy cập
            }

            // Lấy thông tin Player P
            PlayerInfo = await _context.Players
                .FirstOrDefaultAsync(p => p.PlayerId == id && p.NickName == "P");

            if (PlayerInfo == null)
            {
                return NotFound();
            }

            // Verify Player P này thuộc quyền quản lý của Player A hiện tại
            if (!string.IsNullOrEmpty(currentPlayer.CardNumber) &&
                !string.IsNullOrEmpty(PlayerInfo.CardNumber))
            {
                var prefix = currentPlayer.CardNumber.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (!PlayerInfo.CardNumber.StartsWith(prefix))
                {
                    return Forbid(); // Player P này không thuộc quyền quản lý
                }
            }

            // Convert SQL query sang LINQ
            Transactions = await (
                from p in _context.Players
                join pa in _context.VPlayerAccounts on p.PlayerId equals pa.PlayerId
                join pt in _context.VPlayerTransactions on pa.PlayerId equals pt.PlayerId
                join tt in _context.Transactions on pt.TransactionId equals tt.TransactionId
                where pa.PlayerId == id
                orderby tt.Date descending
                select new PlayerPTransactionViewModel
                {
                    PlayerId = p.PlayerId,
                    NickName = p.NickName,
                    AccountTypeDescription = pa.AccountTypeDescription,
                    Date = tt.Date,
                    Value = pt.Value,
                    Balance = pa.Balance,
                    TransactionId = tt.TransactionId
                }
            ).ToListAsync();

            return Page();
        }
    }

    public class PlayerPTransactionViewModel
    {
        public Guid PlayerId { get; set; }
        public string NickName { get; set; }
        public string AccountTypeDescription { get; set; }
        public DateTime Date { get; set; }
        public decimal? Value { get; set; }
        public decimal? Balance { get; set; }
        public Guid TransactionId { get; set; }
    }
}