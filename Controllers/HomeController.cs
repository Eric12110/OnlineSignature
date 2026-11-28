using Microsoft.AspNetCore.Mvc;
using OnlineSignature.Data;
using OnlineSignature.Models;

namespace OnlineSignature.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Save(string name, string signature)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest("姓名及簽名不可空白");
        }

        const string prefix = "data:image/png;base64,";

        if (!signature.StartsWith(prefix))
        {
            return BadRequest("簽名圖片格式錯誤");
        }

        try
        {
            byte[] imageBytes = Convert.FromBase64String(
                signature.Substring(prefix.Length));

            // 限制簽名圖片大小為 1 MB
            if (imageBytes.Length > 1024 * 1024)
                return BadRequest("簽名圖片太大");

            // 確認 PNG 檔案格式
            byte[] pngHeader = { 137, 80, 78, 71, 13, 10, 26, 10 };

            if (imageBytes.Length < 8 ||
                !imageBytes.Take(8).SequenceEqual(pngHeader))
                return BadRequest("圖片格式不正確");

            var data = new Signature
            {
                Name = name.Trim(),
                SignatureImage = imageBytes,
                CreatedAt = DateTime.UtcNow
            };

            _db.Signatures.Add(data);
            _db.SaveChanges();

            return Json(new
            {
                success = true,
                message = "簽名已成功儲存！"
            });
        }
        catch (FormatException)
        {
            return BadRequest("簽名資料格式錯誤");
        }
    }
}