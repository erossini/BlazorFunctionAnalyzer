namespace PSC.FunctionStudy.UI;

public sealed record Reference(string Title, string Source, string Url);

/// <summary>One step of the method: what the app does there and where to read more.</summary>
public sealed record MethodStep(
    int Number,
    string Slug,
    string Title,
    string Question,
    string WhatWeDo,
    string WhyItMatters,
    IReadOnlyList<Reference> Reading);

/// <summary>
/// The explanation behind each of the ten steps. Content rather than computation, so it lives in
/// the shared UI layer: the same text backs the web pages and would back an About screen in a
/// native shell. Every URL here was checked to resolve before it was added — a dead link in a
/// teaching tool is worse than no link.
/// </summary>
public static class MethodGuide
{
    public static readonly IReadOnlyList<MethodStep> Steps = new MethodStep[]
    {
        new(1, "function", "The function",
            "Did we read what you meant?",
            "Your input is turned into an expression tree, tidied up, and printed back. Implicit "
            + "multiplication, the unicode minus pasted from a textbook, |x| for absolute value and "
            + "sin²(x) for the square of a sine are all accepted, so what you type need not be "
            + "typed the way a computer would write it.",
            "Everything that follows is computed from that tree. If the reading is wrong, so is "
            + "the whole study — which is why the tidied form is shown back to you first.",
            new Reference[]
            {
                new("Function (mathematics)", "Wikipedia", "https://en.wikipedia.org/wiki/Function_(mathematics)"),
                new("Elementary function", "Wikipedia", "https://en.wikipedia.org/wiki/Elementary_function"),
                new("Review of functions", "OpenStax Calculus", "https://openstax.org/books/calculus-volume-1/pages/1-1-review-of-functions"),
            }),

        new(2, "domain", "Existence conditions and domain",
            "For which x does the function exist at all?",
            "Every operation that can fail imposes a condition: a denominator must not be zero, an "
            + "even root needs a non-negative argument, a logarithm a strictly positive one, and "
            + "arcsine and arccosine need an argument between −1 and 1. Those conditions are read "
            + "straight off the expression, then the real line is cut at every candidate boundary "
            + "and the pieces where all of them hold are kept.",
            "The domain is the stage on which everything else happens. Every later step — signs, "
            + "limits, derivatives, the plot — is restricted to it, and a point outside it can "
            + "never be a solution no matter how good it looks.",
            new Reference[]
            {
                new("Domain of a function", "Wikipedia", "https://en.wikipedia.org/wiki/Domain_of_a_function"),
                new("Domain", "Wolfram MathWorld", "https://mathworld.wolfram.com/Domain.html"),
                new("Continuity", "Paul's Online Notes", "https://tutorial.math.lamar.edu/Classes/CalcI/Continuity.aspx"),
            }),

        new(3, "symmetry", "Symmetry and periodicity",
            "Does the curve repeat itself?",
            "f(−x) is compared with f(x) and with −f(x) at sample points across the domain: matching "
            + "the first makes the function even, the second odd. A short list of candidate periods "
            + "(π/2, π, 2π, 1, 2, 4) is then tested by shifting x and checking the values are "
            + "unchanged.",
            "Symmetry halves the work: an even or odd function only needs studying for x ≥ 0, and a "
            + "periodic one only over a single period. It is also a strong check on everything "
            + "else — the turning points of an odd function must come in mirrored pairs.",
            new Reference[]
            {
                new("Even and odd functions", "Wikipedia", "https://en.wikipedia.org/wiki/Even_and_odd_functions"),
                new("Periodic function", "Wikipedia", "https://en.wikipedia.org/wiki/Periodic_function"),
                new("Symmetry", "Paul's Online Notes", "https://tutorial.math.lamar.edu/Classes/Alg/Symmetry.aspx"),
            }),

        new(4, "intercepts", "Intercepts",
            "Where does the curve cross the axes?",
            "If 0 belongs to the domain, f(0) gives the point on the y-axis. For the x-axis, f is "
            + "sampled densely and every sign change is narrowed down by bisection, which also "
            + "catches roots where the curve touches the axis without crossing it.",
            "These are the points you can plot exactly, with no estimation. The roots also divide "
            + "the domain into the intervals used in the next step, since a continuous function "
            + "cannot change sign without passing through zero.",
            new Reference[]
            {
                new("Zero of a function", "Wikipedia", "https://en.wikipedia.org/wiki/Zero_of_a_function"),
                new("y-Intercept", "Wolfram MathWorld", "https://mathworld.wolfram.com/y-Intercept.html"),
                new("Root", "Wolfram MathWorld", "https://mathworld.wolfram.com/Root.html"),
            }),

        new(5, "sign", "Sign of f(x)",
            "Which side of the x-axis is the curve on?",
            "The domain is split at the roots found in the previous step, and one point inside each "
            + "piece is tested. Because the function is continuous between consecutive roots, that "
            + "single sample settles the sign for the whole interval.",
            "This is the intermediate value theorem doing real work: it is what makes one test per "
            + "interval enough, instead of checking infinitely many points. Knowing the sign tells "
            + "you where the curve lies before you plot a single value.",
            new Reference[]
            {
                new("Intermediate value theorem", "Wikipedia", "https://en.wikipedia.org/wiki/Intermediate_value_theorem"),
                new("Intermediate value theorem", "Wolfram MathWorld", "https://mathworld.wolfram.com/IntermediateValueTheorem.html"),
                new("Polynomial inequalities", "Paul's Online Notes", "https://tutorial.math.lamar.edu/Classes/Alg/PolynomialInequalities.aspx"),
            }),

        new(6, "limits", "Limits at the edges of the domain",
            "What happens as x approaches a boundary?",
            "At every edge — a hole punched out of the domain, a finite endpoint, and ±∞ if the "
            + "domain reaches that far — the function is evaluated at points closing in on the "
            + "boundary and the trend is classified: settling on a value, running away to infinity, "
            + "or doing neither.",
            "Limits are what turn a list of points into a shape. They say whether the curve shoots "
            + "off near a hole, flattens towards a level, or quietly closes a gap — as sin(x)/x "
            + "does at 0, where the function is undefined but the limit is 1.",
            new Reference[]
            {
                new("Limit of a function", "Wikipedia", "https://en.wikipedia.org/wiki/Limit_of_a_function"),
                new("One-sided limit", "Wikipedia", "https://en.wikipedia.org/wiki/One-sided_limit"),
                new("One-sided limits", "Paul's Online Notes", "https://tutorial.math.lamar.edu/Classes/CalcI/OneSidedLimits.aspx"),
            }),

        new(7, "asymptotes", "Asymptotes",
            "Which straight lines does the curve hug?",
            "A one-sided limit that runs to infinity marks a vertical asymptote. A finite limit at "
            + "±∞ marks a horizontal one. Otherwise the slope m = lim f(x)/x is computed, and if it "
            + "is finite and non-zero, q = lim [f(x) − mx] completes an oblique asymptote y = mx + q.",
            "Asymptotes are the skeleton to draw first: sketch them, and the curve has far fewer "
            + "places left to go. The degrees of a rational function predict which kind to expect "
            + "before any limit is taken, which is a useful check on the answer.",
            new Reference[]
            {
                new("Asymptote", "Wikipedia", "https://en.wikipedia.org/wiki/Asymptote"),
                new("Asymptote", "Wolfram MathWorld", "https://mathworld.wolfram.com/Asymptote.html"),
                new("Limits at infinity and asymptotes", "OpenStax Calculus", "https://openstax.org/books/calculus-volume-1/pages/4-6-limits-at-infinity-and-asymptotes"),
            }),

        new(8, "first-derivative", "First derivative: where it rises and falls",
            "Where does the curve turn?",
            "f′ is computed symbolically — exactly, by the product, quotient and chain rules, not by "
            + "approximation. Its sign gives the direction of travel: positive rising, negative "
            + "falling. Turning points are found three ways, because looking for zeros of f′ alone "
            + "misses two of them: where f′ crosses zero, where it jumps sign without ever being "
            + "zero (the corner of |x|), and at closed endpoints of the domain, where f′ need not "
            + "vanish at all. Everything the function reaches is then weighed against the values it "
            + "only approaches, to decide the absolute maximum and minimum.",
            "This is where the interesting points live. Note the distinction the last step draws: a "
            + "value that is reached is a maximum, while one the curve only closes in on — like the "
            + "1 that sin(x)/x approaches at its hole — is a supremum, and saying so is not "
            + "pedantry but the difference between an attainable answer and an unattainable one.",
            new Reference[]
            {
                new("Derivative", "Wikipedia", "https://en.wikipedia.org/wiki/Derivative"),
                new("Maximum and minimum", "Wikipedia", "https://en.wikipedia.org/wiki/Maximum_and_minimum"),
                new("Fermat's theorem (stationary points)", "Wikipedia", "https://en.wikipedia.org/wiki/Fermat%27s_theorem_(stationary_points)"),
                new("Minimum and maximum values", "Paul's Online Notes", "https://tutorial.math.lamar.edu/Classes/CalcI/MinMaxValues.aspx"),
            }),

        new(9, "second-derivative", "Second derivative: concavity",
            "Which way does the curve bend?",
            "f′ is differentiated again, and the sign of f″ is charted across the domain. Positive "
            + "means concave up, holding water; negative means concave down. Where the sign changes "
            + "and the function is defined, there is an inflection point.",
            "Concavity is what separates two curves that rise at the same rate but bend opposite "
            + "ways. It also settles ambiguous turning points: at a stationary point, f″ > 0 means a "
            + "minimum and f″ < 0 a maximum.",
            new Reference[]
            {
                new("Second derivative", "Wikipedia", "https://en.wikipedia.org/wiki/Second_derivative"),
                new("Inflection point", "Wikipedia", "https://en.wikipedia.org/wiki/Inflection_point"),
                new("Concave function", "Wikipedia", "https://en.wikipedia.org/wiki/Concave_function"),
                new("The shape of a graph, part II", "Paul's Online Notes", "https://tutorial.math.lamar.edu/Classes/CalcI/ShapeofGraphPtII.aspx"),
            }),

        new(10, "graph", "The graph",
            "What does it all look like?",
            "The plot is assembled from everything above: asymptotes as dashed guides, turning "
            + "points and inflections as marked dots, and the curve itself cut into separate "
            + "branches wherever it leaves the domain or jumps a pole, so two sides of an asymptote "
            + "are never joined by a line that does not exist.",
            "Drawing the curve is the check on the other nine steps. If the sketch does not match "
            + "the signs, limits and turning points you worked out, one of them is wrong — and "
            + "finding out which is where the understanding happens.",
            new Reference[]
            {
                new("Graph of a function", "Wikipedia", "https://en.wikipedia.org/wiki/Graph_of_a_function"),
                new("Curve sketching", "Wikipedia", "https://en.wikipedia.org/wiki/Curve_sketching"),
                new("Derivatives and the shape of a graph", "OpenStax Calculus", "https://openstax.org/books/calculus-volume-1/pages/4-5-derivatives-and-the-shape-of-a-graph"),
            }),
    };

    public static MethodStep? Find(string? slug) =>
        Steps.FirstOrDefault(s => string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
