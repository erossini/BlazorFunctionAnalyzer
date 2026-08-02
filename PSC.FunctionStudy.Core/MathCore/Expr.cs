using System.Globalization;

namespace PSC.FunctionStudy.MathCore;

/// <summary>
/// Expression tree for a single-variable function f(x).
/// Every node can evaluate itself, differentiate itself and print itself.
/// </summary>
public abstract class Expr
{
    // Precedence levels used by Format: 1 = +/-, 2 = * /, 3 = unary minus, 4 = ^, 5 = atom
    public abstract double Eval(double x);
    public abstract Expr Derivative();
    public abstract string Format(int parentPrecedence);

    public override string ToString() => Format(0);

    public static readonly Num Zero = new(0);
    public static readonly Num One = new(1);

    public Func<double, double> AsFunc() => Eval;

    internal static string Number(double v)
    {
        if (double.IsPositiveInfinity(v)) return "+\u221E";
        if (double.IsNegativeInfinity(v)) return "\u2212\u221E";
        if (Math.Abs(v - Math.Round(v)) < 1e-12 && Math.Abs(v) < 1e15)
            return Math.Round(v).ToString(CultureInfo.InvariantCulture);
        return v.ToString("0.######", CultureInfo.InvariantCulture);
    }
}

public sealed class Num : Expr
{
    public readonly double V;
    public Num(double v) { V = v; }

    public override double Eval(double x) => V;
    public override Expr Derivative() => Zero;
    public override string Format(int p)
    {
        var s = Number(V);
        return V < 0 && p >= 2 ? "(" + s + ")" : s;
    }
}

public sealed class Var : Expr
{
    public static readonly Var X = new();
    public override double Eval(double x) => x;
    public override Expr Derivative() => One;
    public override string Format(int p) => "x";
}

public sealed class Add : Expr
{
    public readonly Expr L, R;
    public Add(Expr l, Expr r) { L = l; R = r; }
    public override double Eval(double x) => L.Eval(x) + R.Eval(x);
    public override Expr Derivative() => new Add(L.Derivative(), R.Derivative());
    public override string Format(int p)
    {
        var s = L.Format(1) + " + " + R.Format(1);
        return p > 1 ? "(" + s + ")" : s;
    }
}

public sealed class Sub : Expr
{
    public readonly Expr L, R;
    public Sub(Expr l, Expr r) { L = l; R = r; }
    public override double Eval(double x) => L.Eval(x) - R.Eval(x);
    public override Expr Derivative() => new Sub(L.Derivative(), R.Derivative());
    public override string Format(int p)
    {
        var s = L.Format(1) + " \u2212 " + R.Format(2);
        return p > 1 ? "(" + s + ")" : s;
    }
}

public sealed class Mul : Expr
{
    public readonly Expr L, R;
    public Mul(Expr l, Expr r) { L = l; R = r; }
    public override double Eval(double x) => L.Eval(x) * R.Eval(x);
    // product rule
    public override Expr Derivative() =>
        new Add(new Mul(L.Derivative(), R), new Mul(L, R.Derivative()));
    public override string Format(int p)
    {
        var s = L.Format(2) + "\u00B7" + R.Format(2);
        return p > 2 ? "(" + s + ")" : s;
    }
}

public sealed class Div : Expr
{
    public readonly Expr L, R;
    public Div(Expr l, Expr r) { L = l; R = r; }
    public override double Eval(double x) => L.Eval(x) / R.Eval(x);
    // quotient rule
    public override Expr Derivative() =>
        new Div(new Sub(new Mul(L.Derivative(), R), new Mul(L, R.Derivative())),
                new Pow(R, new Num(2)));
    public override string Format(int p)
    {
        var s = L.Format(2) + "/" + R.Format(3);
        return p > 2 ? "(" + s + ")" : s;
    }
}

public sealed class Neg : Expr
{
    public readonly Expr O;
    public Neg(Expr o) { O = o; }
    public override double Eval(double x) => -O.Eval(x);
    public override Expr Derivative() => new Neg(O.Derivative());
    public override string Format(int p)
    {
        var s = "\u2212" + O.Format(3);
        return p >= 3 ? "(" + s + ")" : s;
    }
}

public sealed class Pow : Expr
{
    public readonly Expr B, E;
    public Pow(Expr b, Expr e) { B = b; E = e; }

    public override double Eval(double x)
    {
        double b = B.Eval(x), e = E.Eval(x);
        // allow odd roots of negative numbers, e.g. x^(1/3)
        if (b < 0 && Math.Abs(e - Math.Round(e)) > 1e-12)
        {
            double inv = 1.0 / e;
            if (Math.Abs(inv - Math.Round(inv)) < 1e-9 && Math.Round(inv) % 2 != 0)
                return -Math.Pow(-b, e);
            return double.NaN;
        }
        return Math.Pow(b, e);
    }

