namespace ValueTypeGenerators.Sample;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct IntegerId;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct PersonId;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct StudentId;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct ManagerId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct GloballyUniqueId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct ProductId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct SubscriptionId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct MarketId;
