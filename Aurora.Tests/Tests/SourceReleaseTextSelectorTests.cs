using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class SourceReleaseTextSelectorTests
{
    [Fact]
    public void SelectLatest_OrdersHumanReadableDatesChronologically()
    {
        string? latest = SourceReleaseTextSelector.SelectLatest(
            ["September 2023", "January 2024"]);

        latest.Should().Be("January 2024");
    }

    [Fact]
    public void SelectLatest_OrdersIsoDatesChronologically()
    {
        string? latest = SourceReleaseTextSelector.SelectLatest(
            ["2023-12-01", "2024-01-15", "2022-06-01"]);

        latest.Should().Be("2024-01-15");
    }

    [Fact]
    public void SelectLatest_IgnoresBlankValues()
    {
        string? latest = SourceReleaseTextSelector.SelectLatest(
            [null, " ", "March 2024"]);

        latest.Should().Be("March 2024");
    }
}
