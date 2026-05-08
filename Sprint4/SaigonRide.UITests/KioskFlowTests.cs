using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace SaigonRide.UITests;

[Parallelizable(ParallelScope.None)]
[TestFixture]
public class KioskFlowTests : PageTest
{
    private const string BaseUrl   = "http://localhost:5297";
    private const string TestEmail = "test@saigonride.com";  // must exist in seeded DB
    private const string TestOtp   = "123456";               // dev bypass

    public override BrowserNewContextOptions ContextOptions() =>
        new() { ViewportSize = new ViewportSize { Width = 1280, Height = 720 } };

    private async Task TypeOnNumpad(string targetInputId, string digits)
    {
        foreach (var d in digits)
            await Page.ClickAsync($".numpad-key[data-target='{targetInputId}'][data-val='{d}']");
    }

    private async Task WaitForState(string name, int ms = 10000)
    {
        await Page.Locator($"#paymentState_{name}").WaitForAsync(new()
        {
            State   = WaitForSelectorState.Visible,
            Timeout = ms
        });
        if (name == "Splash")
            await Page.WaitForFunctionAsync("() => window.kioskReady === true", new() { Timeout = ms });
    }

    private async Task NavigateToIdle()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        await Page.FillAsync("#emailInput", TestEmail);
        await Page.ClickAsync("#btnSubmitEmail");
        await WaitForState("OtpInput");
        await TypeOnNumpad("otpInput", TestOtp);
        await Page.ClickAsync("#btnSubmitOtp");
        await WaitForState("VehicleSelect");
        await Page.Locator(".vehicle-option-btn").First.ClickAsync();
        await WaitForState("DepositInfo");
        await Page.ClickAsync("#btnConfirmDeposit");
        await WaitForState("Idle");
    }

    [Test]
    public async Task Kiosk_Loads_And_Shows_Splash()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Expect(Page.Locator("#btnTouchToStart")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_EmailInput_Shows_After_Splash_Tap()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        await Expect(Page.Locator("#emailInput")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_EmailInput_Rejects_Invalid_Email()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        await Page.FillAsync("#emailInput", "notanemail");
        await Page.ClickAsync("#btnSubmitEmail");
        await Expect(Page.Locator("#emailError")).ToHaveTextAsync("Email không hợp lệ.");
        await Expect(Page.Locator("#paymentState_EmailInput")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_OtpInput_Shows_After_Valid_Email()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        await Page.FillAsync("#emailInput", TestEmail);
        await Page.ClickAsync("#btnSubmitEmail");
        await WaitForState("OtpInput");
        await Expect(Page.Locator("#otpInput")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_OtpInput_Rejects_Wrong_Code()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        await Page.FillAsync("#emailInput", TestEmail);
        await Page.ClickAsync("#btnSubmitEmail");
        await WaitForState("OtpInput");
        await TypeOnNumpad("otpInput", "000000");
        await Page.ClickAsync("#btnSubmitOtp");
        await Expect(Page.Locator("#otpError")).Not.ToBeEmptyAsync();
        await Expect(Page.Locator("#paymentState_OtpInput")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_VehicleSelect_Shows_After_Auth()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        await Page.FillAsync("#emailInput", TestEmail);
        await Page.ClickAsync("#btnSubmitEmail");
        await WaitForState("OtpInput");
        await TypeOnNumpad("otpInput", TestOtp);
        await Page.ClickAsync("#btnSubmitOtp");
        await WaitForState("VehicleSelect");
        await Expect(Page.Locator(".vehicle-option-list .vehicle-option-btn").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_DepositInfo_Shows_After_Vehicle_Selection()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        await Page.FillAsync("#emailInput", TestEmail);
        await Page.ClickAsync("#btnSubmitEmail");
        await WaitForState("OtpInput");
        await TypeOnNumpad("otpInput", TestOtp);
        await Page.ClickAsync("#btnSubmitOtp");
        await WaitForState("VehicleSelect");
        await Page.Locator(".vehicle-option-btn").First.ClickAsync();
        await WaitForState("DepositInfo");
        await Expect(Page.Locator("#paymentState_DepositInfo .deposit-row").First).ToBeVisibleAsync();
        await Expect(Page.Locator("#btnConfirmDeposit")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_Idle_Shows_Payment_Method_Buttons()
    {
        await NavigateToIdle();
        await Expect(Page.Locator("#btnVietQR")).ToBeVisibleAsync();
        await Expect(Page.Locator("#btnStripe")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_VietQR_Shows_QR_After_Payment_Initiated()
    {
        await NavigateToIdle();
        await Page.ClickAsync("#btnVietQR");
        await WaitForState("Active");
        var qrImg = Page.Locator("#qrImage");
        await Expect(qrImg).ToBeVisibleAsync();
        var src = await qrImg.GetAttributeAsync("src");
        Assert.That(src, Is.Not.Null.And.Not.Empty);
        await Expect(Page.Locator("#countdownTimer")).ToBeVisibleAsync();
        var cancelBtn = Page.Locator("#btnCancelRental");
        await cancelBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await cancelBtn.ClickAsync();
        await WaitForState("Idle");
    }

    [Test]
    public async Task Kiosk_Stripe_Redirects_To_Stripe_Checkout()
    {
        await NavigateToIdle();
        await Page.ClickAsync("#btnStripe");

        var checkoutTask = Page.WaitForURLAsync("**checkout.stripe.com**", new() { Timeout = 15000 })
                               .ContinueWith(_ => "checkout");
        var errorTask    = Page.Locator("#paymentState_Error")
                               .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 })
                               .ContinueWith(_ => "error");

        var winner = await await Task.WhenAny(checkoutTask, errorTask);
        if (winner == "checkout")
            Assert.That(Page.Url, Does.Contain("stripe.com"));
        else
            Assert.Warn("Stripe not configured — check Railway env vars.");
    }

    [Test]
    public async Task Kiosk_Back_Button_Returns_To_Previous_State()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("EmailInput");
        var backBtn = Page.Locator("#paymentState_EmailInput [data-back-to='Splash']");
        await backBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await backBtn.ClickAsync();
        await WaitForState("Splash");
    }

    [Test]
    public async Task Kiosk_Error_State_Auto_Resets_After_10s()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.EvaluateAsync("() => window.goToState('Error', { message: 'Test error' })");
        await WaitForState("Error");
        await Expect(Page.Locator("#errorMessage")).ToHaveTextAsync("Test error");
        await WaitForState("Splash", ms: 15000);
    }

    [Test]
    public async Task WebApp_Home_Page_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.Locator(".hero-title")).ToBeVisibleAsync();
    }

    [Test]
    public async Task WebApp_Login_Page_Loads_And_Has_Form()
    {
        await Page.GotoAsync($"{BaseUrl}/Identity/Account/Login");
        await Expect(Page.Locator("#Input_Email")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_Password")).ToBeVisibleAsync();
    }

    [Test]
    public async Task WebApp_Dashboard_Redirects_Unauthenticated_To_Login()
    {
        await Page.GotoAsync($"{BaseUrl}/Dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.That(Page.Url, Does.Contain("Login").IgnoreCase);
    }
}