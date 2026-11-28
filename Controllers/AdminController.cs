
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineSignature.Data;

namespace OnlineSignature.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public AdminController(
        AppDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    // 管理者登入頁面
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true &&
            User.IsInRole("Admin"))
        {
            return RedirectToAction(nameof(Index));
        }

        return View();
    }

    // 驗證管理者帳號密碼
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string username,
        string password)
    {
        var adminUsername =
            _configuration["Admin:Username"];

        var adminPassword =
            _configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(adminUsername) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            return StatusCode(500, "尚未設定管理者帳號密碼");
        }

        bool usernameValid = FixedTimeEquals(
            username ?? "",
            adminUsername);

        bool passwordValid = FixedTimeEquals(
            password ?? "",
            adminPassword);

        if (!usernameValid || !passwordValid)
        {
            ViewBag.Error = "帳號或密碼錯誤";
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, adminUsername),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        return RedirectToAction(nameof(Index));
    }

    // 管理者簽名管理頁面
    [Authorize(Roles = "Admin")]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(
        string? name,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var query = _db.Signatures.AsNoTracking().AsQueryable();

        // 依姓名查詢
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(x => x.Name.Contains(name.Trim()));
        }

        // 台灣時區
        var taiwanTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

        // 起始日期：台灣當天 00:00 轉成 UTC
        if (startDate.HasValue)
        {
            var startTaiwan = DateTime.SpecifyKind(
                startDate.Value.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                startTaiwan, taiwanTimeZone);

            query = query.Where(x => x.CreatedAt >= startUtc);
        }

        // 結束日期：採用隔天 00:00 作為排除界線
        if (endDate.HasValue)
        {
            var endTaiwan = DateTime.SpecifyKind(
                endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified);

            var endUtc = TimeZoneInfo.ConvertTimeToUtc(
                endTaiwan, taiwanTimeZone);

            query = query.Where(x => x.CreatedAt < endUtc);
        }

        var signatures = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        // 保留查詢條件，讓畫面可以顯示
        ViewBag.SearchName = name;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

        return View(signatures);
    }

    // 查看簽名圖片
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Image(int id)
    {
        var signature = await _db.Signatures
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (signature == null)
            return NotFound();

        return File(signature.SignatureImage, "image/png");
    }

    // 下載簽名圖片
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var signature = await _db.Signatures
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (signature == null)
            return NotFound();

        return File(
            signature.SignatureImage,
            "image/png",
            $"signature-{signature.Id}.png");
    }

    // 管理者登出
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    // 固定時間比較，避免直接使用一般字串比較
    private static bool FixedTimeEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var signature = await _db.Signatures
            .FirstOrDefaultAsync(x => x.Id == id);

        if (signature == null)
        {
            return NotFound();
        }

        _db.Signatures.Remove(signature);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
