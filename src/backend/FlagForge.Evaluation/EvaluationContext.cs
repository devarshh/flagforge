namespace FlagForge.Evaluation;

/// <summary>The subject of an evaluation: a required key plus optional attributes (names are case-sensitive).</summary>
public sealed class EvaluationContext
{
    private static readonly Dictionary<string, AttributeValue> NoAttributes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, AttributeValue> _attributes;
    private readonly AttributeValue _keyValue;

    public EvaluationContext(string key, IReadOnlyDictionary<string, AttributeValue>? attributes = null)
        : this(key, attributes is null ? NoAttributes : new Dictionary<string, AttributeValue>(attributes, StringComparer.Ordinal))
    {
    }

    private EvaluationContext(string key, Dictionary<string, AttributeValue> attributes)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (attributes.ContainsKey(AttributeNames.Key))
        {
            throw new ArgumentException("'key' is reserved for the context key and cannot be an attribute.", nameof(attributes));
        }

        Key = key;
        _attributes = attributes;
        _keyValue = AttributeValue.FromString(key);
    }

    public string Key { get; }

    public IReadOnlyDictionary<string, AttributeValue> Attributes => _attributes;

    /// <summary>Looks up an attribute; <c>key</c> resolves to the context key. Returns null when missing.</summary>
    public AttributeValue? GetAttribute(string name) =>
        name == AttributeNames.Key ? _keyValue : _attributes.GetValueOrDefault(name);

    /// <summary>Creates a context that takes ownership of an already-validated, ordinal dictionary.</summary>
    internal static EvaluationContext FromValidated(string key, Dictionary<string, AttributeValue> attributes) =>
        new(key, attributes);
}
