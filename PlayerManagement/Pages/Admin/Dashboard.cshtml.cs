using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PlayerManagement.Helpers;
using PlayerManagement.Models;

namespace PlayerManagement.Pages.Admin
{
    public class DashboardModel : PageModel
    {
        private readonly EntityContext _context;

        public DashboardModel(EntityContext context)
        {
            _context = context;
        }

        public List<PlayerLoginViewModel> PlayerLogins { get; set; }
        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }

        // Pagination properties
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;

        // NEW: Sync statistics
        public int PlayersWithoutAccount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; }

        // NEW: Date range filter for SPP Value
        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public async Task<IActionResult> OnGetAsync(int? pageNumber, int? pageSize)
        {
            if (HttpContext.Session.GetString("IsAdmin") != "true")
            {
                return RedirectToPage("/Login");
            }

            CurrentPage = pageNumber ?? 1;
            PageSize = pageSize ?? 20;

            await LoadData();
            await LoadSyncStatistics();
            return Page();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(Guid playerLoginId, int? pageNumber)
        {
            var playerLogin = await _context.PlayerLogins
                .Include(pl => pl.Player)
                .FirstOrDefaultAsync(pl => pl.PlayerLoginId == playerLoginId);

            if (playerLogin != null)
            {
                playerLogin.PasswordHash = SecurityHelper.GetMD5Hash(playerLogin.Username);
                playerLogin.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                SuccessMessage = $"Đã reset password cho {playerLogin.Username}";
            }

            CurrentPage = pageNumber ?? 1;
            await LoadData();
            await LoadSyncStatistics();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(Guid playerLoginId, int? pageNumber)
        {
            var playerLogin = await _context.PlayerLogins
                .FirstOrDefaultAsync(pl => pl.PlayerLoginId == playerLoginId);

            if (playerLogin != null)
            {
                playerLogin.IsActive = !playerLogin.IsActive;
                playerLogin.UpdatedDate = DateTime.Now;
                await _context.SaveChangesAsync();
                SuccessMessage = $"Đã {(playerLogin.IsActive ? "kích hoạt" : "vô hiệu hóa")} tài khoản";
            }

            CurrentPage = pageNumber ?? 1;
            await LoadData();
            await LoadSyncStatistics();
            return Page();
        }

        // NEW: Sync accounts handler
        public async Task<IActionResult> OnPostSyncAsync()
        {
            if (HttpContext.Session.GetString("IsAdmin") != "true")
            {
                return RedirectToPage("/Login");
            }

            try
            {
                // Get all players without login accounts
                var playersWithoutLogin = await _context.Players
                    .Where(p => !_context.PlayerLogins.Any(pl => pl.PlayerId == p.PlayerId))
                    .ToListAsync();

                if (!playersWithoutLogin.Any())
                {
                    ErrorMessage = "Không có Player nào cần đồng bộ. Tất cả đã có tài khoản!";
                    await LoadData();
                    await LoadSyncStatistics();
                    return Page();
                }

                int createdCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                foreach (var player in playersWithoutLogin)
                {
                    try
                    {
                        // Skip if CardNumber is null or empty
                        if (string.IsNullOrWhiteSpace(player.CardNumber))
                        {
                            errors.Add($"Player {player.FirstName} {player.LastName} không có Card Number");
                            errorCount++;
                            continue;
                        }

                        // Check if username (CardNumber) already exists
                        var existingUsername = await _context.PlayerLogins
                            .FirstOrDefaultAsync(pl => pl.Username == player.CardNumber);

                        if (existingUsername != null)
                        {
                            errors.Add($"Username '{player.CardNumber}' đã tồn tại");
                            errorCount++;
                            continue;
                        }

                        // Create PlayerLogin
                        var playerLogin = new PlayerLogin
                        {
                            PlayerLoginId = Guid.NewGuid(),
                            PlayerId = player.PlayerId,
                            Username = player.CardNumber,
                            PasswordHash = SecurityHelper.GetMD5Hash(player.CardNumber), // Password = CardNumber
                            IsAdmin = false,
                            IsActive = true,
                            CreatedDate = DateTime.Now,
                            UpdatedDate = DateTime.Now
                        };

                        _context.PlayerLogins.Add(playerLogin);
                        createdCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Lỗi với Player {player.FirstName} {player.LastName}: {ex.Message}");
                        errorCount++;
                    }
                }

                // Save all changes
                await _context.SaveChangesAsync();

                // Build success message
                if (createdCount > 0)
                {
                    SuccessMessage = $"🔄 Đồng bộ thành công {createdCount} tài khoản! " +
                                   $"(Username & Password = Card Number)";
                }

                if (errorCount > 0)
                {
                    ErrorMessage = $"⚠ Có {errorCount} lỗi khi đồng bộ. " +
                                 $"Chi tiết: {string.Join("; ", errors.Take(2))}";
                    if (errors.Count > 2)
                    {
                        ErrorMessage += $" và {errors.Count - 2} lỗi khác...";
                    }
                }

                await LoadData();
                await LoadSyncStatistics();
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi đồng bộ: {ex.Message}";
                await LoadData();
                await LoadSyncStatistics();
                return Page();
            }
        }

        // NEW: Export to Excel handler
        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            if (HttpContext.Session.GetString("IsAdmin") != "true")
            {
                return RedirectToPage("/Login");
            }

            try
            {
                // Set EPPlus license context

                var query = _context.PlayerLogins
                    .Include(pl => pl.Player)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    query = query.Where(pl =>
                        pl.Username.Contains(SearchTerm) ||
                        pl.Player.FirstName.Contains(SearchTerm) ||
                        pl.Player.LastName.Contains(SearchTerm) ||
                        pl.Player.CardNumber.Contains(SearchTerm));
                }

                // Get all data with date filter applied to SPP calculation
                var playerData = await query
                    .OrderByDescending(pl => pl.CreatedDate)
                    .Select(pl => new
                    {
                        pl.PlayerLoginId,
                        pl.Username,
                        PlayerName = pl.Player.FirstName + " " + pl.Player.LastName,
                        pl.Player.CardNumber,
                        pl.IsAdmin,
                        pl.IsActive,
                        pl.LastLoginDate,
                        pl.CreatedDate,
                        PlayerId = pl.PlayerId
                    })
                    .ToListAsync();

                // Calculate SPP with date filter
                var playerLogins = new List<PlayerLoginViewModel>();
                foreach (var player in playerData)
                {
                    var sppQuery = _context.VVipcampaignTransactions
                        .Where(t => t.PlayerId == player.PlayerId);

                    // Apply date range filter
                    if (FromDate.HasValue)
                    {
                        sppQuery = sppQuery.Where(t => t.Date >= FromDate.Value);
                    }
                    if (ToDate.HasValue)
                    {
                        var toDateEnd = ToDate.Value.Date.AddDays(1).AddTicks(-1);
                        sppQuery = sppQuery.Where(t => t.Date <= toDateEnd);
                    }

                    var totalSpp = await sppQuery.SumAsync(t => t.Sppvalue) ?? 0;

                    playerLogins.Add(new PlayerLoginViewModel
                    {
                        PlayerLoginId = player.PlayerLoginId,
                        Username = player.Username,
                        PlayerName = player.PlayerName,
                        CardNumber = player.CardNumber,
                        IsAdmin = player.IsAdmin,
                        IsActive = player.IsActive,
                        LastLoginDate = player.LastLoginDate,
                        CreatedDate = player.CreatedDate,
                        TotalSppValue = totalSpp
                    });
                }

                // Create Excel package
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Player Dashboard");

                // Add header with filter info
                worksheet.Cells["A1"].Value = "BÁO CÁO QUẢN LÝ TÀI KHOẢN PLAYER";
                worksheet.Cells["A1:I1"].Merge = true;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Add filter information
                var row = 2;
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    worksheet.Cells[$"A{row}"].Value = $"Từ khóa tìm kiếm: {SearchTerm}";
                    worksheet.Cells[$"A{row}:I{row}"].Merge = true;
                    row++;
                }
                if (FromDate.HasValue)
                {
                    worksheet.Cells[$"A{row}"].Value = $"Từ ngày: {FromDate.Value:dd/MM/yyyy}";
                    worksheet.Cells[$"A{row}:I{row}"].Merge = true;
                    row++;
                }
                if (ToDate.HasValue)
                {
                    worksheet.Cells[$"A{row}"].Value = $"Đến ngày: {ToDate.Value:dd/MM/yyyy}";
                    worksheet.Cells[$"A{row}:I{row}"].Merge = true;
                    row++;
                }
                worksheet.Cells[$"A{row}"].Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Cells[$"A{row}:I{row}"].Merge = true;
                row++;

                // Add column headers
                row++;
                worksheet.Cells[$"A{row}"].Value = "Username";
                worksheet.Cells[$"B{row}"].Value = "Tên Player";
                worksheet.Cells[$"C{row}"].Value = "Card Number";
                worksheet.Cells[$"D{row}"].Value = "Tổng SPP";
                worksheet.Cells[$"E{row}"].Value = "Loại";
                worksheet.Cells[$"F{row}"].Value = "Trạng thái";
                worksheet.Cells[$"G{row}"].Value = "Đăng nhập cuối";
                worksheet.Cells[$"H{row}"].Value = "Ngày tạo";

                // Style header row
                using (var range = worksheet.Cells[$"A{row}:H{row}"])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(102, 126, 234));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Add data
                row++;
                var startDataRow = row;
                foreach (var player in playerLogins)
                {
                    worksheet.Cells[$"A{row}"].Value = player.Username;
                    worksheet.Cells[$"B{row}"].Value = player.PlayerName;
                    worksheet.Cells[$"C{row}"].Value = player.CardNumber;
                    worksheet.Cells[$"D{row}"].Value = player.TotalSppValue;
                    worksheet.Cells[$"D{row}"].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[$"E{row}"].Value = player.IsAdmin ? "Admin" : "Player";
                    worksheet.Cells[$"F{row}"].Value = player.IsActive ? "Active" : "Inactive";
                    worksheet.Cells[$"G{row}"].Value = player.LastLoginDate?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa đăng nhập";
                    worksheet.Cells[$"H{row}"].Value = player.CreatedDate.ToString("dd/MM/yyyy");
                    row++;
                }

                // Add total row
                worksheet.Cells[$"A{row}"].Value = "TỔNG CỘNG";
                worksheet.Cells[$"A{row}:C{row}"].Merge = true;
                worksheet.Cells[$"A{row}:C{row}"].Style.Font.Bold = true;
                worksheet.Cells[$"A{row}:C{row}"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                worksheet.Cells[$"D{row}"].Formula = $"SUM(D{startDataRow}:D{row - 1})";
                worksheet.Cells[$"D{row}"].Style.Font.Bold = true;
                worksheet.Cells[$"D{row}"].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[$"D{row}"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[$"D{row}"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);

                // Auto-fit columns
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Add borders to data
                using (var range = worksheet.Cells[$"A{startDataRow - 1}:H{row}"])
                {
                    range.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                }

                // Generate file
                var fileName = $"PlayerDashboard_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var fileBytes = package.GetAsByteArray();

                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi khi xuất Excel: {ex.Message}";
                await LoadData();
                await LoadSyncStatistics();
                return Page();
            }
        }

        private async Task LoadData()
        {
            var query = _context.PlayerLogins
                .Include(pl => pl.Player)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                query = query.Where(pl =>
                    pl.Username.Contains(SearchTerm) ||
                    pl.Player.FirstName.Contains(SearchTerm) ||
                    pl.Player.LastName.Contains(SearchTerm) ||
                    pl.Player.CardNumber.Contains(SearchTerm));
            }

            // Get total count
            TotalRecords = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

            // Ensure CurrentPage is valid
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            // Get paginated data
            var playerData = await query
                .OrderByDescending(pl => pl.CreatedDate)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .Select(pl => new
                {
                    pl.PlayerLoginId,
                    pl.Username,
                    PlayerName = pl.Player.FirstName + " " + pl.Player.LastName,
                    pl.Player.CardNumber,
                    pl.IsAdmin,
                    pl.IsActive,
                    pl.LastLoginDate,
                    pl.CreatedDate,
                    PlayerId = pl.PlayerId
                })
                .ToListAsync();

            // Calculate SPP with date filter for each player
            PlayerLogins = new List<PlayerLoginViewModel>();
            foreach (var player in playerData)
            {
                var sppQuery = _context.VVipcampaignTransactions
                    .Where(t => t.PlayerId == player.PlayerId);

                // Apply date range filter
                if (FromDate.HasValue)
                {
                    sppQuery = sppQuery.Where(t => t.Date >= FromDate.Value);
                }
                if (ToDate.HasValue)
                {
                    var toDateEnd = ToDate.Value.Date.AddDays(1).AddTicks(-1);
                    sppQuery = sppQuery.Where(t => t.Date <= toDateEnd);
                }

                var totalSpp = await sppQuery.SumAsync(t => t.Sppvalue) ?? 0;

                PlayerLogins.Add(new PlayerLoginViewModel
                {
                    PlayerLoginId = player.PlayerLoginId,
                    Username = player.Username,
                    PlayerName = player.PlayerName,
                    CardNumber = player.CardNumber,
                    IsAdmin = player.IsAdmin,
                    IsActive = player.IsActive,
                    LastLoginDate = player.LastLoginDate,
                    CreatedDate = player.CreatedDate,
                    TotalSppValue = totalSpp
                });
            }
        }

        // NEW: Load sync statistics
        private async Task LoadSyncStatistics()
        {
            try
            {
                var totalPlayers = await _context.Players.CountAsync();
                var playersWithAccount = await _context.PlayerLogins
                    .Where(pl => pl.IsAdmin == false)
                    .CountAsync();
                PlayersWithoutAccount = totalPlayers - playersWithAccount;
            }
            catch
            {
                PlayersWithoutAccount = 0;
            }
        }
    }

    public class PlayerLoginViewModel
    {
        public Guid PlayerLoginId { get; set; }
        public string Username { get; set; }
        public string PlayerName { get; set; }
        public string CardNumber { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal TotalSppValue { get; set; }
    }
}