namespace ValueTypeGenerators.Sample;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct IntegerId;

[ValueType(Type = TypeOfValue.Guid)]
public readonly partial struct GloballyUniqueId;
