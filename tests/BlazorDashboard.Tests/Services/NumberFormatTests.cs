using System.Globalization;
using BlazorDashboard.Services;

namespace BlazorDashboard.Tests.Services;

public class NumberFormatTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("158370", "158,370.00")]
    [InlineData("100", "100.00")]
    [InlineData("99.99999", "100.0000")]
    [InlineData("1", "1.0000")]
    [InlineData("0.877321", "0.8773")]
    [InlineData("0.00027", "0.00027")]
    [InlineData("0.000271234", "0.0002712")]
    [InlineData("-0.5", "-0.5")]
    [InlineData("-2.5", "-2.5000")]
    [InlineData("-250", "-250.00")]
    public void Adaptive_ChoosesPrecisionByMagnitude(string input, string expected)
    {
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        Assert.Equal(expected, NumberFormat.Adaptive(value, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Adaptive_UsesProvidedCulture()
    {
        Assert.Equal("1.234,50", NumberFormat.Adaptive(1234.5m, new CultureInfo("de-DE")));
    }

    [Fact]
    public void Adaptive_DefaultsToCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("0,8773", NumberFormat.Adaptive(0.87732m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
