using AwesomeAssertions;
using Xunit;

namespace Microsoft.TryDotNet.FileIntegration.Tests;

public class ListingSourceNormalizerTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void removes_nested_duplicate_regions_preserving_line_breaks(string newline)
    {
        var source = string.Join(newline,
            "#region duplicate",
            "class Widget",
            "{",
            "#region duplicate",
            "    public int Value { get; set; }",
            "#endregion duplicate",
            "}",
            "#endregion duplicate",
            "");
        var expected = string.Join(newline,
            new string(' ', 17),
            "class Widget",
            "{",
            new string(' ', 17),
            "    public int Value { get; set; }",
            new string(' ', 20),
            "}",
            new string(' ', 20),
            "");

        ListingSourceNormalizer.RemoveRegions(source).Should().Be(expected);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void removes_orphan_endregion_preserving_line_breaks(string newline)
    {
        var source = $"#endregion EXCLUDEe{newline}class Widget {{ }}{newline}";
        var expected = $"{new string(' ', 19)}{newline}class Widget {{ }}{newline}";

        ListingSourceNormalizer.RemoveRegions(source).Should().Be(expected);
    }

    [Fact]
    public void removes_orphan_endregion_without_adding_a_trailing_line_break()
    {
        var source = "class Widget { }\n#endregion EXCLUDEe";
        var expected = "class Widget { }\n" + new string(' ', 19);

        ListingSourceNormalizer.RemoveRegions(source).Should().Be(expected);
    }

    [Fact]
    public void preserves_columns_and_line_breaks_for_indented_region_directives()
    {
        var source = "    #region active\r\n    class Widget { }\r\n    #endregion active\r\n";
        var expected = new string(' ', 18) + "\r\n    class Widget { }\r\n" + new string(' ', 21) + "\r\n";

        ListingSourceNormalizer.RemoveRegions(source).Should().Be(expected);
    }

    [Fact]
    public void preserves_code_conditional_directives_and_comments()
    {
        var source = """
            #define FEATURE
            #if FEATURE
            class Widget
            {
                // #region comment
                /*
            #region block comment
            #endregion EXCLUDEe
                */
                public string Value => "#endregion EXCLUDEe";
            }
            #else
            class OtherWidget { }
            #endif
            """;

        ListingSourceNormalizer.RemoveRegions(source).Should().Be(source);
    }

    [Fact]
    public void preserves_apparent_region_directives_inside_raw_strings()
    {
        var source = """"
            class Widget
            {
                public string Value => """
            #region duplicate
            #region duplicate
            #endregion duplicate
            #endregion EXCLUDEe
            """;
            }
            """";

        ListingSourceNormalizer.RemoveRegions(source).Should().Be(source);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void removes_region_directives_in_disabled_code_without_changing_other_disabled_text(string newline)
    {
        var source = string.Join(newline,
            "#if false",
            "#region duplicate",
            "this is disabled text, not valid C#;",
            "#region duplicate",
            "more disabled text",
            "#endregion duplicate",
            "#endregion duplicate",
            "#endregion EXCLUDEe",
            "#else",
            "class Widget { }",
            "#endif",
            "");
        var expected = string.Join(newline,
            "#if false",
            new string(' ', 17),
            "this is disabled text, not valid C#;",
            new string(' ', 17),
            "more disabled text",
            new string(' ', 20),
            new string(' ', 20),
            new string(' ', 19),
            "#else",
            "class Widget { }",
            "#endif",
            "");

        ListingSourceNormalizer.RemoveRegions(source).Should().Be(expected);
    }
}
