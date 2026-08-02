# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A step-by-step "study of a function" (curve sketching) tool for school and university, in the spirit
of Derive: given a single-variable f(x), it works through domain, symmetry, intercepts, sign, limits,
asymptotes, monotonicity, concavity and a plot — as ten numbered steps, the way it is written out by
hand.

The engine is deliberately kept in a dependency-free class library so the same core can back the web
app and, later, native apps (MAUI Blazor Hybrid for Windows, macOS, iOS and Android).

## Build and run

```powershell
dotnet build PSC.FunctionStudy.slnx
dotnet test  PSC.FunctionStudy.Core.Tests
dotnet run   --project PSC.FunctionStudy.Web   # http://localhost:5103, https://localhost:7075

dotnet test PSC.FunctionStudy.Core.Tests --filter "FullyQualifiedName~RecogniserTests"
dotnet test PSC.FunctionStudy.Core.Tests --filter "DisplayName~Symbolic_derivative"
```

No linter is configured; build warnings and the test suite are the only static checks. Both are
currently clean — keep them that way.

## Project layout

```
PSC.FunctionStudy.Core         class library, net10.0, zero dependencies — the whole engine
PSC.FunctionStudy.Core.Tests   xUnit — parser, simplifier, derivatives, analysis, recognition
PSC.FunctionStudy.UI           Razor Class Library — Study/FunctionPlot components + app.css
PSC.FunctionStudy.Web.Client   Blazor WebAssembly — routable page, references UI
PSC.FunctionStudy.Web          ASP.NET Core host, references Web.Client
```

References run `Core ← UI ← Web.Client ← Web`. **Nothing may point back up.** Core knows nothing
about Blazor; UI knows nothing about the host it runs in. That constraint is what makes a MAUI shell
a matter of adding one project rather than forking the UI.

Namespaces do not mirror folders. The `Core` project's root namespace is `PSC.FunctionStudy`, so its
code lives in `PSC.FunctionStudy.MathCore` and `PSC.FunctionStudy.Analysis` — not
`PSC.FunctionStudy.Core.*`.

### Render mode lives in the host, never in the RCL

`PSC.FunctionStudy.UI/Study.razor` is a plain component: no `@page`, no `@rendermode`, no JS interop.
The host supplies both — `PSC.FunctionStudy.Web.Client/Pages/Home.razor` is a five-line wrapper
carrying `@page "/"` and `@rendermode InteractiveWebAssembly` around `<Study />`.

Keep it that way. A `@rendermode` inside the RCL would break the MAUI Blazor Hybrid host, which has
no render modes at all. Any future host adds its own wrapper page.

WebAssembly is the right mode here because the engine is pure computation with no I/O: it runs in the
browser with no round trip and works offline. `Program.cs` still registers the interactive Server mode
too, so switching a page to `InteractiveAuto` needs no host changes. `ReconnectModal` is inert while
nothing uses a server circuit.

### Anything host-specific goes behind an interface in the RCL

`IStudyHistory` (recent functions) follows the same rule as the render mode: the contract lives in
`PSC.FunctionStudy.UI`, the implementation in the host. `Web.Client` supplies
`LocalStorageStudyHistory`; `Web` registers `NullStudyHistory`, because browser storage is
unreachable while the server prerenders — which is also why `Study.razor` reads the list in
`OnAfterRenderAsync(firstRender)` rather than `OnInitialized`. **Both hosts must register an
implementation** or `[Inject]` throws during prerender. A MAUI shell would register a file-backed
one; a future server-backed history swaps in without touching the component.

## Architecture

The pipeline is `string → Expr → StudyReport → UI`:

- **`Core/MathCore/Parser.cs`** — recursive-descent parser over hand-rolled tokens. Deliberately
  lenient about how students type: implicit multiplication (`2x`, `3sin(x)`, `x(x+1)`), `[]`/`{}` as
  parentheses, `|x|` for `abs`, `**` for `^`, unicode `−`/`·`/`×`, `sin^2(x)` meaning `(sin x)^2`,
  and bare arguments (`sqrt x`). `^` is right-associative. `TryParse` turns exceptions into a message
  string for the UI.