    public override Expr Derivative()
    {
        if (E is Num n)                       // power rule
            return new Mul(new Mul(n, new Pow(B, new Num(n.V - 1))), B.Derivative());
        if (B is Num a)                       // exponential rule a^u
            return new Mul(new Mul(this, new Call("ln", a)), E.Derivative());
        // general: u^v * (v'·ln u + v·u'/u)
        return new Mul(this,
            new Add(new Mul(E.Derivative(), new Call("ln", B)),
                    new Div(new Mul(E, B.Derivative()), B)));
    }

    public override string Format(int p)
    {
        var s = B.Format(5) + "^" + E.Format(4);
        return p > 4 ? "(" + s + ")" : s;
    }
}

public sealed class Call : Expr
{
    public readonly string Name;
    public readonly Expr A;
    public Call(string name, Expr a) { Name = name; A = a; }

    public static readonly string[] Known =
    {
        "sin","cos","tan","cot","sec","csc","asin","acos","atan",
        "sinh","cosh","tanh","exp","ln","log","log2","sqrt","cbrt","abs"
    };

    public override double Eval(double x)
    {
        double u = A.Eval(x);
        return Name switch
        {
            "sin" => Math.Sin(u),
            "cos" => Math.Cos(u),
            "tan" => Math.Tan(u),
            "cot" => 1.0 / Math.Tan(u),
            "sec" => 1.0 / Math.Cos(u),
            "csc" => 1.0 / Math.Sin(u),
            "asin" => Math.Asin(u),
            "acos" => Math.Acos(u),
            "atan" => Math.Atan(u),
            "sinh" => Math.Sinh(u),
            "cosh" => Math.Cosh(u),
            "tanh" => Math.Tanh(u),
            "exp" => Math.Exp(u),
            "ln" => Math.Log(u),
            "log" => Math.Log10(u),
            "log2" => Math.Log2(u),
            "sqrt" => Math.Sqrt(u),
            "cbrt" => Math.Cbrt(u),
            "abs" => Math.Abs(u),
            _ => double.NaN
        };
    }

    public override Expr Derivative()
    {
        Expr du = A.Derivative();
        Expr outer = Name switch
        {
            "sin" => new Call("cos", A),
            "cos" => new Neg(new Call("sin", A)),
            "tan" => new Div(One, new Pow(new Call("cos", A), new Num(2))),
            "cot" => new Neg(new Div(One, new Pow(new Call("sin", A), new Num(2)))),
            "sec" => new Div(new Call("sin", A), new Pow(new Call("cos", A), new Num(2))),
            "csc" => new Neg(new Div(new Call("cos", A), new Pow(new Call("sin", A), new Num(2)))),
            "asin" => new Div(One, new Call("sqrt", new Sub(One, new Pow(A, new Num(2))))),
            "acos" => new Neg(new Div(One, new Call("sqrt", new Sub(One, new Pow(A, new Num(2)))))),
            "atan" => new Div(One, new Add(One, new Pow(A, new Num(2)))),
            "sinh" => new Call("cosh", A),
            "cosh" => new Call("sinh", A),
            "tanh" => new Div(One, new Pow(new Call("cosh", A), new Num(2))),
            "exp" => new Call("exp", A),
            "ln" => new Div(One, A),
            "log" => new Div(One, new Mul(A, new Call("ln", new Num(10)))),
            "log2" => new Div(One, new Mul(A, new Call("ln", new Num(2)))),
            "sqrt" => new Div(One, new Mul(new Num(2), new Call("sqrt", A))),
            "cbrt" => new Div(One, new Mul(new Num(3), new Pow(new Call("cbrt", A), new Num(2)))),
            "abs" => new Div(A, new Call("abs", A)),
            _ => Zero
        };
        return new Mul(outer, du);
    }

    public override string Format(int p) =>
        Name == "abs" ? "|" + A.Format(0) + "|" : Name + "(" + A.Format(0) + ")";
}

/// <summary>
/// Light-weight rewriting so that derivatives are readable instead of a wall of 1's and 0's.
/// It is not a full CAS: it folds constants and removes neutral elements, repeatedly.
/// </summary>
public static class Simplifier
{
    public static Expr Run(Expr e)
    {
        var current = e;
        var text = current.ToString();
        for (int i = 0; i < 12; i++)
        {
            var next = Step(current);
            var nextText = next.ToString();
            if (nextText == text) break;
            current = next; text = nextText;
        }
        return current;
    }

    static bool IsNum(Expr e, out double v)
    {
        if (e is Num n) { v = n.V; return true; }
        v = 0; return false;
    }

