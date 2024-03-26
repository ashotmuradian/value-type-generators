using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ValueTypeGenerators;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class ValueTypeAttribute : Attribute {
    public const TypeOfValue DefaultType = TypeOfValue.Guid;
    public const CastOperator DefaultCastOperator = CastOperator.Explicit;

    public TypeOfValue Type { get; set; } = DefaultType;
    public CastOperator CastOperator { get; set; } = DefaultCastOperator;
}

[SuppressMessage("Design", "CA1008: Enums should have zero value", Justification = "Except this case")]
[SuppressMessage("Naming", "CA1720: Identifier contains type name", Justification = "Exactly what is necessary")]
public enum TypeOfValue {
    Guid = 1,
    Int32 = 2,
    Int64 = 3
}

[SuppressMessage("Design", "CA1008: Enums should have zero value", Justification = "Except this case")]
[SuppressMessage("Naming", "CA1720: Identifier contains type name", Justification = "Exactly what is necessary")]
internal enum IntegerType : ulong {
    // Int16 = (ulong)short.MaxValue,
    Int32 = int.MaxValue,
    Int64 = long.MaxValue,
    // Int128 = (System.Int128).MaxValue,
    // UInt16 = ushort.MaxValue,
    // UInt32 = uint.MaxValue,
    // UInt64 = ulong.MaxValue,
    // UInt128 = (System.UInt128).MaxValue,
}

[SuppressMessage("Design", "CA1008: Enums should have zero value", Justification = "Except this case")]
[SuppressMessage("Naming", "CA1720: Identifier contains type name", Justification = "Exactly what is necessary")]
public enum CastOperator {
    Implicit = 1,
    Explicit = 2
}

internal sealed class TargetProject {
    public TargetProject(string @namespace, bool hasEfCoreReference, bool hasJsonReference) {
        Namespace = @namespace;
        HasEfCoreReference = hasEfCoreReference;
        HasJsonReference = hasJsonReference;
    }
    
    public string Namespace { get; }
    public bool HasEfCoreReference { get; }
    public bool HasJsonReference { get; }
}

internal sealed class TargetItem {
    public string Namespace { get; }
    public string Name { get; }
    public TypeOfValue Type { get; }
    public CastOperator CastOperator { get; }
    public Location Location { get; }
    public bool IsCorrectlyDeclared { get; }

    public TargetItem(
        string @namespace,
        string name,
        TypeOfValue type,
        CastOperator castOperator,
        Location location,
        bool isCorrectlyDeclared
    ) {
        Namespace = @namespace;
        Name = name;
        Type = type;
        CastOperator = castOperator;
        Location = location;
        IsCorrectlyDeclared = isCorrectlyDeclared;
    }
}

[Generator]
public sealed class ValueTypeIncrementalGenerator : IIncrementalGenerator {
    private static readonly Type ValueTypeAttributeType = typeof(ValueTypeAttribute);

