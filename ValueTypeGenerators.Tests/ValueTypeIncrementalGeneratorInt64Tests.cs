using System.Globalization;
using System.Text.Json;

using Xunit;

namespace ValueTypeGenerators.Tests;

[ValueType(Type = TypeOfValue.Int64)]
public readonly partial struct Int64Id;

public sealed class ValueTypeIncrementalGeneratorInt64Tests {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        Converters = { Int64IdJsonConverter.Default }
    };

    [Fact]
    public void Empty() {
        Assert.Equal(0, (Int64)Int64Id.Empty);
    }

    [Fact]
    public void NewId() {
        var id1 = (Int64Id)123;
        Assert.NotEqual(Int64Id.Empty, id1);

        var id2 = (Int64Id)456;
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void From_Int64() {
        var a = 123;
        var b = (Int64Id)a;
        var c = (Int64)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(CultureInfo.InvariantCulture), b.ToString());
    }

    [Fact]
    public void To_Int64() {
        var a = (Int64Id)123;
        var b = (Int64)a;
        var c = (Int64Id)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(), b.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void From_Json() {
        var id = 123;
        var expected = JsonSerializer.Deserialize<Int64>($"{id}");
        var actual = JsonSerializer.Deserialize<Int64Id>($"{id}", SerializerOptions);
        Assert.Equal(expected, (Int64)actual);
    }

    [Fact]
    public void To_Json() {
        var id = (Int64Id)123;
        var expected = JsonSerializer.Serialize((Int64)id);
        var actual = JsonSerializer.Serialize(id, SerializerOptions);
        Assert.Equal(expected, actual);
    }
}
