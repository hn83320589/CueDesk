using CueDesk.Core.Text;

namespace CueDesk.Core.Tests.Text;

public class MarkupTests
{
    [Theory]
    [InlineData("<i>Hi</i>", "Hi")]
    [InlineData("{\\an8}<font color=\"#fff\">字</font>", "字")]
    [InlineData("plain", "plain")]
    [InlineData("<b><u>x</u></b>", "x")]
    public void Strip_removes_tags_and_style_codes(string input, string expected) =>
        Assert.Equal(expected, Markup.Strip(input));

    [Theory]
    [InlineData("Hello", 5)]
    [InlineData("你好", 4)]
    [InlineData("你好，world", 11)]   // full-width comma counts 2
    [InlineData("ＡＢ", 4)]            // full-width latin
    [InlineData("𠀀", 2)]             // CJK Extension B (surrogate pair) counts once, wide
    public void DisplayWidth_weights_east_asian_wide_characters(string input, int expected) =>
        Assert.Equal(expected, Markup.DisplayWidth(input));
}
