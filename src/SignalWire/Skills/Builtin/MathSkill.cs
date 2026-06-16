using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Perform basic mathematical calculations.</summary>
public sealed partial class MathSkill : SkillBase
{
    [GeneratedRegex(@"^[\d\s\+\-\*\/\%\.\(\)\^]+$")]
    private static partial Regex SafeExpressionPattern();

    public override string Name => "math";
    public override string Description => "Perform basic mathematical calculations";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    [SuppressMessage("Design", "CA1031", Justification = "Expression evaluation is best-effort; any failure is returned to the caller as an in-band error string.")]
    public override void RegisterTools(AgentBase agent)
    {
        DefineTool(
            "calculate",
            "Perform a mathematical calculation with basic operations (+, -, *, /, %, **)",
            new Dictionary<string, object>
            {
                ["expression"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The mathematical expression to evaluate (e.g., \"2 + 3 * 4\")",
                    // Not required — Python's math skill passes none (math/skill.py:33).
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var expression = args.TryGetValue("expression", out var exObj) ? exObj as string ?? "" : "";

                if (expression.Length == 0)
                {
                    result.SetResponse("Error: No expression provided.");
                    return result;
                }

                if (!SafeExpressionPattern().IsMatch(expression))
                {
                    result.SetResponse("Error: Invalid characters in expression. Only numbers, operators (+, -, *, /, %, **), parentheses, and decimal points are allowed.");
                    return result;
                }

                try
                {
                    var sanitized = expression.Replace("^", "**", StringComparison.Ordinal);
                    var table = new System.Data.DataTable();
                    // DataTable.Compute supports basic arithmetic
                    var simpleExpr = sanitized.Replace("**", "^", StringComparison.Ordinal);
                    var value = table.Compute(simpleExpr, "");

                    if (value is null || value == DBNull.Value)
                    {
                        result.SetResponse($"Error: Could not evaluate expression \"{expression}\".");
                    }
                    else
                    {
                        var numValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        if (double.IsInfinity(numValue))
                        {
                            result.SetResponse("Error: Division by zero or overflow in expression.");
                        }
                        else if (double.IsNaN(numValue))
                        {
                            result.SetResponse("Error: Result is not a number.");
                        }
                        else
                        {
                            result.SetResponse($"The result of {expression} is {numValue}");
                        }
                    }
                }
                catch (Exception e)
                {
                    result.SetResponse($"Error evaluating expression: {e.Message}");
                }

                return result;
            });
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Mathematical Calculations",
            ["body"] = "You can perform mathematical calculations.",
            ["bullets"] = new List<string>
            {
                "Supported operators: + (add), - (subtract), * (multiply), / (divide), % (modulo), ** (power)",
                "Parentheses can be used for grouping.",
                "Use the calculate tool with a string expression.",
            },
        }];
    }
}
