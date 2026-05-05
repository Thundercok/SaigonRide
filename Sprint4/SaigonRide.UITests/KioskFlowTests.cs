using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace SaigonRide.UITests;

[Parallelizable(ParallelScope.None)] // CHANGED: Prevents DB race conditions for the single test user[TestFixture]


public class KioskFlowTests : PageTest
{
    
    private const string BaseUrl   = "http://localhost:5297";
    private const string TestPhone = "0901234567";
    private const string TestOtp   = "123456";
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1280,
                Height = 720
            }
        };
    }

    public BrowserTypeLaunchOptions LaunchOptions()
    {
        return new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 250
        };
    }
    private async Task TypeOnNumpad(string targetInputId, string digits)
    {
        foreach (var digit in digits)
            await Page.ClickAsync($".numpad-key[data-target='{targetInputId}'][data-val='{digit}']");
    }

    private async Task WaitForState(string stateName, int timeoutMs = 10000)
    {
        await Page.Locator($"#paymentState_{stateName}").WaitForAsync(new()
        {
            State   = Microsoft.Playwright.WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });

        if (stateName == "Splash")
        {
            await Page.WaitForFunctionAsync("() => window.kioskReady === true", new Microsoft.Playwright.PageWaitForFunctionOptions
            {
                Timeout = timeoutMs
            });
        }
        
    }

    /// Navigates through auth + vehicle select, leaves kiosk in Idle state.
    private async Task NavigateToIdle()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("PhoneInput");
        await TypeOnNumpad("phoneInput", TestPhone);
        await Page.ClickAsync("#btnSubmitPhone");
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
    public async Task Kiosk_PhoneInput_Shows_After_Splash_Tap()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("PhoneInput");
        await Expect(Page.Locator("#phoneInput")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_PhoneInput_Rejects_Invalid_Phone()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("PhoneInput");
        await TypeOnNumpad("phoneInput", "12345");
        await Page.ClickAsync("#btnSubmitPhone");
        await Expect(Page.Locator("#phoneError")).ToHaveTextAsync("Số điện thoại không hợp lệ.");
        await Expect(Page.Locator("#paymentState_PhoneInput")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_OtpInput_Shows_After_Valid_Phone()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("PhoneInput");
        await TypeOnNumpad("phoneInput", TestPhone);
        await Page.ClickAsync("#btnSubmitPhone");
        await WaitForState("OtpInput");
        await Expect(Page.Locator("#otpInput")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_OtpInput_Rejects_Wrong_Code()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("PhoneInput");
        await TypeOnNumpad("phoneInput", TestPhone);
        await Page.ClickAsync("#btnSubmitPhone");
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
        await WaitForState("PhoneInput");
        await TypeOnNumpad("phoneInput", TestPhone);
        await Page.ClickAsync("#btnSubmitPhone");
        await WaitForState("OtpInput");
        await TypeOnNumpad("otpInput", TestOtp);
        await Page.ClickAsync("#btnSubmitOtp");
        await WaitForState("AuthSuccess");
        await WaitForState("VehicleSelect");
        await Expect(Page.Locator(".vehicle-option-list .vehicle-option-btn").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Kiosk_DepositInfo_Shows_After_Vehicle_Selection()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("PhoneInput");
        await TypeOnNumpad("phoneInput", TestPhone);
        await Page.ClickAsync("#btnSubmitPhone");
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
        
        // Ensure the cancel button is fully visible and interactable before tearing down
        var cancelBtn = Page.Locator("#btnCancelRental");
        await cancelBtn.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible });
        await cancelBtn.ClickAsync();
        
        await WaitForState("Idle");
    }
    
    [Test]
    public async Task Kiosk_Stripe_Redirects_To_Stripe_Checkout()
    {
        await NavigateToIdle();

        async Task<string> WaitForCheckoutAsync()
        {
            await Page.WaitForURLAsync("**checkout.stripe.com**", new()
            {
                Timeout = 15000
            });
            return "checkout";
        }

        async Task<string> WaitForErrorAsync()
        {
            await Page.Locator("#paymentState_Error").WaitForAsync(new()
            {
                State = Microsoft.Playwright.WaitForSelectorState.Visible,
                Timeout = 15000
            });
            return "error";
        }

        await Page.ClickAsync("#btnStripe");

        var checkoutTask = WaitForCheckoutAsync();
        var errorTask = WaitForErrorAsync();
        var completedTask = await Task.WhenAny(checkoutTask, errorTask);
        if (await completedTask == "checkout")
        {
            Assert.That(Page.Url, Does.Contain("stripe.com"));
            return;
        }

        Assert.Warn("Stripe not configured — fix Railway env vars (merged key issue).");
    }

    [Test]
    public async Task Kiosk_Back_Button_Returns_To_Previous_State()
    {
        await Page.GotoAsync($"{BaseUrl}/Kiosk");
        await WaitForState("Splash");
        await Page.ClickAsync("#btnTouchToStart");
        await WaitForState("PhoneInput");

        // Explicitly wait for the back button inside the active state to be visible
        var backBtn = Page.Locator("#paymentState_PhoneInput [data-back-to='Splash']");
        await backBtn.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible });
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
        await WaitForState("Splash", timeoutMs: 15000);
    }

    [Test]
    public async Task WebApp_Home_Page_Loads()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await Expect(Page.Locator(".hero-title")).ToBeVisibleAsync();
        await Expect(Page.Locator(".hero-cta")).ToBeVisibleAsync();
    }

    [Test]
    public async Task WebApp_Login_Page_Loads_And_Has_Form()
    {
        await Page.GotoAsync($"{BaseUrl}/Identity/Account/Login");
        await Expect(Page.Locator(".identity-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_Email")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_Password")).ToBeVisibleAsync();
        await Expect(Page.Locator("#login-submit")).ToBeVisibleAsync();
    }

    [Test]
    public async Task WebApp_Register_Page_Loads_And_Has_Form()
    {
        await Page.GotoAsync($"{BaseUrl}/Identity/Account/Register");
        await Expect(Page.Locator(".identity-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_Email")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_Password")).ToBeVisibleAsync();
        await Expect(Page.Locator("#Input_ConfirmPassword")).ToBeVisibleAsync();
    }

    [Test]
    public async Task WebApp_Dashboard_Redirects_Unauthenticated_To_Login()
    {
        await Page.GotoAsync($"{BaseUrl}/Dashboard");
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
        
        // DEBUG: Print the exact URL we landed on to the test console
        Console.WriteLine($"[DEBUG] Landed on URL: {Page.Url}");
        
        Assert.That(Page.Url, Does.Contain("Login").IgnoreCase);
    }
}
