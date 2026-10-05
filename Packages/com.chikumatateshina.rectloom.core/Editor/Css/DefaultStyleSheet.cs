#nullable enable

using Rectloom.Core.Css.Ast;
using Rectloom.Core.Css.Parsing;
using Rectloom.Core.Diagnostics;

namespace Rectloom.Core.Css
{
    /// <summary>
    /// The compiler's built-in stylesheet, equivalent to a browser's user-agent stylesheet.
    /// </summary>
    /// <remarks>
    /// It exists so that markup with no CSS still produces something usable: headings are larger
    /// than body text, and a bare button is visible and clickable.
    /// <para>
    /// It cascades beneath every author stylesheet, so any declaration an author writes wins,
    /// whatever its specificity.
    /// </para>
    /// </remarks>
    public static class DefaultStyleSheet
    {
        /// <summary>Virtual path used when reporting a diagnostic against the built-in stylesheet.</summary>
        public const string VirtualPath = "Rectloom/DefaultStyleSheet.css";

        /// <summary>
        /// The built-in stylesheet source.
        /// </summary>
        /// <remarks>
        /// Sizes follow browser conventions so that markup written for the web lands close to where
        /// its author expects. Vertical margins on text blocks are kept, because removing them makes
        /// a page of headings and paragraphs run together.
        /// </remarks>
        public const string Source = @"
body {
    display: block;
    width: 100%;
    height: 100%;
}

div, p, h1, h2, h3, h4, h5, h6, img, hr,
article, section, header, footer, nav, main, aside, hgroup, address,
figure, figcaption, blockquote, pre,
ul, ol, li, dl, dt, dd {
    display: block;
}

span, a, strong, b, em, i, u, s, strike, small, big,
code, kbd, samp, var, mark, cite, q, abbr, time,
label, sub, sup, ins, del, bdi, bdo, ruby, rt, rp {
    display: inline;
}

p {
    margin: 16px 0;
}

h1 { font-size: 32px; font-weight: bold; margin: 21px 0; }
h2 { font-size: 24px; font-weight: bold; margin: 20px 0; }
h3 { font-size: 19px; font-weight: bold; margin: 18px 0; }
h4 { font-size: 16px; font-weight: bold; margin: 21px 0; }
h5 { font-size: 13px; font-weight: bold; margin: 22px 0; }
h6 { font-size: 11px; font-weight: bold; margin: 24px 0; }

strong, b { font-weight: bold; }
em, i, cite, var, address { font-style: italic; }
u, ins { text-decoration: underline; }
s, strike, del { text-decoration: line-through; }
small { font-size: 13px; }
big { font-size: 19px; }
a { color: #0645ad; text-decoration: underline; }

ul, ol, dl, blockquote, figure, pre {
    margin: 16px 0;
}

ul, ol { padding-left: 40px; }
dd, blockquote, figure { margin-left: 40px; }
pre, code, kbd, samp { white-space: pre; }

hr {
    height: 1px;
    margin: 8px 0;
    background-color: #a0a0a0;
}

button {
    display: flex;
    justify-content: center;
    align-items: center;
    padding: 6px 16px;
    background-color: #e1e1e1;
    border-width: 1px;
    border-color: #adadad;
    border-radius: 3px;
    color: #101010;
    text-align: center;
    white-space: nowrap;
}
";

        private static CssStyleSheet? _parsed;

        /// <summary>
        /// Gets the parsed built-in stylesheet.
        /// </summary>
        /// <returns>The stylesheet. The same instance is returned on every call.</returns>
        /// <remarks>
        /// The source is fixed and known to parse, so it is parsed once and shared. Any diagnostic
        /// it produced would be a compiler defect rather than an authoring mistake, which is what
        /// the accompanying test asserts.
        /// </remarks>
        public static CssStyleSheet Get()
        {
            return _parsed ??= CssParser.Parse(VirtualPath, Source, new DiagnosticSink());
        }
    }
}
