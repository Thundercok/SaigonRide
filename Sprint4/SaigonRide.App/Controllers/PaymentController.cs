using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonRide.App.Data;
using SaigonRide.App.Helpers;
using SaigonRide.App.Models.Entities;

namespace SaigonRide.App.Controllers
{
    // Class hứng dữ liệu từ SePay
    public class SepayWebhookPayload
    {
        public int id { get; set; }
        public string gateway { get; set; } = string.Empty;
        public string transactionDate { get; set; } = string.Empty;
        public string accountNumber { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
        public string transferType { get; set; } = string.Empty;
        public decimal transferAmount { get; set; }
        public string referenceCode { get; set; } = string.Empty;
    }

    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public PaymentController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // --- ENDPOINT MỚI CHO SEPAY ---
        [HttpPost("sepay-return")]
        public async Task<IActionResult> SepayReturn([FromBody] SepayWebhookPayload payload)
        {
            // 1. Xác thực Webhook (Security Check)
            if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                return Unauthorized(new { message = "Missing Authorization header" });
            }

            var expectedKey = "Apikey " + _config["Sepay:WebhookSecret"];
            if (authHeader != expectedKey)
            {
                return Unauthorized(new { message = "Invalid API Key" });
            }

            // 2. Bỏ qua nếu là giao dịch tiền ra
            if (payload.transferType != "in")
            {
                return Ok(new { message = "Ignored outgoing transfer" });
            }

            // 3. Phân tích nội dung chuyển khoản để tìm RentalId
            var content = payload.content.ToUpper();
            var rentalIdString = new string(content.Where(char.IsDigit).ToArray()); 
            
            if (!int.TryParse(rentalIdString, out int rentalId))
            {
                return Ok(new { message = "No valid Rental ID found in content" });
            }

            // 4. Cập nhật Database
            var rental = await _context.Rentals
                .Include(r => r.Vehicle)
                .Include(r => r.Deposit)
                .FirstOrDefaultAsync(r => r.Id == rentalId);

            if (rental == null)
            {
                return NotFound(new { message = "Rental not found" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (rental.Status == RentalStatus.Pending)
                {
                    rental.Status = RentalStatus.Active;
                    rental.StartTime = DateTime.UtcNow;
                    rental.Vehicle.Status = VehicleStatus.Rented;

                    if (rental.Deposit != null)
                    {
                        rental.Deposit.Status = DepositStatus.Held;
                        rental.Deposit.Note = $"Paid via SePay (Ref: {payload.referenceCode})";
                        rental.Deposit.ProcessedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return Ok(new { message = "Payment confirmed via SePay. Rental is active!" });
                }

                return Ok(new { message = "Rental is already processed." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Database error", error = ex.Message });
            }
        }

        // --- ENDPOINT CŨ CỦA VNPAY ---
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnpayReturn()
        {
            if (Request.Query.Count > 0)
            {
                string vnp_HashSecret = _config["Vnpay:HashSecret"]!;
                var vnpayData = Request.Query;
                VnPayLibrary vnpay = new VnPayLibrary();

                foreach (var (key, value) in vnpayData)
                {
                    if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(key, value!);
                    }
                }

                int rentalId = int.Parse(vnpay.GetResponseData("vnp_TxnRef"));
                long vnp_Amount = long.Parse(vnpay.GetResponseData("vnp_Amount")) / 100;
                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
                string vnp_SecureHash = Request.Query["vnp_SecureHash"]!;

                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                    {
                        var rental = await _context.Rentals
                            .Include(r => r.Vehicle)
                            .Include(r => r.Deposit)
                            .FirstOrDefaultAsync(r => r.Id == rentalId);

                        if (rental != null && rental.Status == RentalStatus.Pending)
                        {
                            using var transaction = await _context.Database.BeginTransactionAsync();
                            try
                            {
                                rental.Status = RentalStatus.Active;
                                rental.StartTime = DateTime.UtcNow; 
                                rental.Vehicle.Status = VehicleStatus.Rented;
                                
                                if (rental.Deposit != null)
                                {
                                    rental.Deposit.Status = DepositStatus.Held;
                                    rental.Deposit.ProcessedAt = DateTime.UtcNow;
                                }

                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();

                                return Ok(new { Message = "Payment successful. Your rental is now active!", RentalId = rentalId });
                            }
                            catch (Exception)
                            {
                                await transaction.RollbackAsync();
                                return StatusCode(500, new { Message = "Internal server error updating rental status." });
                            }
                        }
                        return BadRequest(new { Message = "Rental not found or already processed." });
                    }
                    else
                    {
                        return BadRequest(new { Message = $"Payment failed with code: {vnp_ResponseCode}" });
                    }
                }
                else
                {
                    return BadRequest(new { Message = "Invalid signature. Security warning!" });
                }
            }

            return BadRequest(new { Message = "No data received from VNPay." });
        }
    }
}