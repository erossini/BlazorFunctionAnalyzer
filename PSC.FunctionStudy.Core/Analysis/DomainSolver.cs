using PSC.FunctionStudy.MathCore;

namespace PSC.FunctionStudy.Analysis;

public enum Rel { NonZero, Positive, NonNegative, AbsAtMostOne }

/// <summary>One existence condition read off the expression tree, e.g. "x - 1 &#8800; 0".</summary>
public sealed record Constraint(Expr G, Rel Relation)
{
    public string Text => Relation switch
    {
        Rel.NonZero => $"{G} \u2260 0",
        Rel.Positive => $"{G} > 0",
        Rel.NonNegative => $"{G} \u2265 0",
        _ => $"\u22121 \u2264 {G} \u2264 1"
    };

    public bool Holds(double x)
    {
        double v = Numeric.Safe(G.Eval, x);
        if (!Numeric.Finite(v)) return false;
        return Relation switch
        {
            Rel.NonZero => Math.Abs(v) > 1e-9,
            Rel.Positive => v > 1e-12,
            Rel.NonNegative => v >= -1e-12,
            _ => Math.Abs(v) <= 1 + 1e-12
        };
    }

    /// <summary>Expressions whose zeros are candidate boundaries of the domain.</summary>
    public IEnumerable<Func<double, double>> Boundaries()
    {
        yield return G.Eval;
        if (Relation == Rel.AbsAtMostOne)
        {
            yield return x => G.Eval(x) - 1;
            yield return x => G.Eval(x) + 1;
        }
    }
}

/// <summary>A maximal piece of the domain. Infinite ends use ±infinity.</summary>
public sealed record Interval(double A, double B, bool AOpen, bool BOpen)
{
    public bool IsPoint => A == B;
    public double Mid => double.IsNegativeInfinity(A) && double.IsPositiveInfinity(B) ? 0
        : double.IsNegativeInfinity(A) ? B - 1
        : double.IsPositiveInfinity(B) ? A + 1
        : 0.5 * (A + B);

    public bool Contains(double x) =>
        (x > A || (x == A && !AOpen)) && (x < B || (x == B && !BOpen));

    public override string ToString()
    {
        if (IsPoint) return "{" + Numeric.Pretty(A) + "}";
        string l = double.IsNegativeInfinity(A) ? "(\u2212\u221E" : (AOpen ? "(" : "[") + Numeric.Pretty(A);
        string r = double.IsPositiveInfinity(B) ? "+\u221E)" : Numeric.Pretty(B) + (BOpen ? ")" : "]");
        return l + ", " + r;
    }
}

public static class DomainSolver
{
    /// <summary>Walks the tree and records every existence condition it implies.</summary>
    public static List<Constraint> CollectConstraints(Expr e)
    {
        var list = new List<Constraint>();
        Walk(e, list);
        // de-duplicate by printed form
        return list.GroupBy(c => c.Text).Select(g => g.First()).ToList();
    }

    static void Walk(Expr e, List<Constraint> acc)
    {
        switch (e)
        {
            case Div d:
                acc.Add(new Constraint(Simplifier.Run(d.R), Rel.NonZero));
                Walk(d.L, acc); Walk(d.R, acc); break;

            case Call c:
                switch (c.Name)
                {
                    case "sqrt":
                        acc.Add(new Constraint(Simplifier.Run(c.A), Rel.NonNegative)); break;
                    case "ln": case "log": case "log2":
                        acc.Add(new Constraint(Simplifier.Run(c.A), Rel.Positive)); break;
                    case "tan": case "sec":
                        acc.Add(new Constraint(new Call("cos", c.A), Rel.NonZero)); break;
                    case "cot": case "csc":
                        acc.Add(new Constraint(new Call("sin", c.A), Rel.NonZero)); break;
                    case "asin": case "acos":
                        acc.Add(new Constraint(Simplifier.Run(c.A), Rel.AbsAtMostOne)); break;
                }
                Walk(c.A, acc); break;

            case Pow p:
                if (p.E is Num n)
                {
                    bool integer = Math.Abs(n.V - Math.Round(n.V)) < 1e-12;
                    if (!integer)
                    {
                        double inv = 1.0 / n.V;
                        bool oddRoot = Math.Abs(inv - Math.Round(inv)) < 1e-9 && Math.Round(inv) % 2 != 0;
                        if (!oddRoot)
                            acc.Add(new Constraint(Simplifier.Run(p.B),
                                n.V < 0 ? Rel.Positive : Rel.NonNegative));
                    }
                    else if (n.V < 0)
                        acc.Add(new Constraint(Simplifier.Run(p.B), Rel.NonZero));
                }
                else
                {
                    acc.Add(new Constraint(Simplifier.Run(p.B), Rel.Positive));
                }
                Walk(p.B, acc); Walk(p.E, acc); break;

            case Add a: Walk(a.L, acc); Walk(a.R, acc); break;
            case Sub s: Walk(s.L, acc); Walk(s.R, acc); break;
            case Mul m: Walk(m.L, acc); Walk(m.R, acc); break;
            case Neg g: Walk(g.O, acc); break;
        }
    }

