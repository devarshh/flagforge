namespace FlagForge.Evaluation;

/// <summary>A clause with its values pre-parsed and indexed once, at snapshot compile time.</summary>
internal sealed class CompiledClause
{
    private readonly string _attribute;
    private readonly ClauseOperator _operator;
    private readonly bool _negate;
    private readonly string[] _values;
    private readonly HashSet<string> _stringValues;
    private readonly HashSet<decimal> _numberValues;
    private readonly bool _inTrue;
    private readonly bool _inFalse;
    private readonly decimal? _minNumber;
    private readonly decimal? _maxNumber;

    public CompiledClause(Clause clause)
    {
        _attribute = clause.Attribute;
        _operator = clause.Operator;
        _negate = clause.Negate;
        _values = [.. clause.Values];
        _stringValues = new HashSet<string>(_values, StringComparer.Ordinal);
        _numberValues = [];
        foreach (var text in _values)
        {
            if (NumberParsing.TryParse(text, out var number))
            {
                _numberValues.Add(number);
                _minNumber = _minNumber is null ? number : Math.Min(_minNumber.Value, number);
                _maxNumber = _maxNumber is null ? number : Math.Max(_maxNumber.Value, number);
            }

            _inTrue |= string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
            _inFalse |= string.Equals(text, "false", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool Matches(EvaluationContext context)
    {
        var value = context.GetAttribute(_attribute);
        if (_operator == ClauseOperator.Exists)
        {
            var present = value is not null;
            return _negate ? !present : present;
        }

        // A missing attribute never matches, even when the clause is negated.
        if (value is null)
        {
            return false;
        }

        var matched = value.Kind == AttributeValueKind.Array ? AnyItemMatches(value.Items) : ScalarMatches(value);
        return _negate ? !matched : matched;
    }

    private bool AnyItemMatches(IReadOnlyList<AttributeValue> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (ScalarMatches(items[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool ScalarMatches(AttributeValue value) => _operator switch
    {
        ClauseOperator.In => value.Kind switch
        {
            AttributeValueKind.String => _stringValues.Contains(value.StringValue!),
            AttributeValueKind.Number => _numberValues.Contains(value.NumberValue),
            AttributeValueKind.Boolean => value.BooleanValue ? _inTrue : _inFalse,
            _ => false,
        },
        ClauseOperator.Contains => value.Kind == AttributeValueKind.String && AnyString(value.StringValue!, static (s, v) => s.Contains(v, StringComparison.Ordinal)),
        ClauseOperator.StartsWith => value.Kind == AttributeValueKind.String && AnyString(value.StringValue!, static (s, v) => s.StartsWith(v, StringComparison.Ordinal)),
        ClauseOperator.EndsWith => value.Kind == AttributeValueKind.String && AnyString(value.StringValue!, static (s, v) => s.EndsWith(v, StringComparison.Ordinal)),

        // "Matches any value" reduces to a comparison with the largest (lt/lte) or smallest (gt/gte) value.
        ClauseOperator.Lt => value.Kind == AttributeValueKind.Number && value.NumberValue < _maxNumber,
        ClauseOperator.Lte => value.Kind == AttributeValueKind.Number && value.NumberValue <= _maxNumber,
        ClauseOperator.Gt => value.Kind == AttributeValueKind.Number && value.NumberValue > _minNumber,
        ClauseOperator.Gte => value.Kind == AttributeValueKind.Number && value.NumberValue >= _minNumber,
        _ => false,
    };

    private bool AnyString(string attribute, Func<string, string, bool> predicate)
    {
        foreach (var candidate in _values)
        {
            if (predicate(attribute, candidate))
            {
                return true;
            }
        }

        return false;
    }
}