- **`Core/MathCore/Expr.cs`** — the expression tree. Every node does three things: `Eval`,
  `Derivative` (symbolic, exact — product/quotient/chain rules), and `Format(parentPrecedence)` for
  printing with minimal parentheses. Precedence levels are documented at the top of the file: 1 `+ -`,
  2 `* /`, 3 unary minus, 4 `^`, 5 atom. `Simplifier.Run` is a fixpoint rewriter (max 12 passes,
  compares printed forms to detect convergence) that only folds constants and drops neutral elements —
  it is not a CAS, and its job is purely to keep printed derivatives readable.
- **`Core/Analysis/Numeric.cs`** — everything numeric. The design rule is *symbolic for derivatives,
  numeric for everything else*: `Roots` (sampling + bisection, plus a tangent-root search via ternary
  minimisation of |f|), `At`/`AtInfinity` (evaluate at `10^-k` / `10^k` and `Classify` the tail —
  settled, Aitken-accelerated, or monotonically running away → ±∞), and `Pretty` (renders with unicode
  minus and snaps to π, e, ½, ⅓).
- **`Core/Analysis/DomainSolver.cs`** — `CollectConstraints` walks the tree emitting existence
  conditions (`Div` → denominator ≠ 0, `sqrt` → ≥ 0, `ln`/`log` → > 0, `tan`/`sec` → cos ≠ 0,
  `asin`/`acos` → |·| ≤ 1, fractional/negative powers). `Solve` never solves an inequality
  symbolically: it cuts the real line at every candidate boundary root, samples each segment (three
  points, so slivers are not misjudged), and stitches the survivors into `Interval`s. `SignChart`
  reuses the same idea and merges adjacent same-sign pieces.
- **`Core/MathCore/MathMl.cs`** — renders an expression as MathML Core, which the browser lays out
  itself: real fraction bars, raised exponents, radical signs. **No LaTeX, no KaTeX, no JS interop** —
  the markup goes straight into the page, which is what keeps the UI layer portable to a WebView.
  Precedence mirrors `Expr.Format` except where an element already groups visually: `<mfrac>` and
  `<msqrt>` drop the parentheses the flat form needs. `exp(u)` renders back as e^u. Safe to emit as
  a `MarkupString` because the parser accepts only `x`, digits and `Call.Known` names, so no
  user-controlled text can reach the page as markup.
- **`Core/Analysis/Recognition.cs`** — says what kind of function it is, two ways, both exact.
  `Classify` walks the tree for structure (polynomial degree via `Degree`, rational, and which
  transcendental families appear); a small catalogue then looks the printed form up by name.
  **The catalogue is keyed on `Simplifier.Run(Parser.Parse(source)).ToString()` computed at static
  init** — never on a hand-written formatted string — so entries can't drift from the formatter and
  anything the user types that normalises to the same tree matches. Deliberately no LLM: a confident
  wrong attribution is worse than none. Adding an entry is one line in `Catalogue`.
- **`Core/Analysis/FunctionAnalyzer.cs`** — orchestrates the ten steps into `StudyReport`, in the
  order the UI renders them. Symmetry and period are detected by sampling, not proof (period
  candidates are a fixed list: π/2, π, 2π, 1, 2, 4). Oblique asymptotes come from `m = lim f(x)/x`
  then `q = lim [f(x) − mx]`. `Asymptote.P`/`Q` carry the numbers the plot needs (vertical → P = x;
  horizontal → Q = y; oblique → P = m, Q = q).

  Turning points come from **three** passes, because roots of f′ alone miss two cases: zeros of f′
  (ordinary stationary points), then `Numeric.SignChanges` + `Numeric.Turn` for **corners**, where
  f′ flips sign without ever being zero (`abs(x)` at the origin), then closed domain **endpoints**,
  where f′ need not vanish at all (`sqrt(4-x^2)` at x = ±2, where it is infinite).

  `BuildAbsoluteExtremes` then weighs everything the function *reaches* — turning points, corners,
  endpoints, isolated domain points — against what it only *closes in on*, which is the limits at
  open ends and at infinity. `Extreme.Kind` records which of four outcomes applies: `Attained`,
  `Approached` (a supremum or infimum never reached — `sin(x)/x` closes in on 1 at the hole it
  cannot reach), `Unbounded`, or `Undetermined` when a limit does not exist and nothing honest can
  be claimed. Periodic functions skip the ±∞ limits: those never converge, but the turning points
  already cover every value taken. Saddle points are excluded as candidates — a point where
  monotonicity does not change cannot be a global extreme.

