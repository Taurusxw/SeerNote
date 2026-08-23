using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SeerNote.Domain
{
    public static class PromptTemplate
    {
        private static readonly Regex VariablePattern = new Regex(@"\{\{([^{}]*)\}\}", RegexOptions.Compiled);

        public static IList<string> Parse(string text)
        {
            var variables = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in VariablePattern.Matches(text ?? String.Empty))
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length > 0 && seen.Add(name))
                {
                    variables.Add(name);
                }
            }

            return variables;
        }

        public static string Render(string text, IDictionary<string, string> values)
        {
            string rendered;
            string error;
            if (!TryRender(text, values, out rendered, out error))
            {
                throw new ArgumentException(error, "values");
            }

            return rendered;
        }

        public static bool TryRender(string text, IDictionary<string, string> values, out string rendered, out string error)
        {
            var source = text ?? String.Empty;
            var variables = Parse(source);
            foreach (var variable in variables)
            {
                string value;
                if (values == null || !values.TryGetValue(variable, out value))
                {
                    rendered = null;
                    error = "Missing value for template variable: " + variable;
                    return false;
                }
            }

            rendered = VariablePattern.Replace(source, delegate(Match match)
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length == 0)
                {
                    return match.Value;
                }

                string value;
                return values.TryGetValue(name, out value) ? value ?? String.Empty : match.Value;
            });
            error = null;
            return true;
        }
    }
}
