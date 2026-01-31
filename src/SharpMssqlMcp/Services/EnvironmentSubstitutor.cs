using System.Text.RegularExpressions;

namespace SharpMssqlMcp.Services;

public class EnvironmentSubstitutor
{
    private static readonly Regex _regex = new Regex(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    public string Substitute(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return _regex.Replace(input, match =>
        {
            var varName = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(varName);

            if (value == null)
            {
                throw new InvalidOperationException($"Environment variable '{varName}' not found for substitution.");
            }

            return value;
        });
    }
}
