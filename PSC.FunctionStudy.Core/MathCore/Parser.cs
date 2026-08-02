using System.Globalization;
using System.Text;

namespace PSC.FunctionStudy.MathCore;

/// <summary>
/// Turns text like "2x^2 - 3/(x-1) + ln(x)" into an <see cref="Expr"/> tree.
/// Supports implicit multiplication (2x, 3sin(x), x(x+1)) because that is how students type.
/// </summary>
public static class Parser
{
    public static Expr Parse(string input)
    {
        var tokens = Tokenize(input);
        int pos = 0;
        var e = ParseSum(tokens, ref pos);
        if (pos < tokens.Count)
            throw new FormatException($"Unexpected '{tokens[pos].Text}' at position {tokens[pos].Start + 1}.");
        return e;
    }

    public static bool TryParse(string input, out Expr expr, out string error)
    {
        try { expr = Parse(input); error = ""; return true; }
        catch (Exception ex) { expr = Expr.Zero; error = ex.Message; return false; }
    }

    // ---------- tokens ----------

    enum T { Number, Ident, Op, LParen, RParen, Bar }

    readonly record struct Token(T Kind, string Text, double Value, int Start);

    static List<Token> Tokenize(string s)
    {
        var list = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                var text = s[start..i];
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw new FormatException($"'{text}' is not a valid number.");
                list.Add(new Token(T.Number, text, v, start));
                continue;
            }

