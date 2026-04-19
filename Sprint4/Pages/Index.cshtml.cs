using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaigonRide.App.Helpers;

namespace SaigonRide.App.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPostVNPay()
        {
            try
            {
                _logger.LogInformation("Đang khởi tạo request sang VNPay...");

                var vnp_TmnCode = _configuration["VnPay:TmnCode"];
                var vnp_HashSecret = _configuration["VnPay:HashSecret"];
                var vnp_Url = _configuration["VnPay:BaseUrl"];
                var vnp_ReturnUrl = _configuration["VnPay:ReturnUrl"];

                // BẮT LỖI 1: NẾU KHÔNG ĐỌC ĐƯỢC CẤU HÌNH, IN THẲNG RA MÀN HÌNH
                if (string.IsNullOrEmpty(vnp_TmnCode) || string.IsNullOrEmpty(vnp_HashSecret) || string.IsNullOrEmpty(vnp_Url))
                {
                    return Content($"🚨 BÁO ĐỘNG ĐỎ: Không đọc được appsettings.json!\nTmnCode: '{vnp_TmnCode}'\nHashSecret: '{vnp_HashSecret}'\nBaseUrl: '{vnp_Url}'");
                }

                var amount = 100000;
                var orderId = DateTime.Now.Ticks.ToString(); 
                
                var vnpay = new VnPayLibrary();
                vnpay.AddRequestData("vnp_Version", "2.1.0");
                vnpay.AddRequestData("vnp_Command", "pay");
                vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
                vnpay.AddRequestData("vnp_Amount", (amount * 100).ToString()); 
                vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnpay.AddRequestData("vnp_CurrCode", "VND");
                vnpay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress(HttpContext));
                vnpay.AddRequestData("vnp_Locale", "vn");
                vnpay.AddRequestData("vnp_OrderInfo", "Nap tien Mixi88 don hang: " + orderId);
                vnpay.AddRequestData("vnp_OrderType", "other");
                vnpay.AddRequestData("vnp_ReturnUrl", vnp_ReturnUrl);
                vnpay.AddRequestData("vnp_TxnRef", orderId);

                string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
                
                _logger.LogInformation("URL chuyển hướng: " + paymentUrl);
                return Redirect(paymentUrl);
            }
            catch (Exception ex)
            {
                // BẮT LỖI 2: NẾU CODE CRASH Ở HÀM MÃ HÓA, IN THẲNG LỖI RA
                return Content($"💥 CRASH NGẦM RỒI ÔNG ƠI:\nLỗi: {ex.Message}\nChi tiết: {ex.StackTrace}");
            }
        }

        public IActionResult OnPostPayPal()
        {
            _logger.LogInformation("Từ từ, làm xong VNPay đã rồi tính...");
            return RedirectToPage();
        }
    }
}