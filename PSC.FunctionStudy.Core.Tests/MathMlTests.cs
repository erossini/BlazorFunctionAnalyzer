using PSC.FunctionStudy.MathCore;

namespace PSC.FunctionStudy.Core.Tests;

public class MathMlTests
{
    static string Render(string input) => MathMl.Render(Simplifier.Run(Parser.Parse(input)));

    [Fact]
    public void Wraps_the_whole_expression_in_a_math_element()
    {
        var html = Render("x");
        Assert.StartsWith("<math>", html);
        Assert.EndsWith("</math>", html);
    }

    [Theory]
    [InlineData("1/x", "<mfrac>")]
    [InlineData("sqrt(x)", "<msqrt>")]
    [InlineData("cbrt(x)", "<mroot>")]
    [InlineData("x^2", "<msup>")]
    [InlineData("exp(x)", "<msup><mi>e</mi>")]     // exp(u) is written e^u once typeset
    [InlineData("sin(x)", "<mi>sin</mi>")]
    public void Uses_the_element_that_matches_the_notation(string input, string expected) =>
        Assert.Contains(expected, Render(input));

    [Fact]
    public void A_fraction_bar_replaces_the_parentheses_the_flat_form_needs()
    {
        var flat = Simplifier.Run(Parser.Parse("(x + 1)/(x - 1)")).ToString();
        Assert.Contains("(", flat);                 // the text form has to bracket both sides

        var html = Render("(x + 1)/(x - 1)");
        Assert.Contains("<mfrac>", html);
        Assert.DoesNotContain("<mo>(</mo>", html);  // the bar groups them instead
    }

    [Fact]
    public void A_radical_covers_its_argument_without_brackets() =>
        Assert.DoesNotContain("<mo>(</mo>", Render("sqrt(x + 1)"));

    [Fact]
    public void Keeps_parentheses_where_the_layout_does_not_group()
    {
        // a sum raised to a power still needs them around the base
        Assert.Contains("<mo>(</mo>", Render("(x + 1)^2"));
    }

    [Fact]
    public void Renders_a_negative_number_with_a_real_minus_sign()
    {
        var html = Render("x - 3");
        Assert.Contains("−", html);        // U+2212, not the ASCII hyphen
        Assert.DoesNotContain("<mn>-", html);
    }

    [Fact]
    public void Absolute_value_uses_bars() =>
        Assert.Contains("<mo>|</mo>", Render("abs(x - 2)"));

    [Fact]
    public void Marks_function_application_with_the_invisible_operator() =>
        Assert.Contains("⁡", Render("sin(x)"));

    [Fact]
    public void Block_display_is_opt_in()
    {
        Assert.DoesNotContain("display", MathMl.Render(Parser.Parse("x")));
        Assert.Contains("display=\"block\"", MathMl.Render(Parser.Parse("x"), block: true));
    }

    [Fact]
    public void Renders_every_construct_the_parser_accepts_without_throwing()
    {
        string[] inputs =
        {
            "(x^2 - 1)/(x - 2)", "sin(x)/x", "exp(-x^2)", "sqrt(4 - x^2)", "ln(x^2 - 4)",
            "abs(x)/x", "cbrt(x + 1)", "2^x", "x^x", "atan(x)", "tanh(x)", "-x^2",
            "1/(1 + exp(-x))", "x*exp(-x)", "(x + 1)^3/(x - 1)^2",
        };

        foreach (var input in inputs)
        {
            var f = Simplifier.Run(Parser.Parse(input));

            foreach (var e in new[] { f, Simplifier.Run(f.Derivative()) })
            {
                var html = MathMl.Render(e);
                Assert.StartsWith("<math>", html);
                Assert.DoesNotContain("<mi>?</mi>", html);          // no unhandled node type
                Assert.Equal(Count(html, "<mrow>"), Count(html, "</mrow>"));
                Assert.Equal(Count(html, "<mfrac>"), Count(html, "</mfrac>"));
                Assert.Equal(Count(html, "<msup>"), Count(html, "</msup>"));
            }
        }

        static int Count(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }
    }
}
