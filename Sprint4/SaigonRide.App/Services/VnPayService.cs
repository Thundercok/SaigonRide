using System.Net;
using System.Security.Cryptography;
using System.Text;
using SaigonRide.App.Helpers; // We'll create a helper for the hashing logic

namespace SaigonRide.App.Services
{
    public class VNPayService
    {
        private readonly IConfiguration _config;

        public VNPayService(IConfiguration config)
        {
            _config = config;
        }

        public string CreatePaymentUrl(HttpContext context, decimal amount, string orderInfo, string orderId)
        {
            var vnpay = new VnPayLibrary(); // We will add this library class next

            vnpay.AddRequestData("vnp_Version", _config["Vnpay:Version"]!);
            vnpay.AddRequestData("vnp_Command", _config["Vnpay:Command"]!);
            vnpay.AddRequestData("vnp_TmnCode", _config["Vnpay:TmnCode"]!);
            vnpay.AddRequestData("vnp_Amount", ((long)amount * 100).ToString()); // VNPay uses cents/xu
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", _config["Vnpay:CurrCode"]!);
            vnpay.AddRequestData("vnp_IpAddr", context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnpay.AddRequestData("vnp_Locale", _config["Vnpay:Locale"]!);
            vnpay.AddRequestData("vnp_OrderInfo", orderInfo);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", _config["Vnpay:ReturnUrl"]!);
            vnpay.AddRequestData("vnp_TxnRef", orderId);

            string paymentUrl = vnpay.CreateRequestUrl(_config["Vnpay:BaseUrl"]!, _config["Vnpay:HashSecret"]!);
            return paymentUrl;
        }
    }
}