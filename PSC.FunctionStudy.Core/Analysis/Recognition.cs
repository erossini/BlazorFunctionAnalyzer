using PSC.FunctionStudy.MathCore;

namespace PSC.FunctionStudy.Analysis;

/// <summary>
/// What kind of function this is. <see cref="Family"/> and <see cref="Explanation"/> are always
/// present and come from walking the tree; <see cref="Name"/> and <see cref="Note"/> are filled in
/// only when the normalised form matches an entry in the catalogue.
/// </summary>
public sealed record Recognition(string Family, string Explanation, string? Name = null, string? Note = null)
{
    public bool IsNamed => Name is not null;
}

/// <summary>
/// Identifies a function two ways, both of them exact — no guessing.
/// 1. Structurally: polynomial (and its degree), rational, radical, exponential, and so on.
/// 2. By name: the printed form of the simplified expression is looked up in a small catalogue
///    of curves a syllabus actually names.
/// The catalogue is keyed on <c>Simplifier.Run(...).ToString()</c>, so the key for each entry is
/// computed from its source text rather than written out by hand — anything the user types that
/// normalises to the same tree matches, and the keys can never drift from the formatter.
/// </summary>
public static class Recogniser
{
    // ---------- named curves ----------

    static readonly (string Source, string Name, string Note)[] Catalogue =
    {
        ("1/x", "Hyperbola",
            "The reciprocal function. Its two branches never meet, and the axes are its asymptotes."),
        ("x^2", "Parabola",
            "The simplest even function: one minimum, at the origin, and symmetric about the y-axis."),
        ("x^3", "Cubic parabola",
            "Odd and strictly increasing, with an inflection point at the origin."),
        ("sqrt(x)", "Square-root curve",
            "Half a parabola lying on its side. Defined only for x ≥ 0, with a vertical tangent at the origin."),
        ("cbrt(x)", "Cube-root curve",
            "Odd and defined for every real x, with a vertical tangent at the origin."),
        ("abs(x)", "Absolute value",
            "Two half-lines meeting in a corner at the origin, where the derivative does not exist."),
        ("exp(x)", "Natural exponential",
            "Equal to its own derivative — up to a constant factor, the only function with that property."),
        ("exp(-x)", "Exponential decay",
            "Falls by the same factor over equal steps in x. The x-axis is a horizontal asymptote."),
        ("ln(x)", "Natural logarithm",
            "The inverse of exp(x). Defined only for x > 0, and grows without bound but slower than any positive power of x."),
        ("sin(x)", "Sine wave",
            "The archetypal periodic function: period 2π, odd, and bounded between −1 and 1."),
        ("cos(x)", "Cosine wave",
            "The sine wave shifted by π/2: period 2π, even, and bounded between −1 and 1."),
        ("tan(x)", "Tangent curve",
            "Period π, with a vertical asymptote wherever cos x = 0."),
        ("sin(x)/x", "Sinc function",
            "Has a removable discontinuity at 0 — the limit there is 1, which is why the graph looks unbroken."),
        ("exp(-x^2)", "Gaussian (bell curve)",
            "The shape behind the normal distribution: even, one maximum at the origin, the x-axis as an asymptote."),
        ("1/(1+x^2)", "Witch of Agnesi",
            "Even and bounded between 0 and 1. The area under it gives arctan, and it is the Cauchy distribution up to a factor of π."),
        ("1/(1+exp(-x))", "Logistic (sigmoid) curve",
            "S-shaped, squeezing the whole real line into (0, 1). Used for growth models and in machine learning."),
        ("sqrt(1-x^2)", "Upper unit semicircle",
            "The top half of the circle x² + y² = 1, so the domain is exactly [−1, 1]."),
        ("cosh(x)", "Catenary",
            "The curve a chain takes when it hangs under its own weight."),
    };

    static readonly Dictionary<string, (string Name, string Note)> ByForm = BuildIndex();

