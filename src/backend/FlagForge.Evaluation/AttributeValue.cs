namespace FlagForge.Evaluation;

/// <summary>
/// An immutable context attribute value: a string, a number, a boolean, or an array of those scalars.
/// Null values are not represented; a null attribute is treated exactly like a missing one.
/// </summary>
public sealed class AttributeValue
{
    public static readonly AttributeValue True = new(AttributeValueKind.Boolean, null, 0m, true, []);
    public static readonly AttributeValue False = new(AttributeValueKind.Boolean, null, 0m, false, []);

    private AttributeValue(
        AttributeValueKind kind, string? stringValue, decimal numberValue, bool booleanValue, AttributeValue[] items)
    {
        Kind = kind;
        StringValue = stringValue;
        NumberValue = numberValue;
        BooleanValue = booleanValue;
        Items = items;
    }

    public AttributeValueKind Kind { get; }

    /// <summary>The value when <see cref="Kind"/> is <see cref="AttributeValueKind.String"/>; otherwise null.</summary>
    public string? StringValue { get; }

    /// <summary>The value when <see cref="Kind"/> is <see cref="AttributeValueKind.Number"/>.</summary>
    public decimal NumberValue { get; }

    /// <summary>The value when <see cref="Kind"/> is <see cref="AttributeValueKind.Boolean"/>.</summary>
    public bool BooleanValue { get; }

    /// <summary>The scalar items when <see cref="Kind"/> is <see cref="AttributeValueKind.Array"/>; otherwise empty.</summary>
    public IReadOnlyList<AttributeValue> Items { get; }

    public static AttributeValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AttributeValue(AttributeValueKind.String, value, 0m, false, []);
    }

    public static AttributeValue FromNumber(decimal value) =>
        new(AttributeValueKind.Number, null, value, false, []);

    public static AttributeValue FromBoolean(bool value) => value ? True : False;

    public static AttributeValue FromArray(IEnumerable<AttributeValue> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var array = items.ToArray();
        foreach (var item in array)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(items));
            if (item.Kind == AttributeValueKind.Array)
            {
                throw new ArgumentException("Array attribute values may only contain scalars.", nameof(items));
            }
        }

        return new AttributeValue(AttributeValueKind.Array, null, 0m, false, array);
    }
}
