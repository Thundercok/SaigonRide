using SaigonRide.App.Services;
using FluentAssertions;

namespace SaigonRide.Tests;

public class PricingEngineTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void HourlyFare_LessThanOneHour_BillsMinimumOneHour()
    {
        var fare = PricingEngine.CalculateHourlyFare(20000m, Base, Base.AddMinutes(30));
        fare.Should().Be(20000m);
    }

    [Fact]
    public void HourlyFare_TwoAndHalfHours_BillsThreeHours()
    {
        var fare = PricingEngine.CalculateHourlyFare(20000m, Base, Base.AddHours(2.5));
        fare.Should().Be(60000m);
    }

    [Fact]
    public void DailyFare_FiveHours_BillsMinimumOneDay()
    {
        var fare = PricingEngine.CalculateDailyFare(150000m, Base, Base.AddHours(5));
        fare.Should().Be(150000m);
    }

    [Fact]
    public void Deposit_GradeA_Is20Percent()
    {
        var deposit = PricingEngine.CalculateDepositAmount(10_000_000m, grade: 2);
        deposit.Should().Be(2_000_000m);
    }

    [Fact]
    public void Deposit_GradeC_Is10Percent()
    {
        var deposit = PricingEngine.CalculateDepositAmount(2_000_000m, grade: 0);
        deposit.Should().Be(200_000m);
    }
}