using PSC.FunctionStudy.Analysis;
using PSC.FunctionStudy.MathCore;

namespace PSC.FunctionStudy.Core.Tests;

public class RecogniserTests
{
    static Recognition Describe(string input) =>
        Recogniser.Describe(Simplifier.Run(Parser.Parse(input)));

    [Theory]
    [InlineData("5", "Constant function")]
    [InlineData("2x + 1", "Linear function")]
    [InlineData("x", "Linear function")]
    [InlineData("x^2 - 4", "Quadratic function")]
    [InlineData("x^3 - 3x", "Cubic function")]
    [InlineData("x^4 - x^2", "Quartic function")]
    [InlineData("x^7 + x", "Polynomial of degree 7")]
    public void Classifies_polynomials_by_degree(string input, string expected) =>
        Assert.Equal(expected, Describe(input).Family);

    [Theory]
    [InlineData("x*x", "Quadratic function")]          // simplifier folds this to x^2
    [InlineData("x^2/2", "Quadratic function")]        // dividing by a constant stays polynomial
    [InlineData("(x + 1)*(x - 1)", "Quadratic function")]
    public void Recognises_polynomials_after_simplification(string input, string expected) =>
        Assert.Equal(expected, Describe(input).Family);

    [Theory]
    [InlineData("(x^2 - 1)/(x - 2)")]
    [InlineData("(x^2 + 1)/x")]
    [InlineData("1/(x - 3)")]
    public void Classifies_rational_functions(string input) =>
        Assert.Equal("Rational function", Describe(input).Family);

    [Theory]
    // numerator degree, denominator degree -> the asymptote the note should predict
    [InlineData("1/(x^2 + 1)", "horizontal asymptote y = 0")]
    [InlineData("(2x + 1)/(x - 1)", "ratio of the leading coefficients")]
    [InlineData("(x^2 + 1)/x", "oblique asymptote")]
    [InlineData("(x^4 + 1)/x", "no horizontal or oblique asymptote")]
    public void Rational_note_predicts_the_asymptote(string input, string expected) =>
        Assert.Contains(expected, Describe(input).Explanation);

    [Theory]
    [InlineData("sin(x) + cos(x)", "Trigonometric function")]
    [InlineData("exp(2x)", "Exponential function")]
    [InlineData("2^x", "Exponential function")]           // variable in the exponent
    [InlineData("ln(x - 1)", "Logarithmic function")]
    [InlineData("sqrt(x + 3)", "Radical function")]
    [InlineData("x^(1/3)", "Radical function")]           // fractional exponent
    [InlineData("tanh(x)", "Hyperbolic function")]
    [InlineData("atan(x)", "Inverse trigonometric function")]
    public void Classifies_single_family_transcendentals(string input, string expected) =>
        Assert.Equal(expected, Describe(input).Family);

    [Fact]
    public void Classifies_mixed_expressions_as_composite()
    {
        var r = Describe("exp(-x)*sin(x)");
        Assert.Equal("Composite function", r.Family);
        Assert.Contains("exponential", r.Explanation);
        Assert.Contains("trigonometric", r.Explanation);
    }

    [Theory]
    [InlineData("1/x", "Hyperbola")]
    [InlineData("x^2", "Parabola")]
    [InlineData("sqrt(x)", "Square-root curve")]
    [InlineData("abs(x)", "Absolute value")]
    [InlineData("exp(x)", "Natural exponential")]
    [InlineData("ln(x)", "Natural logarithm")]
    [InlineData("sin(x)/x", "Sinc function")]
    [InlineData("exp(-x^2)", "Gaussian (bell curve)")]
    [InlineData("1/(1 + x^2)", "Witch of Agnesi")]
    [InlineData("1/(1 + exp(-x))", "Logistic (sigmoid) curve")]
    [InlineData("sqrt(1 - x^2)", "Upper unit semicircle")]
    [InlineData("cosh(x)", "Catenary")]
    public void Names_catalogued_curves(string input, string expected)
    {
        var r = Describe(input);
        Assert.True(r.IsNamed, $"expected '{input}' to be recognised as {expected}");
        Assert.Equal(expected, r.Name);
    }

    [Theory]
    // the catalogue is keyed on the normalised tree, so spelling variants must still match
    [InlineData("sin x / x", "Sinc function")]
    [InlineData("x*x", "Parabola")]
    [InlineData("1/(x^2 + 1)", "Witch of Agnesi")]
    [InlineData("e^(-x^2)", "Gaussian (bell curve)")]
    public void Catalogue_matches_are_independent_of_how_it_was_typed(string input, string expected) =>
        Assert.Equal(expected, Describe(input).Name);

    [Theory]
    [InlineData("x^3 - 3x")]
    [InlineData("(x^2 - 1)/(x - 2)")]
    [InlineData("x*exp(-x)")]
    public void Leaves_uncatalogued_functions_unnamed(string input)
    {
        var r = Describe(input);
        Assert.False(r.IsNamed);
        Assert.NotEmpty(r.Family);
        Assert.NotEmpty(r.Explanation);
    }

    [Fact]
    public void Every_catalogue_entry_is_reachable()
    {
        // guards against an entry whose source text normalises to the same key as another,
        // silently shadowing it
        var sources = new[]
        {
            "1/x", "x^2", "x^3", "sqrt(x)", "cbrt(x)", "abs(x)", "exp(x)", "exp(-x)", "ln(x)",
            "sin(x)", "cos(x)", "tan(x)", "sin(x)/x", "exp(-x^2)", "1/(1+x^2)",
            "1/(1+exp(-x))", "sqrt(1-x^2)", "cosh(x)",
        };
        var names = sources.Select(s => Describe(s).Name).ToList();
        Assert.All(names, n => Assert.NotNull(n));
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Analyze_populates_recognition()
    {
        var report = FunctionAnalyzer.Analyze("sin(x)/x");
        Assert.NotNull(report.Recognition);
        Assert.Equal("Sinc function", report.Recognition!.Name);
    }
}
