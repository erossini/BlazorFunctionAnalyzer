using System.Globalization;

namespace PSC.FunctionStudy.Analysis;

public enum LimitKind { Finite, PlusInfinity, MinusInfinity, None }

public readonly record struct Limit(LimitKind Kind, double Value)
{
    public static readonly Limit DNE = new(LimitKind.None, double.NaN);
    public bool IsInfinite => Kind is LimitKind.PlusInfinity or LimitKind.MinusInfinity;

    public override string ToString() => Kind switch
    {
        LimitKind.Finite => Numeric.Pretty(Value),
        LimitKind.PlusInfinity => "+\u221E",
        LimitKind.MinusInfinity => "\u2212\u221E",
        _ => "does not exist"
    };
}

/// <summary>
/// All the numeric machinery. The engine stays symbolic for derivatives and
/// goes numeric for roots, limits and sign, which is far more robust across
/// the mix of function families a student will actually type in.
/// </summary>
public static class Numeric
{
    public static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    public static double Safe(Func<double, double> f, double x)
    {
        try { return f(x); } catch { return double.NaN; }
    }

    public static string Pretty(double v)
    {
        if (double.IsPositiveInfinity(v)) return "+\u221E";
        if (double.IsNegativeInfinity(v)) return "\u2212\u221E";
        if (double.IsNaN(v)) return "undefined";
        double r = Math.Round(v);
        if (Math.Abs(v - r) < 1e-9) v = r == 0 ? 0 : r;
        var s = Math.Abs(v) < 1e-4 && v != 0
            ? v.ToString("0.####e+0", CultureInfo.InvariantCulture)
            : v.ToString("0.####", CultureInfo.InvariantCulture);
        return s.Replace("-", "\u2212");
    }

    // ---------- roots ----------

    /// <summary>Finds real zeros of f on [a,b]: sign changes plus tangent (double) roots.</summary>
    public static List<double> Roots(Func<double, double> f, double a, double b, int samples = 4000)
    {
        var roots = new List<double>();
        if (b <= a) return roots;

        double step = (b - a) / samples;
        double xPrev = a, fPrev = Safe(f, a);

        for (int i = 1; i <= samples; i++)
        {
            double x = a + i * step;
            double fx = Safe(f, x);

            if (Finite(fPrev) && Finite(fx))
            {
                if (fPrev == 0)
                {
                    // A stretch of exact zeros is one flat piece, not one root per sample.
                    // It usually means underflow rather than mathematics — exp(-x^2)'s derivative
                    // is exactly 0.0 in double precision beyond |x| ≈ 27, which would otherwise
                    // report a thousand-odd stationary points. Record only where a run meets a
                    // non-zero value.
                    if (fx != 0 || Safe(f, xPrev - step) != 0) Push(roots, xPrev);
                }
                else if (fPrev * fx < 0) Push(roots, Bisect(f, xPrev, x));
                else if (Math.Abs(fx) < 1e-6)
                {
                    // possible tangent root: |f| has a local minimum near zero
                    double fl = Safe(f, x - step), fr = Safe(f, x + step);
                    if (Finite(fl) && Finite(fr) &&
                        Math.Abs(fx) <= Math.Abs(fl) && Math.Abs(fx) <= Math.Abs(fr))
                        Push(roots, MinimizeAbs(f, x - step, x + step));
                }
            }
            xPrev = x; fPrev = fx;
        }
        roots.Sort();
        return roots;
    }

    static void Push(List<double> list, double r)
    {
        if (!Finite(r)) return;
        double snapped = Math.Round(r, 9);
        if (Math.Abs(snapped - Math.Round(snapped)) < 5e-8) snapped = Math.Round(snapped);
        foreach (var e in list) if (Math.Abs(e - snapped) < 1e-6) return;
        list.Add(snapped);
    }

    static double Bisect(Func<double, double> f, double a, double b)
    {
        double fa = Safe(f, a);
        for (int i = 0; i < 200; i++)
        {
            double m = 0.5 * (a + b), fm = Safe(f, m);
            if (!Finite(fm)) return m;
            if (fm == 0 || (b - a) < 1e-14) return m;
            if (fa * fm < 0) b = m; else { a = m; fa = fm; }
        }
        return 0.5 * (a + b);
    }

    /// <summary>
    /// Brackets where f changes sign between consecutive finite samples. Unlike <see cref="Roots"/>
    /// this does not require f to pass through zero, so it also catches a jump — which is exactly
    /// what the derivative of |x| does at the origin. Non-finite samples are stepped over, so a
    /// bracket may span them.
    /// </summary>
    public static List<(double Lo, double Hi)> SignChanges(Func<double, double> f, double a, double b, int samples = 3000)
    {
        var brackets = new List<(double, double)>();
        if (b <= a) return brackets;

        double step = (b - a) / samples;
        double lastX = double.NaN, lastF = double.NaN;

        for (int i = 0; i <= samples; i++)
        {
            double x = a + i * step;
            double fx = Safe(f, x);
            if (!Finite(fx) || fx == 0) continue;   // an exact zero is Roots' business

            if (Finite(lastF) && lastF * fx < 0) brackets.Add((lastX, x));
            lastX = x; lastF = fx;
        }
        return brackets;
    }

