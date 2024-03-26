# Value Type Generators

### Installation

```shell
dotnet add package ValueTypeGenerators
```

### How to generate

```csharp
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
```

The property `Type` of `ValueType` attribute has a default value of `TypeOfValue.Guid`,
so the `ProductId` above and other `Guid`-based types could be defined as:

```csharp
[ValueType]
public readonly partial struct ProductId;
```

### What's Generated

Below is the simplified version of what's generated.

```csharp
// Value Type
// There is no Value property; instead, casting operators should be used
[StructLayout(LayoutKind.Explicit)]
struct ProductId {
    [FieldOffset(0)]
    private readonly Guid _value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ProductId(Guid g) => Unsafe.As<Guid, ProductId>(ref g);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Guid(ProductId id) => id._value;
}

// System.Text.Json Converter
class ProductIdJsonConverter : JsonConverter<ProductId>;

// Entity Framework Core Value Comparer
class ProductIdValueComparer : ValueComparer<ProductId>;

// Entity Framework Core Value Converter
class ProductIdValueConverter : ValueConverter<ProductId, Guid>;

// Entity Framework Core Conventions
static class ValueTypeConventionExtensions {
    public static void AddValueTypeConversions(this ModelConfigurationBuilder config);
}
```

### Usage

```csharp
[ValueType]
readonly partial struct ProductId;

record Product {
    public ProductId Id { get; }
}
```

Casting is the only way to get the value.

```csharp
var newProductId = ProductId.NewProductId(); // equivalent to casting Guid.NewGuid() to ProductId
var newProductIdAsGuid = (Guid)newProductId;
var anotherProductId = (ProductId)Guid.NewGuid();

var personId = (PersonId)123;
var personIdAsInt = (int)personId;
```

#### Entity Framework Core
```csharp
class DeliveryDbContext : DbContext {
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.AddValueTypeConversions();
    }
    
    protected override void OnModelCreating(ModelBuilder builder) {
        base.OnModelCreating(builder);

        builder.Entity<Product>(static cfg => {
            cfg.HasKey(static x => x.Id);
            cfg.Property(static x => x.Id).ValueGeneratedOnAdd();
        });
    }
}
```

#### ASP.NET Core
```csharp
app.MapGet("/products/{id}", static ([FromRoute] ProductId id) => {
    // ...
});

app.MapGet("/people/{id}", static ([FromRoute] PersonId id) => {
    // ...
});

app.MapGet("/time/{id}", static ([FromRoute] TimestampId id) => {
    // ...
});
```