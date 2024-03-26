namespace ValueTypeGenerators.Sample;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct Int32Id;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct PersonId;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct StudentId;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct ManagerId;

[ValueType(Type = TypeOfValue.Int64)]
public readonly partial struct Int64Id;

[ValueType(Type = TypeOfValue.Int64)]
public readonly partial struct TimestampId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct GloballyUniqueId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct ProductId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct SubscriptionId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct MarketId;
