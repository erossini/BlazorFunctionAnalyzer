# PSC Function Study

A step-by-step **study of a function** for school and university — the kind of exercise where you
are handed an f(x) and asked to work out its domain, symmetry, intercepts, sign, limits, asymptotes,
monotonicity and concavity, then sketch the curve.

Type a function and the app works through the same ten steps you would write out by hand, showing
its reasoning at each one, and finally draws the graph from what it found.

It is written in the spirit of **Derive** — not a black box that prints an answer, but a tool that
shows the method, so a student can compare it against their own working and find where they went
wrong.

```
f(x) = (x² − 1)/(x − 2)

01  The function                  Rational function, degree 2 over degree 1
02  Existence conditions          x − 2 ≠ 0   →   D = (−∞, 2) ∪ (2, +∞)
03  Symmetry and periodicity      neither even nor odd
04  Intercepts                    f(0) = ½ ;  x = −1, 1
05  Sign of f(x)                  above/below the axis on each interval
06  Limits at the edges           lim x→2⁻ = −∞ ,  lim x→2⁺ = +∞
07  Asymptotes                    x = 2  and  y = x + 2
08  First derivative              turning points, and the absolute max/min
09  Second derivative             concavity and inflection points
10  The graph                     drawn from all of the above
```

Every step links to a short reference page explaining what is being looked for and why, with further
reading from Wikipedia, Wolfram MathWorld, Paul's Online Notes and OpenStax.

---

## What it does well

**Reads what a student actually types.** Implicit multiplication (`2x`, `3sin(x)`, `x(x+1)`), square
brackets and braces as parentheses, `|x|` for absolute value, `**` for powers, the unicode minus
pasted straight from a textbook, `sin^2(x)` for the square of a sine, and bare arguments like
`sqrt x`. Errors come back as a sentence, not a stack trace.

**Differentiates exactly.** The product, quotient and chain rules applied symbolically — not a
numerical approximation. The results are simplified only as far as needed to stay readable.

**Knows what it is looking at.** Structural classification (polynomial and its degree, rational,
radical, exponential, composite…) plus a catalogue of 18 named curves. Study `sin(x)/x` and it tells
you that is the *sinc function* and why the graph looks unbroken at a point outside its domain.

**Distinguishes a maximum from a bound.** A value the function *reaches* is a maximum; one it only
closes in on — like the 1 that `sin(x)/x` approaches at the hole it cannot occupy — is a supremum.
The app says which, and reports "unbounded" or "cannot be determined" rather than guessing.

**Finds turning points that a naive search misses.** Zeros of f′ are only one of three cases. Corners
(`|x|` at the origin, where f′ jumps sign without ever being zero) and closed domain endpoints
(`√(4−x²)` at x = ±2, where f′ is infinite) are found too.

**Typesets the formulas.** Fractions stack, exponents raise, radicals draw — using MathML that the
browser lays out natively. No LaTeX, no JavaScript library, no fonts to ship.

**Plots without a chart library.** The graph is plain SVG generated from the analysis, cut into
separate branches wherever the curve leaves the domain or jumps a pole, so two sides of an asymptote
are never joined by a line that does not exist.

---

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/erossini/BlazorFunctionAnalyzer.git
cd BlazorFunctionAnalyzer

