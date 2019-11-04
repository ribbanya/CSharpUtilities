using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace Ribbanya.Utilities.Reflection {
  [PublicAPI]
  public static class ReflectionHelper {
    private static readonly ConcurrentDictionary<Type, object> TypeDefaults = new ConcurrentDictionary<Type, object>();

    public static string GetFullName(this MemberInfo @this, string delimiter = ".") {
      var declaringType = @this.DeclaringType;
      var name = @this.Name;
      return declaringType != null
        ? $"{declaringType.FullName}{delimiter}{name}"
        : name;
    }

    public static object GetDefaultValue(this Type @this) {
      return @this.IsValueType
        ? TypeDefaults.GetOrAdd(@this, Activator.CreateInstance)
        : null;
    }

    public static T GetDefaultValue<T>(this Type @this) {
      return (T) GetDefaultValue(@this);
    }

    public static object GetDefaultValue(this ParameterInfo @this) {
      return @this.IsOptional
        ? @this.HasDefaultValue
          ? @this.DefaultValue
          : @this.ParameterType.GetDefaultValue()
        : throw new ArgumentException($"Parameter {@this} requires a value.");
    }

    public static T GetDefaultValue<T>(this ParameterInfo @this) {
      return (T) GetDefaultValue(@this);
    }

    public static bool TryGetDefaultValue(this ParameterInfo @this, out object value) {
      if (@this.IsActuallyOptional()) {
        value = @this.DefaultValue;
        return true;
      }

      value = @this.ParameterType.GetDefaultValue();
      return false;
    }

    public static bool IsActuallyOptional(this ParameterInfo @this) {
      return @this.IsOptional && @this.HasDefaultValue;
    }

    public static bool IsSimpleType(this Type @this) {
      return
        @this.IsValueType
        || @this.IsPrimitive
        || new[] {
          typeof(string),
          typeof(decimal),
          typeof(DateTime),
          typeof(DateTimeOffset),
          typeof(TimeSpan),
          typeof(Guid)
        }.Contains(@this)
        || Convert.GetTypeCode(@this) != TypeCode.Object;
    }
  }
}