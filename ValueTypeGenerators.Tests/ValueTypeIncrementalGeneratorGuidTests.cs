using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Xunit;

namespace ValueTypeGenerators.Tests;

[ValueType]
public readonly partial struct GloballyUniqueId;

public sealed class ValueTypeIncrementalGeneratorGuidTests {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        Converters = { GloballyUniqueIdJsonConverter.Default }
    };

    [Fact]
    public void Empty() {
        Assert.Equal(Guid.Empty, (Guid)GloballyUniqueId.Empty);
        Assert.Equal(0, (Int128)GloballyUniqueId.Empty);
        Assert.Equal(0u, (UInt128)GloballyUniqueId.Empty);
    }

    [Fact]
    public void NewGloballyUniqueId() {
        var id1 = GloballyUniqueId.NewGloballyUniqueId();
        Assert.NotEqual(GloballyUniqueId.Empty, id1);

        var id2 = GloballyUniqueId.NewGloballyUniqueId();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void From_Guid() {
        var a = Guid.NewGuid();
        var b = (GloballyUniqueId)a;
        var c = (Guid)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(), b.ToString());
    }

    [Fact]
    public void To_Guid() {
        var a = GloballyUniqueId.NewGloballyUniqueId();
        var b = (Guid)a;
        var c = (GloballyUniqueId)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(), b.ToString());
    }

    [Fact]
    public void From_Int128() {
        var a = Int128.MaxValue;
        var b = (GloballyUniqueId)a;
        var c = (Int128)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(CultureInfo.InvariantCulture), Unsafe.As<GloballyUniqueId, Int128>(ref b).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void To_Int128() {
        var a = GloballyUniqueId.NewGloballyUniqueId();
        var b = (Int128)a;
        var c = (GloballyUniqueId)b;
        Assert.Equal(a, c);
        Assert.Equal(Unsafe.As<GloballyUniqueId, Int128>(ref a).ToString(CultureInfo.InvariantCulture), b.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void From_UInt128() {
        var a = UInt128.MaxValue;
        var b = (GloballyUniqueId)a;
        var c = (UInt128)b;
        Assert.Equal(a, c);
        Assert.Equal(a.ToString(CultureInfo.InvariantCulture), Unsafe.As<GloballyUniqueId, UInt128>(ref b).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void To_UInt128() {
        var a = GloballyUniqueId.NewGloballyUniqueId();
        var b = (UInt128)a;
        var c = (GloballyUniqueId)b;
        Assert.Equal(a, c);
        Assert.Equal(Unsafe.As<GloballyUniqueId, UInt128>(ref a).ToString(CultureInfo.InvariantCulture), b.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void From_Json() {
        var id = Guid.NewGuid();
        var expected = JsonSerializer.Deserialize<Guid>($"\"{id}\"");
        var actual = JsonSerializer.Deserialize<GloballyUniqueId>($"\"{id}\"", SerializerOptions);
        Assert.Equal(expected, (Guid)actual);
    }

    [Fact]
    public void To_Json() {
        var id = GloballyUniqueId.NewGloballyUniqueId();
        var expected = JsonSerializer.Serialize((Guid)id);
        var actual = JsonSerializer.Serialize(id, SerializerOptions);
        Assert.Equal(expected, actual);
    }
}
