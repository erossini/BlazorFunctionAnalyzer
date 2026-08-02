using PSC.FunctionStudy.Analysis;
using PSC.FunctionStudy.MathCore;

namespace PSC.FunctionStudy.Core.Tests;

public class DerivativeTests
{
    /// <summary>
    /// The engine differentiates symbolically, so the check is against a numeric slope:
    /// if the two agree at several points, the rule that produced the derivative is right.
    /// </summary>
    [Theory]
    [InlineData("x^3 - 3x")]
    [InlineData("x*exp(-x)")]
    [InlineData("sin(x)*cos(x)")]
    [InlineData("(x^2 - 1)/(x + 3)")]
    [InlineData("ln(x^2 + 1)")]
    [InlineData("sqrt(x + 1)")]
    [InlineData("exp(-x^2)")]
    [InlineData("atan(x)")]
    [InlineData("tanh(x)")]
    [InlineData("2^x")]
    [InlineData("x^x")]
    [InlineData("abs(x)")]
    [InlineData("cbrt(x)")]
    [InlineData("sin(x)/x")]
    public void Symbolic_derivative_matches_the_numeric_slope(string input)
    {
        var f = Parser.Parse(input);
        var fp = Simplifier.Run(f.Derivative());

        foreach (var x in new[] { 0.3, 1.2, 2.6, 4.4 })
        {
            const double h = 1e-6;
            double numeric = (f.Eval(x + h) - f.Eval(x - h)) / (2 * h);
            double symbolic = fp.Eval(x);
            if (!double.IsFinite(numeric) || !double.IsFinite(symbolic)) continue;

            double tolerance = 1e-4 * (1 + Math.Abs(numeric));
            Assert.True(Math.Abs(symbolic - numeric) <= tolerance,
                $"d/dx {input} at x={x}: symbolic {symbolic}, numeric {numeric}");
        }
    }

    [Fact]
    public void Second_derivative_matches_the_numeric_curvature()
    {
        foreach (var input in new[] { "x^4 - x^2", "exp(-x^2)", "ln(x + 5)", "sin(x)" })
        {
            var f = Parser.Parse(input);
            var fpp = Simplifier.Run(Simplifier.Run(f.Derivative()).Derivative());

            foreach (var x in new[] { 0.4, 1.5, 2.7 })
            {
                const double h = 1e-4;
                double numeric = (f.Eval(x + h) - 2 * f.Eval(x) + f.Eval(x - h)) / (h * h);
                double symbolic = fpp.Eval(x);
                double tolerance = 1e-3 * (1 + Math.Abs(numeric));
                Assert.True(Math.Abs(symbolic - numeric) <= tolerance,
                    $"d²/dx² {input} at x={x}: symbolic {symbolic}, numeric {numeric}");
            }
        }
    }
}

public class DomainTests
{
    [Fact]
    public void Excludes_a_zero_of_the_denominator()
    {
        var d = FunctionAnalyzer.Analyze("1/(x - 2)").Domain;
        Assert.Contains(d, i => i.Contains(1));
        Assert.Contains(d, i => i.Contains(3));
        Assert.DoesNotContain(d, i => i.Contains(2));
    }

    [Fact]
    public void Keeps_only_where_a_logarithm_is_positive()
    {
        var d = FunctionAnalyzer.Analyze("ln(x)").Domain;
        Assert.Contains(d, i => i.Contains(1));
        Assert.DoesNotContain(d, i => i.Contains(0));
        Assert.DoesNotContain(d, i => i.Contains(-1));
    }

    [Fact]
    public void Closes_the_interval_at_a_square_root_boundary()
    {
        var d = FunctionAnalyzer.Analyze("sqrt(4 - x^2)").Domain;
        Assert.Contains(d, i => i.Contains(0));
        Assert.Contains(d, i => i.Contains(-2));   // sqrt(0) is defined, so -2 is included
        Assert.Contains(d, i => i.Contains(2));
        Assert.DoesNotContain(d, i => i.Contains(2.5));
    }

    [Fact]
    public void Combines_two_conditions()
    {
        // needs x - 1 > 0 for the log and x != 3 for the denominator
        var d = FunctionAnalyzer.Analyze("ln(x - 1)/(x - 3)").Domain;
        Assert.DoesNotContain(d, i => i.Contains(0.5));
        Assert.Contains(d, i => i.Contains(2));
        Assert.DoesNotContain(d, i => i.Contains(3));
        Assert.Contains(d, i => i.Contains(4));
    }

    [Fact]
    public void Reports_every_real_number_when_nothing_is_restricted()
    {
        var report = FunctionAnalyzer.Analyze("x^3 - 3x");
        Assert.Empty(report.Conditions);
        Assert.True(report.ReachesMinusInfinity);
        Assert.True(report.ReachesPlusInfinity);
    }
}

public class StudyTests
{
    [Theory]
    [InlineData("x^2", Symmetry.Even)]
    [InlineData("cos(x)", Symmetry.Even)]
    [InlineData("x^3", Symmetry.Odd)]
    [InlineData("sin(x)", Symmetry.Odd)]
    [InlineData("x^2 + x", Symmetry.None)]
    [InlineData("exp(x)", Symmetry.None)]
    public void Detects_symmetry(string input, Symmetry expected) =>
        Assert.Equal(expected, FunctionAnalyzer.Analyze(input).Symmetry);

