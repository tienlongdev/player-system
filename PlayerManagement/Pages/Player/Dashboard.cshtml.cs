using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlayerManagement.Models;

namespace PlayerManagement.Pages.Player
{
    public class DashboardModel : PageModel
    {
        private readonly EntityContext _context;

        public DashboardModel(EntityContext context)
        {
            _context = context;
        }

        public Models.Player PlayerInfo { get; set; }
        public List<PlayerTransactionViewModel> Transactions { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalVipValue { get; set; }
        public decimal TotalSppValue { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var playerId = HttpContext.Session.GetString("PlayerId");
            if (string.IsNullOrEmpty(playerId))
            {
                return RedirectToPage("/Login");
            }

            var playerGuid = Guid.Parse(playerId);

            PlayerInfo = await _context.Players
                .FirstOrDefaultAsync(p => p.PlayerId == playerGuid);

            // Query với running total SPP
            var transactionData = await _context.VVipcampaignTransactions
                .Where(t => t.PlayerId == playerGuid)
                .Join(_context.Players,
                    t => t.PlayerId,
                    p => p.PlayerId,
                    (t, p) => new
                    {
                        t.PlayerId,
                        t.PlayerDescription,
                        p.CardNumber,
                        t.PaymentModalityDescription,
                        t.Date,
                        t.AccountValue,
                        t.Sppvalue,
                        t.Vipvalue,
                        t.Value,
                        t.TransactionId,
                        t.ReferenceDescription,
                        t.ViplevelDescription,
                        t.ViplevelColor,
                        t.Username
                    })
                .OrderByDescending(t => t.Date)
                .Take(100)
                .ToListAsync();

            // Calculate running total SPP in memory (since SQL window functions not fully supported in EF LINQ)
            decimal runningSpp = 0;
            Transactions = transactionData
                .OrderBy(t => t.Date) // Sort ascending for running total calculation
                .Select(t =>
                {
                    runningSpp += t.Sppvalue ?? 0;
                    return new PlayerTransactionViewModel
                    {
                        PlayerId = t.PlayerId,
                        PlayerDescription = t.PlayerDescription,
                        CardNumber = t.CardNumber,
                        PaymentModalityDescription = t.PaymentModalityDescription,
                        Date = t.Date,
                        AccountValue = t.AccountValue,
                        Sppvalue = t.Sppvalue,
                        Vipvalue = t.Vipvalue,
                        Value = t.Value,
                        TotalSpp = runningSpp,
                        ReferenceDescription = t.ReferenceDescription,
                        ViplevelDescription = t.ViplevelDescription,
                        ViplevelColor = t.ViplevelColor,
                        Username = t.Username
                    };
                })
                .OrderByDescending(t => t.Date) // Sort back to descending for display
                .ToList();

            TotalValue = Transactions.Sum(t => t.Value ?? 0);
            TotalVipValue = Transactions.Sum(t => t.Vipvalue ?? 0);
            TotalSppValue = Transactions.LastOrDefault()?.TotalSpp ?? 0; // Latest running total

            return Page();
        }
    }

    public class PlayerTransactionViewModel
    {
        public Guid PlayerId { get; set; }
        public string PlayerDescription { get; set; }
        public string CardNumber { get; set; }
        public string PaymentModalityDescription { get; set; }
        public DateTime Date { get; set; }
        public decimal? AccountValue { get; set; }
        public decimal? Sppvalue { get; set; }
        public decimal? Vipvalue { get; set; }
        public decimal? Value { get; set; }
        public decimal TotalSpp { get; set; }
        public string ReferenceDescription { get; set; }
        public string ViplevelDescription { get; set; }
        public int? ViplevelColor { get; set; }
        public string Username { get; set; }
    }
}