dotnet build PSC.FunctionStudy.slnx
dotnet test  PSC.FunctionStudy.Core.Tests
dotnet run   --project PSC.FunctionStudy.Web
```

Then open <http://localhost:5103>.

---

## Project structure

```
PSC.FunctionStudy.Core         the engine — parser, expression tree, analysis. No dependencies.
PSC.FunctionStudy.Core.Tests   xUnit suite over the engine
PSC.FunctionStudy.UI           Razor Class Library — the components, stylesheet and guide content
PSC.FunctionStudy.Web.Client   Blazor WebAssembly — the routable pages
PSC.FunctionStudy.Web          ASP.NET Core host
```

References run one way only:

```
Core  ←  UI  ←  Web.Client  ←  Web
```

`Core` knows nothing about Blazor. `UI` knows nothing about the host it runs in — no `@page`, no
`@rendermode`, no JavaScript interop, and anything host-specific (browser storage, routing) arrives
through an interface or a parameter. That constraint is deliberate: adding a **.NET MAUI Blazor
Hybrid** shell for Windows, macOS, iOS and Android becomes a matter of adding one project and a
thin wrapper page, rather than forking the UI.

### How a study is computed

```
"(x^2-1)/(x-2)"  →  Parser  →  Expr tree  →  FunctionAnalyzer  →  StudyReport  →  components
```

| File | Responsibility |
|---|---|
| `MathCore/Parser.cs` | Recursive-descent parser over hand-rolled tokens |
| `MathCore/Expr.cs` | The expression tree: `Eval`, `Derivative`, `Format`, plus the simplifier |
| `MathCore/MathMl.cs` | Renders an expression as MathML for the browser to typeset |
| `Analysis/Numeric.cs` | Roots, limits, sign changes, turning points |
| `Analysis/DomainSolver.cs` | Existence conditions and the intervals where they all hold |
| `Analysis/Recognition.cs` | What kind of function it is, and its name if it has one |
| `Analysis/FunctionAnalyzer.cs` | Orchestrates the ten steps into a `StudyReport` |

The governing design rule is **symbolic for derivatives, numeric for everything else**. Derivatives
are computed exactly, because a wrong derivative invalidates the whole study. Roots, limits and sign
charts are computed numerically, which is far more robust across the mix of function families a
student will actually type than trying to solve each one in closed form.

### Supported functions

`sin` `cos` `tan` `cot` `sec` `csc` `asin` `acos` `atan` `sinh` `cosh` `tanh` `exp` `ln` `log`
`log2` `sqrt` `cbrt` `abs`, plus `pi` and `e`.

Adding one means touching four places together: `Call.Known`, `Call.Eval`, `Call.Derivative`, and —
if it restricts the domain — `DomainSolver.Walk`. Add a row to the derivative test at the same time
and the new rule is verified against a numeric slope without anyone computing it by hand.

---

## Testing

```bash
dotnet test PSC.FunctionStudy.Core.Tests
```

77 test methods — 165 cases once the parameterised ones expand — cover the parser, simplifier,
derivatives, domain solver, recognition, absolute extremes and MathML output.

The most valuable one is `Symbolic_derivative_matches_the_numeric_slope`: it differentiates
symbolically and checks the result against a central finite difference at several points across a
range of functions. A broken differentiation rule fails without anyone having to work out the right
answer first.

---

## Known limits

- **Analysis is confined to `x ∈ [−40, 40]`** (`FunctionAnalyzer.Range`). Features outside that
  window are not found.
- **Sampling, not proof.** Symmetry, periodicity, roots, limits and extrema are all numeric.
  Periodicity in particular is only tested against a fixed list of candidates (π/2, π, 2π, 1, 2, 4).
- **One variable, expressions not equations.** `f(x)` only — `E = mc²` is out of scope, and
  supporting it would mean changing `Eval`'s signature and every consumer of it.
- **The engine is tested; the UI is not.** Components are verified by hand in a browser.

---

## Roadmap

- LaTeX output, so formulas can be pasted into Word or a LaTeX document
- Progressive Web App, for offline install on desktop and mobile
- .NET MAUI Blazor Hybrid shells for Windows, macOS, iOS and Android
- Accounts, saved studies, and a gallery of what others have studied
- Photograph a formula and have it recognised

---

## Author

Built by **Enrico Rossini**.

- [PureSourceCode](https://puresourcecode.com/)
- [Bio](https://flnk.it/enrico) · [LinkedIn](https://www.linkedin.com/in/rossiniuk/) · [GitHub](https://github.com/erossini)
- Other apps: [Language In Use](https://languageinuse.com/) · [SplitEasy](https://spliteasy.app/)
