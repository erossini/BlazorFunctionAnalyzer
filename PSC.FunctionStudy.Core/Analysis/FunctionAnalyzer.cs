using PSC.FunctionStudy.MathCore;

namespace PSC.FunctionStudy.Analysis;

public enum Symmetry { None, Even, Odd }

public sealed record BoundaryLimit(double X, Limit FromLeft, Limit FromRight, bool HasLeft, bool HasRight);

/// <summary>P and Q carry the numbers the plot needs: vertical -> P = x;
/// horizontal -> Q = y; oblique -> P = m, Q = q.</summary>
public sealed record Asymptote(string Kind, string Equation, string Reason, double P = 0, double Q = 0);

public sealed record NotablePoint(double X, double Y, string Kind);

public enum ExtremeKind
{
    /// <summary>The function actually reaches this value, at the listed x.</summary>
    Attained,
    /// <summary>The values close in on this bound without ever arriving — a supremum or infimum.</summary>
    Approached,
    /// <summary>No bound on this side.</summary>
    Unbounded,
    /// <summary>A limit does not exist, so nothing can be claimed either way.</summary>
    Undetermined,
}

/// <summary>The largest or smallest value the function takes over its whole domain.</summary>
public sealed record Extreme(ExtremeKind Kind, double Value, IReadOnlyList<double> At);

public sealed class StudyReport
{
    public string Input { get; init; } = "";
    public Expr F { get; init; } = Expr.Zero;
    public Expr FPrime { get; init; } = Expr.Zero;
    public Expr FSecond { get; init; } = Expr.Zero;

    public Recognition? Recognition { get; set; }

    public List<Constraint> Conditions { get; } = new();
    public List<Interval> Domain { get; } = new();
    public Symmetry Symmetry { get; set; }
    public double? Period { get; set; }

    public double? YIntercept { get; set; }
    public List<double> XIntercepts { get; } = new();

    public List<DomainSolver.SignPiece> SignChart { get; } = new();
    public List<BoundaryLimit> Limits { get; } = new();
    public Limit LimitAtMinusInfinity { get; set; } = Limit.DNE;
    public Limit LimitAtPlusInfinity { get; set; } = Limit.DNE;
    public bool ReachesMinusInfinity { get; set; }
    public bool ReachesPlusInfinity { get; set; }

    public List<Asymptote> Asymptotes { get; } = new();
    public List<DomainSolver.SignPiece> Monotonicity { get; } = new();
    public List<NotablePoint> Extrema { get; } = new();
    public Extreme? AbsoluteMaximum { get; set; }
    public Extreme? AbsoluteMinimum { get; set; }
    public List<DomainSolver.SignPiece> Concavity { get; } = new();
    public List<NotablePoint> Inflections { get; } = new();

    public string DomainText =>
        Domain.Count == 0 ? "empty" : string.Join(" \u222A ", Domain.Select(i => i.ToString()));
}

public static class FunctionAnalyzer
{
    const double Range = 40;

