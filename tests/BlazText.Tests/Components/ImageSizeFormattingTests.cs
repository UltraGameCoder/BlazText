using System.Globalization;
using BlazText.Plugins;

namespace BlazText.Tests.Components;

public class ImageSizeFormattingTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(999, "999 bytes")]
    [InlineData(1000, "1 KB")]
    [InlineData(512_000, "512 KB")]
    [InlineData(999_999, "999.9 KB")]
    // Rounding afterwards used to report this as "1024 KB".
    [InlineData(1_048_575, "1 MB")]
    [InlineData(1_000_000, "1 MB")]
    // Must not read "1.5 MB": a 1.5 MB file is rejected by this limit, so rounding up would
    // advertise a limit more permissive than the one enforced.
    [InlineData(1_549_000, "1.5 MB")]
    [InlineData(2_000_000, "2 MB")]
    [InlineData(5_000_000, "5 MB")]
    [InlineData(1_000_000_000, "1 GB")]
    [InlineData(long.MaxValue, "9223372036.8 GB")]
    // A negative limit rejects everything; it must at least not crash or read as a huge number.
    [InlineData(-1, "-1 bytes")]
    public void Size_is_formatted_in_the_largest_readable_unit(long bytes, string expected)
    {
        using var culture = new InvariantCultureScope();

        Assert.Equal(expected, ImagePlugin.FormatSize(bytes));
    }

    [Fact]
    public void Size_uses_the_current_culture_for_the_decimal_separator()
    {
        // The message is user-facing text, so current culture is correct — pinned so nobody
        // "fixes" it to invariant, and so the theory above is understood as culture-scoped.
        using var culture = new CultureScope(CultureInfo.GetCultureInfo("nl-NL"));

        Assert.Equal("1,5 MB", ImagePlugin.FormatSize(1_549_000));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _original = CultureInfo.CurrentCulture;

        public CultureScope(CultureInfo culture) => CultureInfo.CurrentCulture = culture;

        public void Dispose() => CultureInfo.CurrentCulture = _original;
    }

    private sealed class InvariantCultureScope : IDisposable
    {
        private readonly CultureScope _scope = new(CultureInfo.InvariantCulture);

        public void Dispose() => _scope.Dispose();
    }
}
