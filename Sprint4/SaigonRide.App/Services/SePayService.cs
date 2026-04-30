using System.Web;
using Microsoft.Extensions.Configuration;

namespace SaigonRide.App.Services
{
    public class SepayService
    {
        private readonly IConfiguration _config;

        public SepayService(IConfiguration config)
        {
            _config = config;
        }

        // Hàm này sinh ra đường link ảnh QR Code
        public string GenerateQrUrl(decimal amount, int rentalId)
        {
            // Các thông tin cố định (bạn có thể đưa BankId và AccountName vào appsettings sau nếu thích)
            string bankId = "bidv"; 
            string accountNumber = _config["Sepay:AccountNumber"] ?? "96247WXZRR";
            string accountName = "BUI LE THUY QUYNH"; 
            string template = "compact2"; // Layout chuẩn, đẹp cho Kiosk
            
            // Nội dung chuyển khoản BẮT BUỘC phải map với Webhook (Ví dụ: "SGR 9")
            string content = $"SGR {rentalId}";

            // Encode dữ liệu để đảm bảo an toàn khi truyền qua URL (tránh lỗi khoảng trắng, dấu tiếng Việt)
            string encodedContent = HttpUtility.UrlEncode(content);
            string encodedName = HttpUtility.UrlEncode(accountName);

            // Ráp thành URL hoàn chỉnh của API VietQR
            string qrUrl = $"https://img.vietqr.io/image/{bankId}-{accountNumber}-{template}.png?amount={(long)amount}&addInfo={encodedContent}&accountName={encodedName}";

            return qrUrl;
        }
    }
}
