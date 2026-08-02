using PSC.FunctionStudy.Analysis;

namespace PSC.FunctionStudy.Core.Tests;

public class EndpointAndCornerTests
{
    [Fact]
    public void Finds_the_minima_at_a_closed_endpoint()
    {
        // f' is infinite at x = ±2, so looking for zeros of f' can never find these
        var extrema = FunctionAnalyzer.Analyze("sqrt(4 - x^2)").Extrema;

        Assert.Contains(extrema, p => p.Kind == "endpoint minimum" && Math.Abs(p.X + 2) < 1e-6);
        Assert.Contains(extrema, p => p.Kind == "endpoint minimum" && Math.Abs(p.X - 2) < 1e-6);
        Assert.Contains(extrema, p => p.Kind == "local maximum" && Math.Abs(p.X) < 1e-4);
    }

    [Fact]
    public void Finds_the_corner_minimum_of_absolute_value()
    {
        // f' = x/|x| is never zero and is undefined at the origin
        var extrema = FunctionAnalyzer.Analyze("abs(x)").Extrema;

        var corner = Assert.Single(extrema, p => p.Kind == "local minimum");
        Assert.Equal(0, corner.X, 4);
        Assert.Equal(0, corner.Y, 4);
    }

    [Fact]
    public void Does_not_invent_a_corner_at_a_pole()
    {
        // 1/x is undefined at 0, but 0 is outside the domain and f' never changes sign
        Assert.Empty(FunctionAnalyzer.Analyze("1/x").Extrema);
    }

    [Fact]
    public void Does_not_duplicate_an_ordinary_turning_point()
    {
        // the sign-change scan sees x^3 - 3x's turning points too; they must not be added twice
        var extrema = FunctionAnalyzer.Analyze("x^3 - 3x").Extrema;
        Assert.Equal(2, extrema.Count);
    }
}

public class AbsoluteExtremeTests
{
    static StudyReport Study(string input) => FunctionAnalyzer.Analyze(input);

    [Fact]
    public void A_parabola_reaches_its_minimum_and_has_no_maximum()
    {
        var r = Study("x^2");

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMinimum!.Kind);
        Assert.Equal(0, r.AbsoluteMinimum.Value, 6);
        Assert.Equal(0, Assert.Single(r.AbsoluteMinimum.At), 4);

        Assert.Equal(ExtremeKind.Unbounded, r.AbsoluteMaximum!.Kind);
    }

    [Fact]
    public void A_cubic_is_unbounded_both_ways()
    {
        var r = Study("x^3 - 3x");
        Assert.Equal(ExtremeKind.Unbounded, r.AbsoluteMaximum!.Kind);
        Assert.Equal(ExtremeKind.Unbounded, r.AbsoluteMinimum!.Kind);
    }

    [Fact]
    public void A_bell_curve_reaches_its_peak_but_only_approaches_zero()
    {
        var r = Study("exp(-x^2)");

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMaximum!.Kind);
        Assert.Equal(1, r.AbsoluteMaximum.Value, 6);
        Assert.Equal(0, Assert.Single(r.AbsoluteMaximum.At), 4);

        // the x-axis is an asymptote, so 0 is an infimum that is never reached
        Assert.Equal(ExtremeKind.Approached, r.AbsoluteMinimum!.Kind);
        Assert.Equal(0, r.AbsoluteMinimum.Value, 6);
        Assert.Empty(r.AbsoluteMinimum.At);
    }

    [Fact]
    public void A_semicircle_reaches_both_ends()
    {
        var r = Study("sqrt(4 - x^2)");

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMaximum!.Kind);
        Assert.Equal(2, r.AbsoluteMaximum.Value, 6);

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMinimum!.Kind);
        Assert.Equal(0, r.AbsoluteMinimum.Value, 6);
        Assert.Equal(2, r.AbsoluteMinimum.At.Count);          // x = -2 and x = 2
    }

    [Fact]
    public void Sinc_approaches_one_at_the_hole_it_cannot_reach()
    {
        // 0 is outside the domain, so the value 1 is a supremum rather than a maximum —
        // exactly what the removable discontinuity means
        var r = Study("sin(x)/x");

        Assert.Equal(ExtremeKind.Approached, r.AbsoluteMaximum!.Kind);
        Assert.Equal(1, r.AbsoluteMaximum.Value, 6);

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMinimum!.Kind);
        Assert.True(r.AbsoluteMinimum.Value < 0);
    }

    [Fact]
    public void Arctangent_approaches_both_bounds_without_reaching_either()
    {
        var r = Study("atan(x)");

        Assert.Equal(ExtremeKind.Approached, r.AbsoluteMaximum!.Kind);
        Assert.Equal(Math.PI / 2, r.AbsoluteMaximum.Value, 4);
        Assert.Equal(ExtremeKind.Approached, r.AbsoluteMinimum!.Kind);
        Assert.Equal(-Math.PI / 2, r.AbsoluteMinimum.Value, 4);
    }

    [Fact]
    public void Absolute_value_reaches_its_minimum_at_the_corner()
    {
        var r = Study("abs(x)");

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMinimum!.Kind);
        Assert.Equal(0, r.AbsoluteMinimum.Value, 6);
        Assert.Equal(ExtremeKind.Unbounded, r.AbsoluteMaximum!.Kind);
    }

    [Fact]
    public void A_periodic_function_takes_its_bounds_despite_having_no_limit_at_infinity()
    {
        var r = Study("sin(x)");

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMaximum!.Kind);
        Assert.Equal(1, r.AbsoluteMaximum.Value, 6);
        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMinimum!.Kind);
        Assert.Equal(-1, r.AbsoluteMinimum.Value, 6);
    }

    [Fact]
    public void A_pole_makes_the_function_unbounded_both_ways()
    {
        var r = Study("1/x");
        Assert.Equal(ExtremeKind.Unbounded, r.AbsoluteMaximum!.Kind);
        Assert.Equal(ExtremeKind.Unbounded, r.AbsoluteMinimum!.Kind);
    }

    [Fact]
    public void Both_minima_of_a_quartic_are_listed()
    {
        var r = Study("x^4 - 2x^2");

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMinimum!.Kind);
        Assert.Equal(-1, r.AbsoluteMinimum.Value, 6);
        Assert.Equal(2, r.AbsoluteMinimum.At.Count);
        Assert.Equal(-1, r.AbsoluteMinimum.At[0], 4);
        Assert.Equal(1, r.AbsoluteMinimum.At[1], 4);
    }

    [Fact]
    public void A_constant_function_reaches_its_value()
    {
        var r = Study("5");

        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMaximum!.Kind);
        Assert.Equal(5, r.AbsoluteMaximum.Value, 6);
        Assert.Equal(ExtremeKind.Attained, r.AbsoluteMinimum!.Kind);
        Assert.Equal(5, r.AbsoluteMinimum.Value, 6);
    }

    [Fact]
    public void A_bell_curve_lists_only_its_real_turning_point()
    {
        // past |x| ~ 27 the values are bit-identical zeros; those flat spots are underflow,
        // not stationary points of the curve
        var extrema = FunctionAnalyzer.Analyze("exp(-x^2)").Extrema;
        var only = Assert.Single(extrema);
        Assert.Equal("local maximum", only.Kind);
        Assert.Equal(0, only.X, 4);
    }

    [Fact]
    public void An_empty_domain_leaves_the_extremes_unset()
    {
        var r = Study("sqrt(-1 - x^2)");
        Assert.Empty(r.Domain);
        Assert.Null(r.AbsoluteMaximum);
        Assert.Null(r.AbsoluteMinimum);
    }
}