    [Theory]
    [InlineData("sin(x)", 2 * Math.PI)]
    [InlineData("cos(x)", 2 * Math.PI)]
    [InlineData("tan(x)", Math.PI)]
    public void Detects_period(string input, double expected)
    {
        var period = FunctionAnalyzer.Analyze(input).Period;
        Assert.NotNull(period);
        Assert.Equal(expected, period!.Value, 6);
    }

    [Fact]
    public void Reports_no_period_for_a_non_periodic_function() =>
        Assert.Null(FunctionAnalyzer.Analyze("x^3 - 3x").Period);

    [Fact]
    public void Finds_x_intercepts()
    {
        var roots = FunctionAnalyzer.Analyze("x^2 - 1").XIntercepts;
        Assert.Equal(2, roots.Count);
        Assert.Equal(-1, roots[0], 6);
        Assert.Equal(1, roots[1], 6);
    }

    [Fact]
    public void Finds_the_y_intercept()
    {
        var report = FunctionAnalyzer.Analyze("x^2 - 4");
        Assert.NotNull(report.YIntercept);
        Assert.Equal(-4, report.YIntercept!.Value, 6);
    }

    [Fact]
    public void Reports_no_y_intercept_when_zero_is_outside_the_domain() =>
        Assert.Null(FunctionAnalyzer.Analyze("1/x").YIntercept);

    [Fact]
    public void Finds_a_vertical_asymptote()
    {
        var a = FunctionAnalyzer.Analyze("1/(x - 2)").Asymptotes;
        Assert.Contains(a, v => v.Kind == "vertical" && Math.Abs(v.P - 2) < 1e-6);
    }

    [Fact]
    public void Finds_a_horizontal_asymptote()
    {
        var a = FunctionAnalyzer.Analyze("1/x").Asymptotes;
        Assert.Contains(a, h => h.Kind == "horizontal" && Math.Abs(h.Q) < 1e-6);
    }

    [Fact]
    public void Finds_an_oblique_asymptote()
    {
        // (x^2 + 1)/x = x + 1/x, so the oblique asymptote is y = x
        var a = FunctionAnalyzer.Analyze("(x^2 + 1)/x").Asymptotes;
        Assert.Contains(a, o => o.Kind == "oblique"
                                && Math.Abs(o.P - 1) < 1e-6
                                && Math.Abs(o.Q) < 1e-6);
    }

    [Fact]
    public void Finds_a_maximum_and_a_minimum()
    {
        var extrema = FunctionAnalyzer.Analyze("x^3 - 3x").Extrema;
        Assert.Contains(extrema, p => p.Kind.StartsWith("local max") && Math.Abs(p.X + 1) < 1e-4);
        Assert.Contains(extrema, p => p.Kind.StartsWith("local min") && Math.Abs(p.X - 1) < 1e-4);
    }

    [Fact]
    public void Finds_an_inflection_point()
    {
        var inflections = FunctionAnalyzer.Analyze("x^3").Inflections;
        Assert.Contains(inflections, p => Math.Abs(p.X) < 1e-4);
    }

    [Fact]
    public void A_bell_curve_has_one_maximum_and_nothing_else()
    {
        // exp(-x^2)'s derivative underflows to exactly 0.0 beyond |x| ~ 27, which used to be
        // reported as a separate stationary point at every sample — 1272 of them
        var extrema = FunctionAnalyzer.Analyze("exp(-x^2)").Extrema;

        var turning = extrema.Where(p => p.Kind.StartsWith("local")).ToList();
        var only = Assert.Single(turning);
        Assert.StartsWith("local max", only.Kind);
        Assert.Equal(0, only.X, 4);

        // the remainder are where the underflow run meets a non-zero value: flat, not turning
        Assert.True(extrema.Count < 10, $"expected a handful of points, got {extrema.Count}");
    }

    [Fact]
    public void Reports_no_stationary_points_for_a_monotonic_function() =>
        Assert.Empty(FunctionAnalyzer.Analyze("exp(x)").Extrema);

    [Fact]
    public void Sign_chart_puts_a_parabola_below_the_axis_between_its_roots()
    {
        var pieces = FunctionAnalyzer.Analyze("x^2 - 1").SignChart;
        var middle = pieces.Single(p => p.Where.Contains(0));
        Assert.Equal(-1, middle.Sign);
    }

    [Fact]
    public void The_default_sample_studies_end_to_end()
    {
        var report = FunctionAnalyzer.Analyze("(x^2 - 1)/(x - 2)");

        Assert.Equal("Rational function", report.Recognition!.Family);
        Assert.Single(report.Conditions);
        Assert.Equal(2, report.Domain.Count);                 // split at x = 2
        Assert.Equal(2, report.XIntercepts.Count);            // x = -1 and x = 1
        Assert.Contains(report.Asymptotes, a => a.Kind == "vertical");
        Assert.Contains(report.Asymptotes, a => a.Kind == "oblique");
    }

    [Fact]
    public void An_empty_domain_returns_a_report_rather_than_throwing()
    {
        var report = FunctionAnalyzer.Analyze("sqrt(-1 - x^2)");
        Assert.Empty(report.Domain);
        Assert.NotNull(report.Recognition);                   // still classified
    }
}
