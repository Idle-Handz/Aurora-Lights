using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Rules;

namespace Builder.Presentation.Services;

/// <summary>Evaluates candidate prerequisites against the current character and selection context.</summary>
public sealed class SelectionRequirementEvaluator
{
    private readonly ExpressionInterpreter _interpreter = new();
    private readonly IReadOnlySet<string> _ownedIds;

    public SelectionRequirementEvaluator(SelectRule rule, IEnumerable<string> ownedIds)
    {
        _interpreter.InitializeWithSelectionRule(rule);
        _ownedIds = SelectionRequirementCompatibility.ExpandForSelectionValidation(ownedIds);
    }

    /// <summary>
    /// Null means the content expression could not be evaluated. New choices must require
    /// true; cleanup may preserve an existing choice when its validity is unknown.
    /// </summary>
    public bool? Evaluate(ElementBase candidate)
    {
        if (!candidate.HasRequirements) return true;
        try
        {
            return _interpreter.EvaluateElementRequirementsExpression(candidate.Requirements, _ownedIds);
        }
        catch (Exception ex)
        {
            Logger.Warning("could not evaluate prerequisites for {0}: {1}", candidate.Id, ex.Message);
            return null;
        }
    }
}
