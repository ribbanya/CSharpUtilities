using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace Ribbanya.Reflection {
  [PublicAPI]
  public static class ReflectionHelper {
    private static readonly ConcurrentDictionary<Type, object> TypeDefaults = new ConcurrentDictionary<Type, object>();

    public static object GetDefaultValue(this Type type) {
      return type.IsValueType
        ? TypeDefaults.GetOrAdd(type, Activator.CreateInstance)
        : null;
    }

    public static object GetDefaultParameterValue(this ParameterInfo parameter) {
      return parameter.IsOptional
        ? parameter.HasDefaultValue
          ? parameter.DefaultValue
          : parameter.ParameterType.GetDefaultValue()
        : throw new ArgumentException($"Parameter {parameter} requires a value.");
    }

    public static bool IsSimpleType(this Type type) {
      return
        type.IsValueType ||
        type.IsPrimitive ||
        new[] {
          typeof(string),
          typeof(decimal),
          typeof(DateTime),
          typeof(DateTimeOffset),
          typeof(TimeSpan),
          typeof(Guid)
        }.Contains(type) ||
        Convert.GetTypeCode(type) != TypeCode.Object;
    }
  }
}