using OneSake.Server.Match;

namespace OneSake.Tests;

public class GuestIdValidatorTests
{
    [Theory]
    [InlineData("3F2504E0-4F89-11D3-9A0C-0305E82C3301", "3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("  3f2504e0-4f89-11d3-9a0c-0305e82c3301 ", "3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public void Normalize_AcceptsGuids(string input, string expected)
    {
        Assert.Equal(expected, GuestIdValidator.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("<script>alert(1)</script>")]
    public void Normalize_ReturnsNullForEverythingElse(string? input)
    {
        Assert.Null(GuestIdValidator.Normalize(input));
    }
}