    public static StudyReport Analyze(string input)
    {
        var f = Simplifier.Run(Parser.Parse(input));
        var fp = Simplifier.Run(f.Derivative());
        var fpp = Simplifier.Run(fp.Derivative());

        var r = new StudyReport { Input = input, F = f, FPrime = fp, FSecond = fpp };

        // 0. what kind of function this is — structural, so it does not need the domain
        r.Recognition = Recogniser.Describe(f);

        // 1. existence conditions and domain
        r.Conditions.AddRange(DomainSolver.CollectConstraints(f));
        r.Domain.AddRange(DomainSolver.Solve(f, r.Conditions, Range));
        if (r.Domain.Count == 0) return r;

        r.ReachesMinusInfinity = r.Domain.Any(i => double.IsNegativeInfinity(i.A));
        r.ReachesPlusInfinity = r.Domain.Any(i => double.IsPositiveInfinity(i.B));

        // 2. symmetry
        r.Symmetry = DetectSymmetry(f, r.Domain);
        r.Period = DetectPeriod(f, r.Domain);

        // 3. intercepts
        if (r.Domain.Any(i => i.Contains(0)))
        {
            double y0 = Numeric.Safe(f.Eval, 0);
            if (Numeric.Finite(y0)) r.YIntercept = y0;
        }
        foreach (var iv in r.Domain)
        {
            double lo = double.IsInfinity(iv.A) ? -Range : iv.A;
            double hi = double.IsInfinity(iv.B) ? Range : iv.B;
            foreach (var z in Numeric.Roots(f.Eval, lo, hi, 4000))
                if (iv.Contains(z)) r.XIntercepts.Add(Math.Round(z, 6));
        }
        r.XIntercepts.Sort();

        // 4. sign
        r.SignChart.AddRange(DomainSolver.SignChart(f.Eval, r.Domain, Range));

        // 5. limits at the edges of the domain
        foreach (var x0 in EdgePoints(r.Domain))
        {
            bool hasLeft = r.Domain.Any(i => i.B >= x0 - 1e-12 && i.A < x0);
            bool hasRight = r.Domain.Any(i => i.A <= x0 + 1e-12 && i.B > x0);
            r.Limits.Add(new BoundaryLimit(
                x0,
                hasLeft ? Numeric.At(f.Eval, x0, -1) : Limit.DNE,
                hasRight ? Numeric.At(f.Eval, x0, +1) : Limit.DNE,
                hasLeft, hasRight));
        }
        if (r.ReachesMinusInfinity) r.LimitAtMinusInfinity = Numeric.AtInfinity(f.Eval, -1);
        if (r.ReachesPlusInfinity) r.LimitAtPlusInfinity = Numeric.AtInfinity(f.Eval, +1);

        // 6. asymptotes
        BuildAsymptotes(r, f);

        // 7. first derivative
        r.Monotonicity.AddRange(DomainSolver.SignChart(fp.Eval, r.Domain, Range));
        foreach (var iv in r.Domain)
        {
            double lo = double.IsInfinity(iv.A) ? -Range : iv.A;
            double hi = double.IsInfinity(iv.B) ? Range : iv.B;
            foreach (var c in Numeric.Roots(fp.Eval, lo, hi, 4000))
            {
                if (!iv.Contains(c)) continue;

                // A point sitting inside an exactly flat run is underflow, not a feature of the
                // curve — exp(-x^2) evaluates to precisely 0.0 past |x| ~ 27. Exact comparison is
                // deliberate here: the question is whether the values are bit-identical.
                double at = Numeric.Safe(f.Eval, c);
                if (Numeric.Safe(f.Eval, c - 1e-4) == at && Numeric.Safe(f.Eval, c + 1e-4) == at) continue;

                double left = Numeric.Safe(fp.Eval, c - 1e-4);
                double right = Numeric.Safe(fp.Eval, c + 1e-4);
                string kind =
                    left > 0 && right < 0 ? "local maximum" :
                    left < 0 && right > 0 ? "local minimum" :
                    "stationary (no change of monotonicity)";
                r.Extrema.Add(new NotablePoint(Math.Round(c, 6), Numeric.Safe(f.Eval, c), kind));
            }

            // corners: f' flips sign without ever being zero, so looking for roots of f' cannot
            // find them. |x| at the origin is the standard example.
            foreach (var (bracketLo, bracketHi) in Numeric.SignChanges(fp.Eval, lo, hi))
            {
                if (r.Extrema.Any(p => p.X >= bracketLo - 1e-6 && p.X <= bracketHi + 1e-6)) continue;

                bool rises = Numeric.Safe(fp.Eval, bracketHi) > 0;
                double c = Numeric.Turn(f.Eval, bracketLo, bracketHi, rises);
                if (!iv.Contains(c)) continue;

                double y = Numeric.Safe(f.Eval, c);
                if (!Numeric.Finite(y)) continue;
                r.Extrema.Add(new NotablePoint(Math.Round(c, 6), y,
                    rises ? "local minimum" : "local maximum"));
            }

            // a closed endpoint is an extremum too, and f' need not vanish there — sqrt(4 - x^2)
            // takes its minimum at x = ±2, where the derivative is infinite
            if (!iv.IsPoint)
            {
                foreach (var (edge, isOpen, inward) in new[]
                         {
                             (iv.A, iv.AOpen, +1.0),
                             (iv.B, iv.BOpen, -1.0),
                         })
                {
                    if (isOpen || double.IsInfinity(edge)) continue;

                    double y = Numeric.Safe(f.Eval, edge);
                    double inside = Numeric.Safe(f.Eval, edge + inward * 1e-5);
                    if (!Numeric.Finite(y) || !Numeric.Finite(inside)) continue;
                    if (Math.Abs(inside - y) < 1e-12) continue;

                    r.Extrema.Add(new NotablePoint(edge, y,
                        y > inside ? "endpoint maximum" : "endpoint minimum"));
                }
            }
        }
        r.Extrema.Sort((p, q) => p.X.CompareTo(q.X));

        // 8. second derivative
        r.Concavity.AddRange(DomainSolver.SignChart(fpp.Eval, r.Domain, Range));
        foreach (var iv in r.Domain)
        {
            double lo = double.IsInfinity(iv.A) ? -Range : iv.A;
            double hi = double.IsInfinity(iv.B) ? Range : iv.B;
            foreach (var c in Numeric.Roots(fpp.Eval, lo, hi, 4000))
            {
                if (!iv.Contains(c)) continue;
                double left = Numeric.Safe(fpp.Eval, c - 1e-4);
                double right = Numeric.Safe(fpp.Eval, c + 1e-4);
                if (left * right < 0)
                    r.Inflections.Add(new NotablePoint(Math.Round(c, 6), Numeric.Safe(f.Eval, c), "inflection point"));
            }
        }

        // 10. the largest and smallest values over the whole domain
        BuildAbsoluteExtremes(r, f);

        return r;
    }