    static Dictionary<string, (string, string)> BuildIndex()
    {
        var index = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var (source, name, note) in Catalogue)
            index[Key(Simplifier.Run(Parser.Parse(source)))] = (name, note);
        return index;
    }

    static string Key(Expr e) => e.ToString();

    /// <summary>Classifies <paramref name="f"/>, which is expected to be already simplified.</summary>
    public static Recognition Describe(Expr f)
    {
        var (family, explanation) = Classify(f);
        return ByForm.TryGetValue(Key(f), out var hit)
            ? new Recognition(family, explanation, hit.Name, hit.Note)
            : new Recognition(family, explanation);
    }

    // ---------- structure ----------

    static (string Family, string Explanation) Classify(Expr f)
    {
        if (Degree(f) is int d)
            return d switch
            {
                0 => ("Constant function", "The value never changes, so the graph is a horizontal line."),
                1 => ("Linear function", "A straight line: constant slope, no turning points, exactly one root unless the slope is zero."),
                2 => ("Quadratic function", "A parabola: one turning point, at most two real roots, and no inflection."),
                3 => ("Cubic function", "At most two turning points, at most three real roots, and exactly one inflection point."),
                4 => ("Quartic function", "At most three turning points and at most four real roots."),
                _ => ($"Polynomial of degree {d}",
                      $"At most {d - 1} turning points and at most {d} real roots. Defined for every real x."),
            };

        if (f is Div div && Degree(div.L) is int n && Degree(div.R) is int m && m > 0)
            return ("Rational function", RationalNote(n, m));

        return FromFeatures(Features(f));
    }

    /// <summary>
    /// The degrees of a rational function already tell you which asymptote to expect,
    /// before any limit is computed. Step 07 then confirms it.
    /// </summary>
    static string RationalNote(int numerator, int denominator)
    {
        var lead = $"A ratio of two polynomials, of degree {numerator} over degree {denominator}. ";
        if (numerator < denominator)
            return lead + "The numerator has the lower degree, so expect the horizontal asymptote y = 0.";
        if (numerator == denominator)
            return lead + "The degrees match, so expect a horizontal asymptote at the ratio of the leading coefficients.";
        if (numerator == denominator + 1)
            return lead + "The numerator's degree is one higher, so expect an oblique asymptote.";
        return lead + "The numerator's degree exceeds the denominator's by more than one, so there is no horizontal or oblique asymptote.";
    }

    /// <summary>Degree as a polynomial in x, or null when the expression is not a polynomial.</summary>
    static int? Degree(Expr e) => e switch
    {
        Num => 0,
        Var => 1,
        Add a => Both(Degree(a.L), Degree(a.R), Math.Max),
        Sub s => Both(Degree(s.L), Degree(s.R), Math.Max),
        Mul m => Both(Degree(m.L), Degree(m.R), (x, y) => x + y),
        Neg n => Degree(n.O),
        // dividing by a constant keeps it a polynomial; dividing by anything else does not
        Div d => Degree(d.R) == 0 ? Degree(d.L) : null,
        Pow p when p.E is Num e2 && IsWholeNumber(e2.V) && e2.V >= 0 =>
            Degree(p.B) is int b ? b * (int)Math.Round(e2.V) : null,
        _ => null,
    };

    static int? Both(int? a, int? b, Func<int, int, int> combine) =>
        a is int x && b is int y ? combine(x, y) : null;

    static bool IsWholeNumber(double v) => Math.Abs(v - Math.Round(v)) < 1e-12;

    // ---------- transcendental features ----------

    [Flags]
    enum Feature
    {
        None = 0,
        Trigonometric = 1,
        InverseTrigonometric = 2,
        Hyperbolic = 4,
        Exponential = 8,
        Logarithmic = 16,
        Radical = 32,
        AbsoluteValue = 64,
    }

    static readonly (Feature Flag, string Name, string Expect)[] FeatureNotes =
    {
        (Feature.Trigonometric, "trigonometric", "periodic behaviour"),
        (Feature.InverseTrigonometric, "inverse trigonometric", "a bounded domain and bounded values"),
        (Feature.Hyperbolic, "hyperbolic", "exponential growth in both directions"),
        (Feature.Exponential, "exponential", "growth or decay that outruns any power of x"),
        (Feature.Logarithmic, "logarithmic", "a domain restricted to where the argument stays positive"),
        (Feature.Radical, "radical", "a domain restricted by the even roots"),
        (Feature.AbsoluteValue, "absolute-value", "a corner wherever the inside changes sign"),
    };

    static (string Family, string Explanation) FromFeatures(Feature features)
    {
        var present = FeatureNotes.Where(p => features.HasFlag(p.Flag)).ToList();

        if (present.Count == 0)
            return ("Algebraic function", "Built from the four arithmetic operations and powers of x.");

        if (present.Count == 1)
            return (Capitalise(present[0].Name) + " function", $"Expect {present[0].Expect}.");

        var names = string.Join(", ", present.Take(present.Count - 1).Select(p => p.Name))
                    + " and " + present[^1].Name;
        var expectations = string.Join("; ", present.Select(p => p.Expect));
        return ("Composite function", $"Combines {names} parts, so expect {expectations}.");
    }

    static Feature Features(Expr e)
    {
        var found = Feature.None;
        Collect(e, ref found);
        return found;
    }

    static void Collect(Expr e, ref Feature acc)
    {
        switch (e)
        {
            case Call c:
                acc |= c.Name switch
                {
                    "sin" or "cos" or "tan" or "cot" or "sec" or "csc" => Feature.Trigonometric,
                    "asin" or "acos" or "atan" => Feature.InverseTrigonometric,
                    "sinh" or "cosh" or "tanh" => Feature.Hyperbolic,
                    "exp" => Feature.Exponential,
                    "ln" or "log" or "log2" => Feature.Logarithmic,
                    "sqrt" or "cbrt" => Feature.Radical,
                    "abs" => Feature.AbsoluteValue,
                    _ => Feature.None,
                };
                Collect(c.A, ref acc);
                break;

            case Pow p:
                // x in the exponent makes it exponential; a fractional exponent is a root.
                // Evaluate rather than pattern-match on Num: the simplifier leaves 1/3 as a
                // division, since folding it would lose exactness.
                if (ContainsVariable(p.E))
                {
                    acc |= Feature.Exponential;
                }
                else
                {
                    double exponent = Numeric.Safe(p.E.Eval, 0);
                    if (double.IsFinite(exponent) && !IsWholeNumber(exponent)) acc |= Feature.Radical;
                }
                Collect(p.B, ref acc);
                Collect(p.E, ref acc);
                break;

            case Add a: Collect(a.L, ref acc); Collect(a.R, ref acc); break;
            case Sub s: Collect(s.L, ref acc); Collect(s.R, ref acc); break;
            case Mul m: Collect(m.L, ref acc); Collect(m.R, ref acc); break;
            case Div d: Collect(d.L, ref acc); Collect(d.R, ref acc); break;
            case Neg g: Collect(g.O, ref acc); break;
        }
    }

    static bool ContainsVariable(Expr e) => e switch
    {
        Var => true,
        Add a => ContainsVariable(a.L) || ContainsVariable(a.R),
        Sub s => ContainsVariable(s.L) || ContainsVariable(s.R),
        Mul m => ContainsVariable(m.L) || ContainsVariable(m.R),
        Div d => ContainsVariable(d.L) || ContainsVariable(d.R),
        Pow p => ContainsVariable(p.B) || ContainsVariable(p.E),
        Neg n => ContainsVariable(n.O),
        Call c => ContainsVariable(c.A),
        _ => false,
    };

    static string Capitalise(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}