            if (char.IsLetter(c))
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]))) i++;
                list.Add(new Token(T.Ident, s[start..i].ToLowerInvariant(), 0, start));
                continue;
            }

            switch (c)
            {
                case '(': case '[': case '{':
                    list.Add(new Token(T.LParen, "(", 0, i)); i++; continue;
                case ')': case ']': case '}':
                    list.Add(new Token(T.RParen, ")", 0, i)); i++; continue;
                case '|':
                    list.Add(new Token(T.Bar, "|", 0, i)); i++; continue;
                case '+': case '-': case '*': case '/': case '^':
                    if (c == '*' && i + 1 < s.Length && s[i + 1] == '*')
                    { list.Add(new Token(T.Op, "^", 0, i)); i += 2; continue; }
                    list.Add(new Token(T.Op, c.ToString(), 0, i)); i++; continue;
                case '\u2212': // unicode minus, pasted from textbooks
                    list.Add(new Token(T.Op, "-", 0, i)); i++; continue;
                case '\u00B7': case '\u00D7':
                    list.Add(new Token(T.Op, "*", 0, i)); i++; continue;
                default:
                    throw new FormatException($"Character '{c}' at position {i + 1} is not allowed.");
            }
        }
        return list;
    }

    // ---------- grammar ----------

    static bool Is(List<Token> t, int p, T kind, string? text = null) =>
        p < t.Count && t[p].Kind == kind && (text is null || t[p].Text == text);

    // sum := product (('+' | '-') product)*
    static Expr ParseSum(List<Token> t, ref int p)
    {
        var left = ParseProduct(t, ref p);
        while (Is(t, p, T.Op, "+") || Is(t, p, T.Op, "-"))
        {
            bool plus = t[p].Text == "+";
            p++;
            var right = ParseProduct(t, ref p);
            left = plus ? new Add(left, right) : new Sub(left, right);
        }
        return left;
    }

    // product := unary (('*' | '/' | implicit) unary)*
    static Expr ParseProduct(List<Token> t, ref int p)
    {
        var left = ParseUnary(t, ref p);
        while (true)
        {
            if (Is(t, p, T.Op, "*")) { p++; left = new Mul(left, ParseUnary(t, ref p)); }
            else if (Is(t, p, T.Op, "/")) { p++; left = new Div(left, ParseUnary(t, ref p)); }
            else if (StartsAtom(t, p)) { left = new Mul(left, ParseUnary(t, ref p)); }
            else return left;
        }
    }

    static bool StartsAtom(List<Token> t, int p) =>
        p < t.Count && (t[p].Kind is T.Number or T.Ident or T.LParen);

    // unary := ('-' | '+')* power
    static Expr ParseUnary(List<Token> t, ref int p)
    {
        if (Is(t, p, T.Op, "-")) { p++; return new Neg(ParseUnary(t, ref p)); }
        if (Is(t, p, T.Op, "+")) { p++; return ParseUnary(t, ref p); }
        return ParsePower(t, ref p);
    }

    // power := atom ('^' unary)?   -- right associative, so 2^-x and 2^3^2 work
    static Expr ParsePower(List<Token> t, ref int p)
    {
        var b = ParseAtom(t, ref p);
        if (Is(t, p, T.Op, "^"))
        {
            p++;
            return new Pow(b, ParseUnary(t, ref p));
        }
        return b;
    }

    static Expr ParseAtom(List<Token> t, ref int p)
    {
        if (p >= t.Count) throw new FormatException("The expression ends too early.");

        var tok = t[p];

        if (tok.Kind == T.Number) { p++; return new Num(tok.Value); }

        if (tok.Kind == T.LParen)
        {
            p++;
            var inner = ParseSum(t, ref p);
            if (!Is(t, p, T.RParen)) throw new FormatException("A closing parenthesis is missing.");
            p++;
            return inner;
        }

        if (tok.Kind == T.Bar)
        {
            p++;
            var inner = ParseSum(t, ref p);
            if (!Is(t, p, T.Bar)) throw new FormatException("A closing '|' is missing.");
            p++;
            return new Call("abs", inner);
        }

        if (tok.Kind == T.Ident)
        {
            string name = tok.Text;

            if (name is "x" or "t") { p++; return Var.X; }
            if (name is "pi" or "\u03C0") { p++; return new Num(Math.PI); }
            if (name == "e" && !Is(t, p + 1, T.LParen)) { p++; return new Num(Math.E); }

            if (Array.IndexOf(Call.Known, name) >= 0)
            {
                p++;

                // sin^2(x) is the usual textbook shorthand for (sin x)^2. The exponent comes
                // before the argument, so it has to be read here — after the argument is too late.
                Expr? shorthandExponent = null;
                if (Is(t, p, T.Op, "^"))
                {
                    p++;
                    shorthandExponent = ParseUnary(t, ref p);

                    // sin^-1 means arcsin in most textbooks but 1/sin by the rules of algebra.
                    // Refuse rather than silently pick one. A negated literal arrives as Neg(Num),
                    // never as a negative Num — the tokenizer has no negative number literals.
                    double? literal = shorthandExponent switch
                    {
                        Num n => n.V,
                        Neg { O: Num inner } => -inner.V,
                        _ => null,
                    };
                    if (literal is < 0)
                        throw new FormatException(
                            $"'{name}^{Expr.Number(literal.Value)}' is ambiguous: it reads as an inverse function in " +
                            $"most textbooks but as a negative power algebraically. Write 1/{name}(x)^" +
                            $"{Expr.Number(-literal.Value)} for the power, or name the inverse (asin, acos, atan).");
                }

                // sqrt x and sqrt(x) are both accepted
                Expr arg;
                if (Is(t, p, T.LParen))
                {
                    p++;
                    arg = ParseSum(t, ref p);
                    if (!Is(t, p, T.RParen)) throw new FormatException($"'{name}' is missing a closing parenthesis.");
                    p++;
                }
                else arg = ParsePower(t, ref p);

                var call = new Call(name, arg);
                if (shorthandExponent is not null) return new Pow(call, shorthandExponent);

                // sin(x)^2 — the same thing written the other way round
                if (Is(t, p, T.Op, "^"))
                {
                    p++;
                    return new Pow(call, ParseUnary(t, ref p));
                }
                return call;
            }

            throw new FormatException($"'{name}' is not a known function or variable. Use x, or one of: {string.Join(", ", Call.Known)}.");
        }

        throw new FormatException($"Unexpected '{tok.Text}' at position {tok.Start + 1}.");
    }
}