Everything outside the domain is clipped to `const double Range = 40` — infinite intervals are
sampled on `[-40, 40]`. Raising it makes analysis slower roughly linearly.

- **`UI/Study.razor`** — the ten `<section class="step">` blocks, driven entirely off `StudyReport`.
  Runs the analysis synchronously in `OnInitialized` with a default sample, so a fresh page already
  shows a worked example.
- **`UI/FunctionPlot.razor`** — SVG plotting with **no JS interop and no chart library**, which is
  what lets the identical component render inside a MAUI `BlazorWebView`. Fixed 720×460 viewBox with
  `Sx`/`Sy` mapping data to pixels. Two details matter: the y-window is taken from the 3rd–97th
  percentile of sampled values so a single pole does not flatten the curve, and the polyline is cut
  into separate branches wherever x leaves the domain or y jumps more than 45% of the window (poles).
  It re-fits the view only when `Report.Input` changes, so zoom/pan survive re-renders. `N()` formats
  every coordinate with `InvariantCulture` — required, or a comma decimal separator corrupts the SVG.
- **`UI/IStudyHistory.cs`** — the recent-functions contract plus `NullStudyHistory`. Per-device
  today; the interface is what lets a server-backed, per-account history replace it later.
- **`UI/wwwroot/app.css`** — the whole stylesheet, served to hosts from
  `_content/PSC.FunctionStudy.UI/app.css`. Light and dark via `prefers-color-scheme`, all colours as
  custom properties on `:root`.

## Conventions

- Culture: every number that reaches text or SVG goes through `InvariantCulture` (`Numeric.Pretty`,
  `Expr.Number`, `FunctionPlot.N`).
- User-facing math uses real unicode glyphs, not ASCII: `−` (U+2212) for minus, `∞`, `≠`, `≥`, `∪`,
  `·`, `′`/`″` for derivatives. The parser accepts these on input as well.
- Numeric comparisons are tolerance-based throughout (`1e-9`, `1e-12`, scaled by `1 + |value|` where
  magnitude varies). Do not introduce exact `==` on doubles — with one deliberate exception, where
  the question really is whether values are bit-identical: detecting a flat run caused by underflow
  (`exp(-x^2)` is precisely `0.0` past |x| ≈ 27, which otherwise reports phantom stationary points
  and a false attained minimum). Those sites say so in a comment.
- Adding a function to the engine means touching four places in lockstep: `Call.Known`, `Call.Eval`,
  `Call.Derivative`, and — if it restricts the domain — `DomainSolver.Walk`. Add a row to
  `Symbolic_derivative_matches_the_numeric_slope` at the same time.
- The engine is **single-variable by construction**: `Eval(double x)`, one `Var.X`, and expressions
  rather than equations. Multi-variable formulas (`E = mc²`) are not a small extension — they change
  `Eval`'s signature and every consumer in `Analysis`.

## Testing notes

`Core.Tests` pins the engine's behaviour; the numeric heuristics (limit classification, tangent-root
finding, the fixed period-candidate list, tolerance constants) are exactly the code that regresses
silently, so prefer adding a case there over reasoning about them.

The strongest test in the suite is `Symbolic_derivative_matches_the_numeric_slope`: it differentiates
symbolically and checks the result against a central finite difference at several points, with a
relative tolerance. Adding a function to `Call.Known` should come with a row there — it verifies the
new derivative rule without anyone hand-computing the answer.

## Known gaps

- **Explanatory prose lives in `Study.razor`**, not in Core — sentences like *"f(−x) = f(x) … so the
  function is even"* are hardcoded English in the component. For a step-by-step teaching tool that
  text is domain output, not presentation. Moving it into Core as a structured `StudyStep` record is
  what unlocks localization and keeps future hosts from duplicating it. `Asymptote.Reason` already
  works this way — it is the pattern to generalize.
- **No LaTeX output.** `MathMl` covers display; a `ToLatex()` beside it would let students paste
  formulas into Word or a LaTeX document.
- `PSC.FunctionStudy.Web/wwwroot/` is empty, so `/favicon.ico` 404s.