    /// <summary>
    /// Splits the real line at every candidate boundary, then keeps the pieces where
    /// every condition holds. Works for any combination of constraints without needing
    /// to solve inequalities symbolically.
    /// </summary>
    public static List<Interval> Solve(Expr f, List<Constraint> constraints, double range = 40)
    {
        var breaks = new List<double>();
        foreach (var c in constraints)
            foreach (var g in c.Boundaries())
                foreach (var r in Numeric.Roots(g, -range, range, 6000))
                    Insert(breaks, r);
        breaks.Sort();

        bool Ok(double x) => Numeric.Finite(Numeric.Safe(f.Eval, x)) && constraints.All(c => c.Holds(x));

        int n = breaks.Count;
        var segOk = new bool[n + 1];
        for (int i = 0; i <= n; i++)
        {
            double lo = i == 0 ? double.NegativeInfinity : breaks[i - 1];
            double hi = i == n ? double.PositiveInfinity : breaks[i];
            double m = double.IsInfinity(lo) && double.IsInfinity(hi) ? 0
                : double.IsInfinity(lo) ? hi - 1
                : double.IsInfinity(hi) ? lo + 1
                : 0.5 * (lo + hi);
            // sample three points so a thin sliver is not misjudged
            double a = double.IsInfinity(lo) ? m - 0.3 : lo + (m - lo) * 0.5;
            double b = double.IsInfinity(hi) ? m + 0.3 : hi - (hi - m) * 0.5;
            segOk[i] = Ok(m) || (Ok(a) && Ok(b));
        }

        var ptOk = new bool[n];
        for (int i = 0; i < n; i++) ptOk[i] = Ok(breaks[i]);

        var result = new List<Interval>();
        int seg = 0;
        while (seg <= n)
        {
            if (!segOk[seg])
            {
                if (seg < n && ptOk[seg] && (seg + 1 > n || !segOk[seg + 1]))
                    result.Add(new Interval(breaks[seg], breaks[seg], false, false));
                seg++;
                continue;
            }

            int end = seg;
            while (end < n && ptOk[end] && segOk[end + 1]) end++;

            double left = seg == 0 ? double.NegativeInfinity : breaks[seg - 1];
            bool leftOpen = seg == 0 || !ptOk[seg - 1];
            double right = end == n ? double.PositiveInfinity : breaks[end];
            bool rightOpen = end == n || !ptOk[end];

            result.Add(new Interval(left, right, leftOpen, rightOpen));
            seg = end + 1;
        }

        if (result.Count == 0 && n == 0 && segOk[0])
            result.Add(new Interval(double.NegativeInfinity, double.PositiveInfinity, true, true));

        return result;
    }

    static void Insert(List<double> list, double v)
    {
        double s = Math.Abs(v - Math.Round(v)) < 5e-8 ? Math.Round(v) : Math.Round(v, 9);
        foreach (var e in list) if (Math.Abs(e - s) < 1e-6) return;
        list.Add(s);
    }

    // ---------- sign charts ----------

    public sealed record SignPiece(Interval Where, int Sign);

    /// <summary>Sign of g on each piece of the domain: +1, -1, or 0 on a whole stretch.</summary>
    public static List<SignPiece> SignChart(Func<double, double> g, List<Interval> domain, double range = 40)
    {
        var pieces = new List<SignPiece>();

        foreach (var iv in domain)
        {
            if (iv.IsPoint) continue;
            double lo = Math.Max(iv.A, -range), hi = Math.Min(iv.B, range);
            if (double.IsNegativeInfinity(iv.A)) lo = -range;
            if (double.IsPositiveInfinity(iv.B)) hi = range;
            if (hi <= lo) continue;

            var cuts = Numeric.Roots(g, lo + 1e-9, hi - 1e-9, 3000)
                              .Where(r => r > lo + 1e-9 && r < hi - 1e-9).ToList();

            var bounds = new List<double> { iv.A }; bounds.AddRange(cuts); bounds.Add(iv.B);

            for (int i = 0; i + 1 < bounds.Count; i++)
            {
                double a = bounds[i], b = bounds[i + 1];
                var piece = new Interval(a, b, true, true);
                double v = Numeric.Safe(g, piece.Mid);
                if (!Numeric.Finite(v)) continue;
                pieces.Add(new SignPiece(piece, Math.Abs(v) < 1e-12 ? 0 : Math.Sign(v)));
            }
        }

        // merge neighbours that carry the same sign
        var merged = new List<SignPiece>();
        foreach (var p in pieces)
        {
            if (merged.Count > 0 && merged[^1].Sign == p.Sign &&
                Math.Abs(merged[^1].Where.B - p.Where.A) < 1e-9)
            {
                var prev = merged[^1];
                merged[^1] = prev with { Where = prev.Where with { B = p.Where.B, BOpen = p.Where.BOpen } };
            }
            else merged.Add(p);
        }
        return merged;
    }
}