    /// <summary>
    /// Locates a turning point of f in [a, b] by ternary search. It never differentiates, so it
    /// works at a corner, where there is no derivative to find a root of.
    /// </summary>
    public static double Turn(Func<double, double> f, double a, double b, bool minimum)
    {
        for (int i = 0; i < 200; i++)
        {
            double m1 = a + (b - a) / 3, m2 = b - (b - a) / 3;
            double f1 = Safe(f, m1), f2 = Safe(f, m2);
            if (!Finite(f1) || !Finite(f2)) break;
            if (minimum ? f1 < f2 : f1 > f2) b = m2; else a = m1;
        }
        return 0.5 * (a + b);
    }

    static double MinimizeAbs(Func<double, double> f, double a, double b)
    {
        for (int i = 0; i < 200; i++)
        {
            double m1 = a + (b - a) / 3, m2 = b - (b - a) / 3;
            if (Math.Abs(Safe(f, m1)) < Math.Abs(Safe(f, m2))) b = m2; else a = m1;
        }
        return 0.5 * (a + b);
    }

    // ---------- limits ----------

    /// <summary>Limit of f as x approaches x0. side = -1 from the left, +1 from the right.</summary>
    public static Limit At(Func<double, double> f, double x0, int side)
    {
        var vals = new List<double>();
        for (int k = 2; k <= 11; k++)
        {
            double h = Math.Pow(10, -k);
            double v = Safe(f, x0 + side * h);
            vals.Add(v);
        }
        return Classify(vals);
    }

    /// <summary>Limit of f as x approaches +infinity (sign = +1) or -infinity (sign = -1).</summary>
    public static Limit AtInfinity(Func<double, double> f, int sign)
    {
        var vals = new List<double>();
        for (int k = 1; k <= 9; k++)
        {
            double v = Safe(f, sign * Math.Pow(10, k));
            vals.Add(v);
        }
        return Classify(vals);
    }

    static Limit Classify(List<double> raw)
    {
        var tail = raw.Skip(Math.Max(0, raw.Count - 5)).ToList();
        if (tail.Count == 0 || tail.Any(double.IsNaN)) return Limit.DNE;

        // overflowed all the way to infinity: the sign is the answer
        if (double.IsInfinity(tail[^1]))
            return tail[^1] > 0
                ? new Limit(LimitKind.PlusInfinity, double.PositiveInfinity)
                : new Limit(LimitKind.MinusInfinity, double.NegativeInfinity);

        var finite = tail.Where(Finite).ToList();
        if (finite.Count < 3) return Limit.DNE;

        var pts = finite.Skip(Math.Max(0, finite.Count - 4)).ToList();
        var deltas = new List<double>();
        for (int i = 0; i + 1 < pts.Count; i++) deltas.Add(pts[i + 1] - pts[i]);

        double v3 = pts[^1], d1 = deltas[^2], d2 = deltas[^1];
        double scale = 1 + Math.Abs(v3);

        // settled on a value
        if (Math.Abs(d2) < 1e-7 * scale) return new Limit(LimitKind.Finite, Snap(v3));

        // still moving but the steps are shrinking: accelerate and accept
        if (Math.Abs(d2) < Math.Abs(d1) && Math.Abs(d2) < 1e-2 * scale)
        {
            double denom = d2 - d1;
            double acc = Math.Abs(denom) > 1e-300 ? v3 - d2 * d2 / denom : v3;
            if (Finite(acc) && Math.Abs(acc - v3) < 1e-1 * scale)
                return new Limit(LimitKind.Finite, Snap(acc));
        }

        // steps keep the same direction and refuse to shrink: it runs away.
        // This is what catches slow blow-ups such as ln(x) or ln(x^2 - 4) near 2.
        bool sameDirection = deltas.All(d => d > 0) || deltas.All(d => d < 0);
        if (sameDirection && Math.Abs(deltas[^1]) >= 0.5 * Math.Abs(deltas[0]))
            return deltas[^1] > 0
                ? new Limit(LimitKind.PlusInfinity, double.PositiveInfinity)
                : new Limit(LimitKind.MinusInfinity, double.NegativeInfinity);

        return Limit.DNE;
    }

    static double Snap(double v)
    {
        double r = Math.Round(v);
        if (Math.Abs(v - r) < 1e-7) return r == 0 ? 0 : r;
        foreach (var c in new[] { Math.PI, Math.E, Math.PI / 2, 0.5, 1.0 / 3 })
            if (Math.Abs(Math.Abs(v) - c) < 1e-8) return Math.Sign(v) * c;
        return v;
    }
}
