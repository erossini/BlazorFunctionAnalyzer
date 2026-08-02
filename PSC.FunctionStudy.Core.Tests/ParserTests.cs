using PSC.FunctionStudy.MathCore;

namespace PSC.FunctionStudy.Core.Tests;

public class ParserTests
{
    static double Eval(string input, double x) => Parser.Parse(input).Eval(x);

    [Theory]
    [InlineData("2x", "2*x")]
    [InlineData("3sin(x)", "3*sin(x)")]
    [InlineData("x(x + 1)", "x*(x + 1)")]
    [InlineData("2pi", "2*pi")]
    public void Implicit_multiplication_matches_the_explicit_form(string implicitForm, string explicitForm) =>
        Assert.Equal(Eval(explicitForm, 1.7), Eval(implicitForm, 1.7), 10);

    [Theory]
    [InlineData("x**2", "x^2")]
    [InlineData("[x + 1]*{x - 1}", "(x + 1)*(x - 1)")]
    [InlineData("|x - 3|", "abs(x - 3)")]
    [InlineData("sqrt x", "sqrt(x)")]
    [InlineData("2·x", "2*x")]
    [InlineData("2×x", "2*x")]
    public void Accepts_the_notations_students_actually_type(string typed, string canonical) =>
        Assert.Equal(Eval(canonical, 1.7), Eval(typed, 1.7), 10);

    [Fact]
    public void Accepts_the_unicode_minus_pasted_from_textbooks() =>
        Assert.Equal(Eval("x - 1", 5), Eval("x − 1", 5), 10);

    [Fact]
    public void Sin_squared_is_textbook_shorthand_for_the_square_of_sin()
    {
        // sin^2(x) means (sin x)^2, not sin(sin(x)) or sin(x^2)
        var expected = Math.Pow(Math.Sin(1.3), 2);
        Assert.Equal(expected, Eval("sin^2(x)", 1.3), 10);
    }

    [Fact]
    public void Sin_to_a_negative_power_is_refused_rather_than_guessed()
    {
        // sin^-1 reads as arcsin in most textbooks but as 1/sin algebraically — picking one
        // silently would quietly study the wrong function
        Assert.False(Parser.TryParse("sin^-1(x)", out _, out var error));
        Assert.Contains("ambiguous", error);
    }

    [Fact]
    public void Power_is_right_associative() =>
        Assert.Equal(512, Eval("2^3^2", 0), 10);   // 2^(3^2), not (2^3)^2 = 64

    [Fact]
    public void Unary_minus_binds_looser_than_power() =>
        Assert.Equal(-4, Eval("-x^2", 2), 10);     // -(x^2), not (-x)^2

    [Fact]
    public void Odd_roots_of_negative_numbers_are_allowed() =>
        Assert.Equal(-2, Eval("x^(1/3)", -8), 6);

    [Fact]
    public void Even_roots_of_negative_numbers_are_not_a_real_number() =>
        Assert.True(double.IsNaN(Eval("sqrt(x)", -1)));

    [Theory]
    [InlineData("x +")]
    [InlineData("(x + 1")]
    [InlineData("foo(x)")]
    [InlineData("x @ 2")]
    [InlineData("|x")]
    public void Reports_a_message_instead_of_throwing_for_bad_input(string input)
    {
        Assert.False(Parser.TryParse(input, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Names_the_allowed_functions_when_an_identifier_is_unknown()
    {
        Parser.TryParse("foo(x)", out _, out var error);
        Assert.Contains("sqrt", error);
    }

    [Theory]
    [InlineData("x^2 - 1", "x^2 − 1")]
    [InlineData("2*x", "2·x")]
    [InlineData("(x + 1)*(x - 1)", "(x + 1)·(x − 1)")]
    [InlineData("abs(x)", "|x|")]
    public void Prints_with_unicode_math_glyphs_and_minimal_parentheses(string input, string expected) =>
        Assert.Equal(expected, Parser.Parse(input).ToString());
}

public class SimplifierTests
{
    static string Simplify(string input) => Simplifier.Run(Parser.Parse(input)).ToString();

    [Theory]
    [InlineData("x + 0", "x")]
    [InlineData("x*1", "x")]
    [InlineData("x*0", "0")]
    [InlineData("x/1", "x")]
    [InlineData("x^1", "x")]
    [InlineData("x^0", "1")]
    [InlineData("2 + 3", "5")]
    [InlineData("x - x", "0")]
    [InlineData("x*x", "x^2")]
    [InlineData("--x", "x")]
    public void Folds_constants_and_drops_neutral_elements(string input, string expected) =>
        Assert.Equal(expected, Simplify(input));

    [Theory]
    [InlineData("e^x", "exp(x)")]
    [InlineData("e^(-x^2)", "exp(−x^2)")]
    [InlineData("e^(2x)", "exp(2·x)")]
    [InlineData("ln(e)", "1")]
    public void Rewrites_powers_of_e_as_exp(string input, string expected) =>
        Assert.Equal(expected, Simplify(input));

    [Fact]
    public void The_derivative_of_e_to_the_x_does_not_leak_a_decimal_constant()
    {
        // written as a power, e^x differentiates through the a^u rule and prints
        // as 2.718282^x·ln(2.718282)
        var d = Simplifier.Run(Simplifier.Run(Parser.Parse("e^x")).Derivative());
        Assert.Equal("exp(x)", d.ToString());
        Assert.DoesNotContain("2.718", d.ToString());
    }

    [Fact]
    public void Simplifying_never_changes_the_value()
    {
        string[] inputs =
        {
            "x^3 - 3x", "(x^2 - 1)/(x - 2)", "x*exp(-x)", "sin(x)*cos(x)",
            "2*(x + 1) - 2", "(x^2 + 1)/x", "sqrt(x^2 + 1)",
        };
        foreach (var input in inputs)
        {
            var original = Parser.Parse(input);
            var simplified = Simplifier.Run(original);
            foreach (var x in new[] { 0.3, 1.4, 2.9, 4.1 })
                Assert.Equal(original.Eval(x), simplified.Eval(x), 9);
        }
    }
}