    private static readonly DiagnosticDescriptor ImproperDeclarationError = new(
        id: "VT0001",
        title: "Value type must be non-nested 'readonly partial struct'",
        messageFormat: "Value type '{0}' must be non-nested 'readonly partial struct'",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var targetProject = context.CompilationProvider.Select(static (x, _) => {
            return new TargetProject(
                @namespace: x.AssemblyName ?? string.Empty,
                hasEfCoreReference: x.ReferencedAssemblyNames.Any(static a => a.Name == "Microsoft.EntityFrameworkCore"),
                hasJsonReference: x.ReferencedAssemblyNames.Any(static a => a.Name == "System.Text.Json")
            );
        });

        var targetItems = context.SyntaxProvider.ForAttributeWithMetadataName(
            ValueTypeAttributeType.FullName!,
            static (x, _) => x is TypeDeclarationSyntax,
            static (x, _) => {
                var attr = x.Attributes.First(static a => a.AttributeClass?.ToString() == ValueTypeAttributeType.FullName);
                var type = (TypeOfValue)(attr.NamedArguments.FirstOrDefault(static a => a.Key == nameof(ValueTypeAttribute.Type)).Value.Value ?? ValueTypeAttribute.DefaultType);
                var castOperator = (CastOperator)(attr.NamedArguments.FirstOrDefault(static a => a.Key == nameof(ValueTypeAttribute.CastOperator)).Value.Value ?? ValueTypeAttribute.DefaultCastOperator);
                return new TargetItem(
                    name: x.TargetSymbol.Name,
                    type: type,
                    castOperator: castOperator,
                    @namespace: x.TargetSymbol.ContainingNamespace.IsGlobalNamespace
                        ? string.Empty
                        : x.TargetSymbol.ContainingNamespace.ToString(),
                    location: x.TargetNode.GetLocation(),
                    isCorrectlyDeclared: x.TargetNode is StructDeclarationSyntax s &&
                                         x.TargetSymbol.ContainingSymbol is INamespaceSymbol &&
                                         s.Modifiers.Any(SyntaxKind.PartialKeyword) &&
                                         s.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
                );
            }
        );

        context.RegisterSourceOutput(targetItems.Combine(targetProject), static (context, item) => {
            var target = item.Left;
            var targetProject = item.Right;
            if (target.IsCorrectlyDeclared) {
                if (target.Type == TypeOfValue.Guid) {
                    context.AddSource($"{target.Name}.g.cs", Source(target.Name, target.Namespace, target.CastOperator));
                } else {
                    context.AddSource($"{target.Name}.g.cs", SourceInteger(target.Name, target.Namespace, target.CastOperator, ToIntegerType(target.Type)));
                }

                if (targetProject.HasJsonReference) {
                    if (target.Type == TypeOfValue.Guid) {
                        context.AddSource($"{target.Name}JsonConverter.g.cs", JsonConverterSource(target.Name, target.Namespace));
                    } else {
                        context.AddSource($"{target.Name}JsonConverter.g.cs", JsonConverterSourceInteger(target.Name, target.Namespace, ToIntegerType(target.Type)));
                    }
                }

                if (targetProject.HasEfCoreReference) {
                    if (target.Type == TypeOfValue.Guid) {
                        context.AddSource($"{target.Name}ValueConverter.g.cs", ValueConverterSource(target.Name, target.Namespace));
                    } else {
                        context.AddSource($"{target.Name}ValueConverter.g.cs", ValueConverterSourceInteger(target.Name, target.Namespace, ToIntegerType(target.Type)));
                    }

                    context.AddSource($"{target.Name}ValueComparer.g.cs", ValueComparerSource(target.Name, target.Namespace));
                }
            } else {
                context.ReportDiagnostic(Diagnostic.Create(ImproperDeclarationError, target.Location, target.Name));
            }

            static IntegerType ToIntegerType(TypeOfValue type) {
                return type switch {
                    TypeOfValue.Int32 => IntegerType.Int32,
                    TypeOfValue.Int64 => IntegerType.Int64,
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
                };
            }
        });

        context.RegisterSourceOutput(targetItems.Collect().Combine(targetProject), static (context, item) => {
            var target = item.Left;
            var targetProject = item.Right;
            if (target.All(static x => x.IsCorrectlyDeclared)) {
                var names = target.Select(static x => $"{x.Namespace}.{x.Name}").ToArray();

                if (targetProject.HasEfCoreReference && names.Length != 0) {
                    context.AddSource("ValueTypeConventionExtensions.g.cs", EfCoreConventionExtensionsSource(names, targetProject.Namespace));
                }
            }
        });

        static string Source(string name, string @namespace, CastOperator castOperator) {
            var castOperatorType = castOperator == CastOperator.Implicit ? "implicit" : "explicit";

            return
                $$"""
                  using System;
                  using System.Diagnostics.CodeAnalysis;
                  using System.Numerics;
                  using System.Runtime.CompilerServices;
                  using System.Runtime.InteropServices;
                  using System.Text.Json.Serialization;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  [JsonConverter(typeof({{name}}JsonConverter))]
                  [Serializable]
                  [StructLayout(LayoutKind.Explicit)]
                  readonly partial struct {{name}}
                      : IEquatable<{{name}}>,
                        IComparable,
                        IComparable<{{name}}>,
                        IComparisonOperators<{{name}}, {{name}}, bool>,
                        ISpanParsable<{{name}}>,
                        ISpanFormattable,
                        IUtf8SpanFormattable
                  {
                      private static readonly {{name}} _empty = default;
                      public static ref readonly {{name}} Empty => ref _empty;
                  
                      [FieldOffset(0)]
                      private readonly Guid _value;
                      
                      /// <inheritdoc cref="Guid.NewGuid" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} New{{name}}()
                      {
                          var g = Guid.NewGuid();
                          return Unsafe.As<Guid, {{name}}>(ref g);
                      }
                  
                      /// <inheritdoc cref="Guid.ToString()" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public override string ToString() => _value.ToString();
                  
                      /// <inheritdoc cref="Guid.GetHashCode" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public override int GetHashCode() => _value.GetHashCode();
                  
                      /// <inheritdoc cref="Guid.Equals(object?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public override bool Equals([NotNullWhen(true)] object? o) => _value.Equals(o);
                      
                      /// <inheritdoc cref="ISpanFormattable.TryFormat" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format)
                      {
                          return _value.TryFormat(destination, out charsWritten, format);
                      }
                      
                      /// <inheritdoc cref="ISpanFormattable.TryFormat" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format, IFormatProvider? provider)
                      {
                          return _value.TryFormat(destination, out charsWritten, format);
                      }
                      
                      /// <inheritdoc cref="IFormattable.ToString(string?,System.IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public string ToString([StringSyntax(StringSyntaxAttribute.GuidFormat)] string? format)
                      {
                          return _value.ToString(format, null);
                      }
                      
                      /// <inheritdoc cref="IFormattable.ToString(string?,System.IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public string ToString([StringSyntax(StringSyntaxAttribute.GuidFormat)] string? format, IFormatProvider? formatProvider)
                      {
                          return _value.ToString(format, formatProvider);
                      }
                      
                      /// <inheritdoc cref="IComparable.CompareTo" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public int CompareTo(object? obj)
                      {
                          return _value.CompareTo(obj);
                      }
                      
                      /// <inheritdoc cref="IComparable{T}.CompareTo" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public int CompareTo({{name}} other)
                      {
                          return _value.CompareTo(other._value);
                      }
                      
                      /// <inheritdoc cref="IEquatable{T}.Equals(T?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool Equals({{name}} other)
                      {
                          return _value.Equals(other._value);
                      }
                      
                      /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(System.ReadOnlySpan{char},System.IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(ReadOnlySpan<char> s)
                      {
                          var g = Guid.Parse(s, null);
                          return Unsafe.As<Guid, {{name}}>(ref g);
                      }
                      
                      /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(System.ReadOnlySpan{char},System.IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
                      {
                          var g = Guid.Parse(s, provider);
                          return Unsafe.As<Guid, {{name}}>(ref g);
                      }
                      
                      /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(System.ReadOnlySpan{char},System.IFormatProvider?,out TSelf)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse(ReadOnlySpan<char> s, out {{name}} result)
                      {
                          var r = Guid.TryParse(s, null, out var g);
                          result = Unsafe.As<Guid, {{name}}>(ref g);
                          return r;
                      }
                      
                      /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(System.ReadOnlySpan{char},System.IFormatProvider?,out TSelf)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out {{name}} result)
                      {
                          var r = Guid.TryParse(s, provider, out var g);
                          result = Unsafe.As<Guid, {{name}}>(ref g);
                          return r;
                      }
                      
                      /// <inheritdoc cref="IParsable{TSelf}.Parse" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(string s)
                      {
                          var g = Guid.Parse(s, null);
                          return Unsafe.As<Guid, {{name}}>(ref g);
                      }
                      
                      /// <inheritdoc cref="IParsable{TSelf}.Parse" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(string s, IFormatProvider? provider)
                      {
                          var g = Guid.Parse(s, provider);
                          return Unsafe.As<Guid, {{name}}>(ref g);
                      }
                      
                      /// <inheritdoc cref="IParsable{TSelf}.TryParse" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse([NotNullWhen(true)] string? s, out {{name}} result)
                      {
                          var r = Guid.TryParse(s, null, out var g);
                          result = Unsafe.As<Guid, {{name}}>(ref g);
                          return r;
                      }
                      
                      /// <inheritdoc cref="IParsable{TSelf}.TryParse" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out {{name}} result)
                      {
                          var r = Guid.TryParse(s, provider, out var g);
                          result = Unsafe.As<Guid, {{name}}>(ref g);
                          return r;
                      }
                      
                      /// <inheritdoc cref="IUtf8SpanFormattable.TryFormat" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format)
                      {
                          return _value.TryFormat(utf8Destination, out bytesWritten, format);
                      }
                      
                      /// <inheritdoc cref="IUtf8SpanFormattable.TryFormat" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.GuidFormat)] ReadOnlySpan<char> format, IFormatProvider? provider)
                      {
                          return _value.TryFormat(utf8Destination, out bytesWritten, format);
                      }
                      
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator {{name}}(Guid g) => Unsafe.As<Guid, {{name}}>(ref g);
                  
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator Guid({{name}} i) => i._value;
                  
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator {{name}}(Int128 i) => Unsafe.As<Int128, {{name}}>(ref i);
                  
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator Int128({{name}} i) => Unsafe.As<{{name}}, Int128>(ref i);
                  
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator {{name}}(UInt128 i) => Unsafe.As<UInt128, {{name}}>(ref i);
                  
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator UInt128({{name}} i) => Unsafe.As<{{name}}, UInt128>(ref i);
                  
                      /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Equality(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator ==({{name}} left, {{name}} right)
                      {
                          return left._value == right._value;
                      }
                  
                      /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Inequality(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator !=({{name}} left, {{name}} right)
                      {
                          return left._value != right._value;
                      }
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_GreaterThan(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator >({{name}} left, {{name}} right)
                      {
                          return left._value > right._value;
                      }
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator >=({{name}} left, {{name}} right)
                      {
                          return left._value >= right._value;
                      }
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_LessThan(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator <({{name}} left, {{name}} right)
                      {
                          return left._value < right._value;
                      }
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_LessThanOrEqual(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator <=({{name}} left, {{name}} right)
                      {
                          return left._value <= right._value;
                      }
                  }
                  """;
        }

        static string SourceInteger(string name, string @namespace, CastOperator castOperator, IntegerType integerType) {
            var castOperatorType = castOperator == CastOperator.Implicit ? "implicit" : "explicit";

            var type = integerType.ToString("G");

            return
                $$"""
                  using System;
                  using System.Diagnostics.CodeAnalysis;
                  using System.Numerics;
                  using System.Runtime.CompilerServices;
                  using System.Runtime.InteropServices;
                  using System.Text.Json.Serialization;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  [JsonConverter(typeof({{name}}JsonConverter))]
                  [Serializable]
                  [StructLayout(LayoutKind.Explicit)]
                  readonly partial struct {{name}}
                      : IEquatable<{{name}}>,
                        IComparable,
                        IComparable<{{name}}>,
                        IComparisonOperators<{{name}}, {{name}}, bool>,
                        ISpanParsable<{{name}}>,
                        IUtf8SpanParsable<{{name}}>,
                        ISpanFormattable,
                        IUtf8SpanFormattable
                  {
                      private static readonly {{name}} _empty = default;
                      public static ref readonly {{name}} Empty => ref _empty;
                  
                      [FieldOffset(0)]
                      private readonly {{type}} _value;
                  
                      /// <inheritdoc cref="{{type}}.ToString()" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public override string ToString()
                          => _value.ToString();
                  
                      /// <inheritdoc cref="{{type}}.GetHashCode()" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public override int GetHashCode()
                          => _value.GetHashCode();
                  
                      /// <inheritdoc cref="{{type}}.Equals(object?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public override bool Equals([NotNullWhen(true)] object? obj)
                          => _value.Equals(obj);
                  
                      /// <inheritdoc cref="IEquatable{T}.Equals(T)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool Equals({{name}} other)
                          => _value.Equals(other._value);
                  
                      /// <inheritdoc cref="IComparable.CompareTo(object?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public int CompareTo(object? obj)
                          => _value.CompareTo(obj);
                  
                      /// <inheritdoc cref="IComparable{T}.CompareTo(T)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public int CompareTo({{name}} other)
                          => _value.CompareTo(other._value);
                  
                      /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(ReadOnlySpan<char> s)
                      {
                          var i = {{type}}.Parse(s, null);
                          return Unsafe.As<{{type}}, {{name}}>(ref i);
                      }
                  
                      /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
                      {
                          var i = {{type}}.Parse(s, provider);
                          return Unsafe.As<{{type}}, {{name}}>(ref i);
                      }
                  
                      /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse(ReadOnlySpan<char> s, out {{name}} result)
                      {
                          var r = {{type}}.TryParse(s, null, out var i);
                          result = Unsafe.As<{{type}}, {{name}}>(ref i);
                          return r;
                      }
                  
                      /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out {{name}} result)
                      {
                          var r = {{type}}.TryParse(s, provider, out var i);
                          result = Unsafe.As<{{type}}, {{name}}>(ref i);
                          return r;
                      }
                      
                      /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.Parse(ReadOnlySpan{byte}, IFormatProvider?)" />
                      public static {{name}} Parse(ReadOnlySpan<byte> utf8Text)
                      {
                          var i = {{type}}.Parse(utf8Text, null);
                          return Unsafe.As<{{type}}, {{name}}>(ref i);
                      }
                      
                      /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.Parse(ReadOnlySpan{byte}, IFormatProvider?)" />
                      public static {{name}} Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
                      {
                          var i = {{type}}.Parse(utf8Text, provider);
                          return Unsafe.As<{{type}}, {{name}}>(ref i);
                      }
                  
                      /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.TryParse(ReadOnlySpan{byte}, IFormatProvider?, out TSelf)" />
                      public static bool TryParse(ReadOnlySpan<byte> utf8Text, out {{name}} result)
                      {
                          var r = {{type}}.TryParse(utf8Text, null, out var i);
                          result = Unsafe.As<{{type}}, {{name}}>(ref i);
                          return r;
                      }
                  
                      /// <inheritdoc cref="IUtf8SpanParsable{TSelf}.TryParse(ReadOnlySpan{byte}, IFormatProvider?, out TSelf)" />
                      public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out {{name}} result)
                      {
                          var r = {{type}}.TryParse(utf8Text, provider, out var i);
                          result = Unsafe.As<{{type}}, {{name}}>(ref i);
                          return r;
                      }
                  
                      /// <inheritdoc cref="IParsable{TSelf}.Parse(string, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(string s)
                      {
                          var i = {{type}}.Parse(s, null);
                          return Unsafe.As<{{type}}, {{name}}>(ref i);
                      }
                  
                      /// <inheritdoc cref="IParsable{TSelf}.Parse(string, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{name}} Parse(string s, IFormatProvider? provider)
                      {
                          var i = {{type}}.Parse(s, provider);
                          return Unsafe.As<{{type}}, {{name}}>(ref i);
                      }
                  
                      /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse([NotNullWhen(true)] string? s, out {{name}} result)
                      {
                          var r = {{type}}.TryParse(s, null, out var i);
                          result = Unsafe.As<{{type}}, {{name}}>(ref i);
                          return r;
                      }
                  
                      /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out {{name}} result)
                      {
                          var r = {{type}}.TryParse(s, provider, out var i);
                          result = Unsafe.As<{{type}}, {{name}}>(ref i);
                          return r;
                      }
                  
                      /// <inheritdoc cref="IFormattable.ToString(string?, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format)
                          => _value.ToString(format, null);
                  
                      /// <inheritdoc cref="IFormattable.ToString(string?, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format, IFormatProvider? formatProvider)
                          => _value.ToString(format, formatProvider);
                  
                      /// <inheritdoc cref="ISpanFormattable.TryFormat(Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format)
                          => _value.TryFormat(destination, out charsWritten, format);
                  
                      /// <inheritdoc cref="ISpanFormattable.TryFormat(Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format, IFormatProvider? provider)
                          => _value.TryFormat(destination, out charsWritten, format, provider);
                  
                      /// <inheritdoc cref="IUtf8SpanFormattable.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format)
                          => _value.TryFormat(utf8Destination, out bytesWritten, format);
                  
                      /// <inheritdoc cref="IUtf8SpanFormattable.TryFormat(Span{byte}, out int, ReadOnlySpan{char}, IFormatProvider?)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format, IFormatProvider? provider)
                          => _value.TryFormat(utf8Destination, out bytesWritten, format, provider);
                      
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator {{name}}({{type}} i) => Unsafe.As<{{type}}, {{name}}>(ref i);
                  
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static {{castOperatorType}} operator {{type}}({{name}} i) => i._value;
                  
                      /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Equality(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator ==({{name}} left, {{name}} right)
                          => left._value == right._value;
                  
                      /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Inequality(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator !=({{name}} left, {{name}} right)
                          => left._value != right._value;
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_GreaterThan(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator >({{name}} left, {{name}} right)
                          => left._value > right._value;
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator >=({{name}} left, {{name}} right)
                          => left._value >= right._value;
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_LessThan(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator <({{name}} left, {{name}} right)
                          => left._value < right._value;
                  
                      /// <inheritdoc cref="IComparisonOperators{TSelf,TOther,TResult}.op_LessThanOrEqual(TSelf, TOther)" />
                      [MethodImpl(MethodImplOptions.AggressiveInlining)]
                      public static bool operator <=({{name}} left, {{name}} right)
                          => left._value <= right._value;
                  }
                  """;
        }

        static string ValueConverterSource(string name, string @namespace) {
            return
                $$"""
                  using System;
                  using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  public sealed class {{name}}ValueConverter() : ValueConverter<{{name}}, Guid>
                  (
                      static id => (Guid)id,
                      static id => ({{name}})id
                  )
                  {
                      public static readonly {{name}}ValueConverter Default = new();
                  }
                  """;
        }

        static string ValueConverterSourceInteger(string name, string @namespace, IntegerType integerType) {
            var type = integerType.ToString("G");
            
            return
                $$"""
                  using System;
                  using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  public sealed class {{name}}ValueConverter() : ValueConverter<{{name}}, {{type}}>
                  (
                      static id => ({{type}})id,
                      static id => ({{name}})id
                  )
                  {
                      public static readonly {{name}}ValueConverter Default = new();
                  }
                  """;
        }

        static string ValueComparerSource(string name, string @namespace) {
            return
                $$"""
                  using System;
                  using Microsoft.EntityFrameworkCore.ChangeTracking;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  public sealed class {{name}}ValueComparer() : ValueComparer<{{name}}>
                  (
                      static (a, b) => a.Equals(b),
                      static id => id.GetHashCode(),
                      static id => id
                  )
                  {
                      public static readonly {{name}}ValueComparer Default = new();
                  }
                  """;
        }

        static string JsonConverterSource(string name, string @namespace) {
            return
                $$"""
                  using System;
                  using System.Runtime.CompilerServices;
                  using System.Text.Json.Serialization;
                  using System.Text.Json;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  public sealed class {{name}}JsonConverter : JsonConverter<{{name}}>
                  {
                      public static readonly {{name}}JsonConverter Default = new();
                      
                      public override {{name}} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                      {
                          var g = reader.GetGuid();
                          return Unsafe.As<Guid, {{name}}>(ref g);
                      }
                      
                      public override void Write(Utf8JsonWriter writer, {{name}} value, JsonSerializerOptions options)
                      {
                          writer.WriteStringValue(Unsafe.As<{{name}}, Guid>(ref value));
                      }
                  }
                  """;
        }

        static string JsonConverterSourceInteger(string name, string @namespace, IntegerType integerType) {
            var type = integerType.ToString("G");
            
            return
                $$"""
                  using System;
                  using System.Runtime.CompilerServices;
                  using System.Text.Json.Serialization;
                  using System.Text.Json;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  public sealed class {{name}}JsonConverter : JsonConverter<{{name}}>
                  {
                      public static readonly {{name}}JsonConverter Default = new();
                      
                      public override {{name}} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                      {
                          var i = reader.Get{{type}}();
                          return Unsafe.As<{{type}}, {{name}}>(ref i);
                      }
                      
                      public override void Write(Utf8JsonWriter writer, {{name}} value, JsonSerializerOptions options)
                      {
                          writer.WriteNumberValue(Unsafe.As<{{name}}, {{type}}>(ref value));
                      }
                  }
                  """;
        }

        static string EfCoreConventionExtensionsSource(string[] names, string @namespace) {
            return
                $$"""
                  using System;
                  using Microsoft.EntityFrameworkCore;

                  #nullable enable

                  {{(string.IsNullOrWhiteSpace(@namespace) ? "// global namespace" : $"namespace {@namespace};")}}

                  public static class ValueTypeConventionExtensions
                  {
                      public static void AddValueTypeConversions(this ModelConfigurationBuilder config)
                      {
                  {{string.Join("\n", names.Select((static name => $"        config.Properties<{name}>().HaveConversion<{name}ValueConverter, {name}ValueComparer>();")))}}
                      }
                  }
                  """;
        }
    }
}