    static bool Same(Expr a, Expr b) => a.ToString() == b.ToString();

    static Expr Step(Expr e)
    {
        switch (e)
        {
            case Add a:
            {
                var l = Step(a.L); var r = Step(a.R);
                if (IsNum(l, out var lv) && IsNum(r, out var rv)) return new Num(lv + rv);
                if (IsNum(l, out lv) && lv == 0) return r;
                if (IsNum(r, out rv) && rv == 0) return l;
                if (r is Neg rn) return new Sub(l, rn.O);
                if (IsNum(r, out rv) && rv < 0) return new Sub(l, new Num(-rv));
                if (IsNum(l, out _) && !IsNum(r, out _)) return new Add(r, l); // constants to the right
                return new Add(l, r);
            }
            case Sub s:
            {
                var l = Step(s.L); var r = Step(s.R);
                if (IsNum(l, out var lv) && IsNum(r, out var rv)) return new Num(lv - rv);
                if (IsNum(r, out rv) && rv == 0) return l;
                if (IsNum(l, out lv) && lv == 0) return new Neg(r);
                if (Same(l, r)) return Expr.Zero;
                if (r is Neg rn) return new Add(l, rn.O);
                return new Sub(l, r);
            }
            case Mul m:
            {
                var l = Step(m.L); var r = Step(m.R);
                if (IsNum(l, out var lv) && IsNum(r, out var rv)) return new Num(lv * rv);
                if ((IsNum(l, out lv) && lv == 0) || (IsNum(r, out rv) && rv == 0)) return Expr.Zero;
                if (IsNum(l, out lv) && lv == 1) return r;
                if (IsNum(r, out rv) && rv == 1) return l;
                if (IsNum(l, out lv) && lv == -1) return new Neg(r);
                if (IsNum(r, out rv) && rv == -1) return new Neg(l);
                if (IsNum(r, out _) && !IsNum(l, out _)) return new Mul(r, l); // coefficient first
                if (l is Neg ln2) return new Neg(new Mul(ln2.O, r));
                if (r is Neg rn2) return new Neg(new Mul(l, rn2.O));
                if (Same(l, r)) return new Pow(l, new Num(2));
                // a·(b·z) with a,b numeric  ->  (a·b)·z
                if (IsNum(l, out lv) && r is Mul rm && IsNum(rm.L, out var rlv))
                    return new Mul(new Num(lv * rlv), rm.R);
                return new Mul(l, r);
            }
            case Div d:
            {
                var l = Step(d.L); var r = Step(d.R);
                if (IsNum(r, out var rv) && rv == 1) return l;
                if (IsNum(l, out var lv) && lv == 0) return Expr.Zero;
                if (IsNum(l, out lv) && IsNum(r, out rv) && rv != 0 &&
                    Math.Abs(lv / rv - Math.Round(lv / rv)) < 1e-12) return new Num(lv / rv);
                if (Same(l, r)) return Expr.One;
                if (l is Neg ln3) return new Neg(new Div(ln3.O, r));
                return new Div(l, r);
            }
            case Pow p:
            {
                var b = Step(p.B); var ex = Step(p.E);
                if (IsNum(ex, out var ev))
                {
                    if (ev == 1) return b;
                    if (ev == 0) return Expr.One;
                }
                if (IsNum(b, out var bv) && bv == 1) return Expr.One;
                // e^u is exp(u). Written as a power it prints as 2.718282^u and differentiates
                // through the a^u rule into a stray ln(2.718282), so rewrite it here.
                if (IsNum(b, out bv) && Math.Abs(bv - Math.E) < 1e-12) return new Call("exp", ex);
                if (IsNum(b, out bv) && IsNum(ex, out ev) && ev >= 0 && ev <= 8 &&
                    Math.Abs(ev - Math.Round(ev)) < 1e-12) return new Num(Math.Pow(bv, ev));
                return new Pow(b, ex);
            }
            case Neg n:
            {
                var o = Step(n.O);
                if (IsNum(o, out var ov)) return new Num(-ov);
                if (o is Neg inner) return inner.O;
                return new Neg(o);
            }
            case Call c:
            {
                var a = Step(c.A);
                if (IsNum(a, out var av))
                {
                    if (c.Name == "ln" && av == 1) return Expr.Zero;
                    if (c.Name == "ln" && Math.Abs(av - Math.E) < 1e-12) return Expr.One;
                    if (c.Name == "exp" && av == 0) return Expr.One;
                    if (c.Name == "sqrt" && av >= 0 &&
                        Math.Abs(Math.Sqrt(av) - Math.Round(Math.Sqrt(av))) < 1e-12)
                        return new Num(Math.Round(Math.Sqrt(av)));
                }
                return new Call(c.Name, a);
            }
            default:
                return e;
        }
    }
}
