using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Threading.Tasks;

namespace SaigonRide.UITests
{
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class KioskFlowTests : PageTest
    {
        [Test]
        public async Task Kiosk_ShouldShowNumpad_And_GenerateQrCode()
        {
            // 1. Tell the robot to go to your Kiosk page
            // UPDATE THIS PORT to match your actual running application!
            await Page.GotoAsync("http://localhost:5297/Kiosk");

            // 2. Click the screen to start (assuming you have a welcome screen)
            // We use standard CSS selectors here. If your start button has id="btn-start", use "#btn-start"
            // await Page.ClickAsync("#btn-start");

            // 3. Type the phone number into the numpad input
            // Swap "#phone-input" with the actual ID of your HTML input field
            await Page.FillAsync("#phone-input", "0901234567");
            
            // Click the submit/next button
            // Swap "#btn-submit" with your actual HTML button ID
            await Page.ClickAsync("#btn-submit");

            // 4. THE ASSERTION: Prove the UI reacted correctly
            // We tell the robot to wait until the QR code physically appears on the screen
            // Swap "#qr-code-image" with the ID of your actual Stripe/SePay QR code image
            var qrCode = Page.Locator("#qr-code-image");
            await Expect(qrCode).ToBeVisibleAsync();
        }
    }
}