    /// <summary>
    /// Weighs every value the function actually reaches — the turning points, the corners, the
    /// closed endpoints, any isolated point of the domain — against the values it only closes in
    /// on, which are the limits at open ends and at infinity. Whichever wins decides whether there
    /// is a genuine maximum, a bound that is never reached, or no bound at all.
    /// </summary>
    static void BuildAbsoluteExtremes(StudyReport r, Expr f)
    {
        var reached = new List<(double X, double Y)>();
        foreach (var p in r.Extrema)
        {
            // A point where monotonicity does not change is not a local extreme, so it cannot be
            // the global one either. Skipping them also keeps underflow out: exp(-x^2) evaluates
            // to exactly 0.0 past |x| ~ 27, which would otherwise look like an attained minimum.
            if (!p.Kind.Contains("maximum") && !p.Kind.Contains("minimum")) continue;
            if (Numeric.Finite(p.Y)) reached.Add((p.X, p.Y));
        }
        foreach (var iv in r.Domain.Where(i => i.IsPoint))
        {
            double y = Numeric.Safe(f.Eval, iv.A);
            if (Numeric.Finite(y)) reached.Add((iv.A, y));
        }

        // A constant function has no turning point, but it does reach its value — everywhere.
        if (r.Monotonicity.Count > 0 && r.Monotonicity.All(piece => piece.Sign == 0))
        {
            double mid = r.Domain[0].Mid;
            double y = Numeric.Safe(f.Eval, mid);
            if (Numeric.Finite(y)) reached.Add((mid, y));
        }

        var edges = new List<Limit>();
        foreach (var b in r.Limits)
        {
            if (b.HasLeft) edges.Add(b.FromLeft);
            if (b.HasRight) edges.Add(b.FromRight);
        }

        // At infinity a periodic function has no limit, but it also has nothing new to show:
        // the turning points already cover every value it takes.
        if (r.Period is null)
        {
            if (r.ReachesMinusInfinity) edges.Add(r.LimitAtMinusInfinity);
            if (r.ReachesPlusInfinity) edges.Add(r.LimitAtPlusInfinity);
        }

        r.AbsoluteMaximum = Decide(reached, edges, maximum: true);
        r.AbsoluteMinimum = Decide(reached, edges, maximum: false);
    }

    static Extreme Decide(List<(double X, double Y)> reached, List<Limit> edges, bool maximum)
    {
        var none = Array.Empty<double>();

        // an oscillating edge could hide anything, so claim nothing
        if (edges.Any(l => l.Kind == LimitKind.None))
            return new Extreme(ExtremeKind.Undetermined, double.NaN, none);

        var runaway = maximum ? LimitKind.PlusInfinity : LimitKind.MinusInfinity;
        if (edges.Any(l => l.Kind == runaway))
            return new Extreme(ExtremeKind.Unbounded,
                maximum ? double.PositiveInfinity : double.NegativeInfinity, none);

        int direction = maximum ? 1 : -1;
        bool Beats(double candidate, double incumbent) => candidate * direction > incumbent * direction;

        double? best = null;
        foreach (var (_, y) in reached)
            if (best is null || Beats(y, best.Value)) best = y;

        double? bound = null;
        foreach (var l in edges.Where(l => l.Kind == LimitKind.Finite))
            if (bound is null || Beats(l.Value, bound.Value)) bound = l.Value;

        if (best is null && bound is null)
            return new Extreme(ExtremeKind.Undetermined, double.NaN, none);

        // a tie goes to the value that is actually reached
        if (best is not null &&
            (bound is null || !Beats(bound.Value, best.Value + direction * 1e-9)))
        {
            double value = best.Value;
            double tolerance = 1e-7 * (1 + Math.Abs(value));
            var at = reached.Where(p => Math.Abs(p.Y - value) <= tolerance)
                            .Select(p => p.X)
                            .Distinct()
                            .OrderBy(x => x)
                            .ToArray();
            return new Extreme(ExtremeKind.Attained, value, at);
        }

        return new Extreme(ExtremeKind.Approached, bound!.Value, none);
    }

