using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SaigonRide.App.Models.ViewModels;
using SaigonRide.App.Services;
using SaigonRide.App.Settings;
using Stripe;
using Stripe.Checkout;

namespace SaigonRide.App.Controllers;

[ApiController]
[Route("api/wallet")]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly StripeSettings _stripe;
    private readonly IConfiguration _config;

    public WalletController(WalletService walletService, IOptions<StripeSettings> stripe, IConfiguration config)
    {
        _walletService = walletService;
        _stripe = stripe.Value;
        _config = config;
    }

    [HttpGet("balance")]
    [Authorize]
    public async Task<IActionResult> GetBalance(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var rideCard = await _walletService.GetOrCreateRideCardAsync(userId, cancellationToken);
        return Ok(new WalletBalanceResponse { Balance = rideCard.Balance, Currency = rideCard.Currency });
    }

    [HttpPost("topup")]
    [Authorize]
    public async Task<IActionResult> TopUp([FromBody] WalletTopUpRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        try
        {
            var response = await _walletService.CreateTopUpAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        if (Request.Headers.ContainsKey("Stripe-Signature"))
            return await HandleStripeWebhook(cancellationToken);

        if (!VerifySepaySignature())
            return Unauthorized(new { message = "Invalid or missing API key" });

        var payload = await Request.ReadFromJsonAsync<SePayWebhookPayload>(cancellationToken: cancellationToken);
        if (payload == null) return BadRequest(new { message = "Invalid SePay payload" });

        var result = await _walletService.CompleteSePayTopUpAsync(payload, cancellationToken);
        return Ok(result);
    }

    private async Task<IActionResult> HandleStripeWebhook(CancellationToken cancellationToken)
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync(cancellationToken);

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                _stripe.WebhookSecret);

            if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
                return Ok(new WalletWebhookResponse { Success = true, Message = "Ignored Stripe event." });

            var session = (Session)stripeEvent.Data.Object;
            var result = await _walletService.CompleteStripeCheckoutAsync(session, cancellationToken);
            return Ok(result);
        }
        catch (StripeException)
        {
            return BadRequest(new { message = "Invalid Stripe webhook signature" });
        }
    }

    private bool VerifySepaySignature()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return false;

        var expected = "Apikey " + _config["Sepay:WebhookSecret"];
        return authHeader.ToString() == expected;
    }
}
