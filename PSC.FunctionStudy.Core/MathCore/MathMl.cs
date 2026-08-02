using System.Text;

namespace PSC.FunctionStudy.MathCore;

/// <summary>
/// Renders an expression as MathML Core, so the browser stacks fractions, raises powers and
/// draws radicals the way they are written by hand. Deliberately not LaTeX-plus-a-JS-library:
/// this is markup the page renders on its own, with no script, no font bundle and no interop,
/// which keeps the UI layer portable to a WebView shell.
///
/// Precedence mirrors <see cref="Expr.Format"/> with one difference: where a MathML element
/// already groups its parts visually — a fraction bar, a radical sign — the parentheses the
/// flat form needs are dropped, which is the whole point of typesetting it.
///
/// The output is assembled from a closed alphabet (digits, x, and the names in
/// <see cref="Call.Known"/>), because the parser rejects everything else. Nothing a user types
/// can reach the page as markup.
/// </summary>
public static class MathMl
{
    // 1 = +/-, 2 = * /, 3 = unary minus, 4 = ^, 5 = must be an atom (a power's base)
    public static string Render(Expr e, bool block = false) =>
        $"<math{(block ? " display=\"block\"" : "")}>{Node(e, 0)}</math>";

    static string Node(Expr e, int parent) => e switch
    {
        Num n => Wrap(Number(n.V), n.V < 0 && parent >= 2),

        Var => "<mi>x</mi>",

        Add a => Wrap(Row(Node(a.L, 1), Op("+"), Node(a.R, 1)), parent > 1),

        Sub s => Wrap(Row(Node(s.L, 1), Op("−"), Node(s.R, 2)), parent > 1),

        Mul m => Wrap(Row(Node(m.L, 2), Op("·"), Node(m.R, 2)), parent > 2),

        // the bar groups numerator and denominator, so neither needs parentheses;
        // only a power taking the whole fraction as its base does
        Div d => Wrap($"<mfrac>{Node(d.L, 0)}{Node(d.R, 0)}</mfrac>", parent >= 5),

        Neg g => Wrap(Row(Op("−"), Node(g.O, 3)), parent >= 3),

        // the exponent is raised, so it needs no parentheses of its own
        Pow p => Wrap($"<msup>{Node(p.B, 5)}{Node(p.E, 0)}</msup>", parent > 4),

        Call c => Function(c, parent),

        _ => "<mi>?</mi>",
    };

    static string Function(Call c, int parent) => c.Name switch
    {
        "sqrt" => $"<msqrt>{Node(c.A, 0)}</msqrt>",
        "cbrt" => $"<mroot>{Node(c.A, 0)}<mn>3</mn></mroot>",
        "abs" => Row(Op("|"), Node(c.A, 0), Op("|")),
        // exp(u) is written e^u once it can be typeset
        "exp" => Wrap($"<msup><mi>e</mi>{Node(c.A, 0)}</msup>", parent > 4),
        _ => Row($"<mi>{c.Name}</mi>", ApplyFunction, Op("("), Node(c.A, 0), Op(")")),
    };

    /// <summary>U+2061, the invisible operator that marks a function applied to an argument.</summary>
    const string ApplyFunction = "<mo>⁡</mo>";

    static string Op(string symbol) => $"<mo>{symbol}</mo>";

    static string Row(params string[] parts)
    {
        var sb = new StringBuilder("<mrow>");
        foreach (var part in parts) sb.Append(part);
        return sb.Append("</mrow>").ToString();
    }

    static string Wrap(string inner, bool parenthesise) =>
        parenthesise ? Row(Op("("), inner, Op(")")) : inner;

    static string Number(double v) =>
        $"<mn>{Expr.Number(v).Replace('-', '−')}</mn>";
}
