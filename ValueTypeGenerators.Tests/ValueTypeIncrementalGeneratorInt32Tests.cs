using System.Globalization;
using System.Text.Json;

using Xunit;

namespace ValueTypeGenerators.Tests;

[ValueType(Type = TypeOfValue.Int32)]
public readonly partial struct IntegerId;

public sealed class ValueTypeIncrementalGeneratorInt32Tests {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        Converters = { IntegerIdJsonConverter.Default }
    };

    [Fact]
    public void Empty() {
        Assert.Equal(0, (Int32)IntegerId.Empty);
    }

    [Fact]
    public void NewId() {
        var id1 = (IntegerId)123;
        Assert.NotEqual(IntegerId.Empty, id1);

        var id2 = (IntegerId)456;
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void From_Int32() {
        var a = 123;
        var b = (IntegerId)a;
        var c = (Int32)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(CultureInfo.InvariantCulture), b.ToString());
    }

    [Fact]
    public void To_Int32() {
        var a = (IntegerId)123;
        var b = (Int32)a;
        var c = (IntegerId)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(), b.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void From_Json() {
        var id = 123;
        var expected = JsonSerializer.Deserialize<Int32>($"{id}");
        var actual = JsonSerializer.Deserialize<IntegerId>($"{id}", SerializerOptions);
        Assert.Equal(expected, (Int32)actual);
    }

    [Fact]
    public void To_Json() {
        var id = (IntegerId)123;
        var expected = JsonSerializer.Serialize((Int32)id);
        var actual = JsonSerializer.Serialize(id, SerializerOptions);
        Assert.Equal(expected, actual);
    }
}