    static IEnumerable<double> EdgePoints(List<Interval> domain)
    {
        var set = new List<double>();
        foreach (var iv in domain)
        {
            if (!double.IsInfinity(iv.A)) Add(set, iv.A);
            if (!double.IsInfinity(iv.B)) Add(set, iv.B);
        }
        set.Sort();
        return set;

        static void Add(List<double> l, double v)
        {
            foreach (var e in l) if (Math.Abs(e - v) < 1e-9) return;
            l.Add(v);
        }
    }

    static void BuildAsymptotes(StudyReport r, Expr f)
    {
        foreach (var b in r.Limits)
        {
            bool blowsUp = (b.HasLeft && b.FromLeft.IsInfinite) || (b.HasRight && b.FromRight.IsInfinite);
            if (!blowsUp) continue;
            var parts = new List<string>();
            if (b.HasLeft) parts.Add($"lim x\u2192{Numeric.Pretty(b.X)}\u207B f(x) = {b.FromLeft}");
            if (b.HasRight) parts.Add($"lim x\u2192{Numeric.Pretty(b.X)}\u207A f(x) = {b.FromRight}");
            r.Asymptotes.Add(new Asymptote("vertical", $"x = {Numeric.Pretty(b.X)}", string.Join("; ", parts), b.X));
        }

        foreach (var (lim, sign, label) in new[]
                 {
                     (r.LimitAtPlusInfinity, +1, "+\u221E"),
                     (r.LimitAtMinusInfinity, -1, "\u2212\u221E")
                 })
        {
            bool reaches = sign > 0 ? r.ReachesPlusInfinity : r.ReachesMinusInfinity;
            if (!reaches) continue;

            if (lim.Kind == LimitKind.Finite)
            {
                r.Asymptotes.Add(new Asymptote("horizontal", $"y = {Numeric.Pretty(lim.Value)}",
                    $"lim x\u2192{label} f(x) = {lim}", 0, lim.Value));
                continue;
            }

            if (!lim.IsInfinite) continue;

            var m = Numeric.AtInfinity(x => f.Eval(x) / x, sign);
            if (m.Kind != LimitKind.Finite || Math.Abs(m.Value) < 1e-9) continue;
            double mv = m.Value;
            var q = Numeric.AtInfinity(x => f.Eval(x) - mv * x, sign);
            if (q.Kind != LimitKind.Finite) continue;

            string eq = $"y = {Numeric.Pretty(mv)}x" +
                        (Math.Abs(q.Value) < 1e-9 ? "" :
                         q.Value > 0 ? $" + {Numeric.Pretty(q.Value)}" : $" \u2212 {Numeric.Pretty(-q.Value)}");
            r.Asymptotes.Add(new Asymptote("oblique", eq,
                $"m = lim x\u2192{label} f(x)/x = {m}; q = lim x\u2192{label} [f(x) \u2212 mx] = {q}", mv, q.Value));
        }
    }

    static Symmetry DetectSymmetry(Expr f, List<Interval> domain)
    {
        bool even = true, odd = true, tested = false;
        for (double x = 0.37; x < 6; x += 0.53)
        {
            if (!domain.Any(i => i.Contains(x)) || !domain.Any(i => i.Contains(-x))) continue;
            double a = Numeric.Safe(f.Eval, x), b = Numeric.Safe(f.Eval, -x);
            if (!Numeric.Finite(a) || !Numeric.Finite(b)) continue;
            tested = true;
            double tol = 1e-7 * (1 + Math.Abs(a));
            if (Math.Abs(b - a) > tol) even = false;
            if (Math.Abs(b + a) > tol) odd = false;
        }
        if (!tested) return Symmetry.None;
        if (even) return Symmetry.Even;
        if (odd) return Symmetry.Odd;
        return Symmetry.None;
    }

    static double? DetectPeriod(Expr f, List<Interval> domain)
    {
        foreach (var cand in new[] { Math.PI / 2, Math.PI, 2 * Math.PI, 1, 2, 4 })
        {
            bool ok = true; int checks = 0;
            for (double x = -3.1; x < 3.1; x += 0.41)
            {
                double a = Numeric.Safe(f.Eval, x), b = Numeric.Safe(f.Eval, x + cand);
                if (!Numeric.Finite(a) || !Numeric.Finite(b)) continue;
                checks++;
                if (Math.Abs(a - b) > 1e-7 * (1 + Math.Abs(a))) { ok = false; break; }
            }
            if (ok && checks >= 8) return cand;
        }
        return null;
    }
}
