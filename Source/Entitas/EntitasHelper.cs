using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DesperateDevs.Utils;
using Entitas;
using Entitas.CodeGeneration.Attributes;

// ReSharper disable MemberCanBePrivate.Global

namespace TeamSalvato.Entitas {
  public static class EntitasHelper {
    public static void BindComponent(this IEntity @this, int index, object value, bool replace = false) {
      var valueType = value.GetType();
      var componentType = @this.contextInfo.componentTypes[index];
      var valueMembers = GetPublicMemberInfo(valueType, PublicMemberFilters.MustRead);
      var valueMap = valueMembers.ToDictionary(info => info.name, info => info);
      var componentInfo = GetPublicMemberInfo(componentType, PublicMemberFilters.MustWrite);
      var component = @this.CreateComponent(index, componentType);

      //TODO: Throw exception if fields don't match
      if (valueMap.Count <= 0 || Type.GetTypeCode(valueType) != TypeCode.Object)
        componentInfo[0].SetValue(component, value);
      else
        foreach (var destination in componentInfo) {
          var source = valueMap[destination.name];
          destination.SetValue(component, source.GetValue(value));
        }

      if (replace) @this.ReplaceComponent(index, component);
      else @this.AddComponent(index, component);
    }

    public static List<PublicMemberInfo> GetPublicMemberInfo(this Type type,
                                                             PublicMemberFilters filter = PublicMemberFilters.Any) {
      const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
      var fields = type.GetFields(flags);
      var properties = type.GetProperties(flags);
      var result = new List<PublicMemberInfo>(fields.Length + properties.Length);

      var mustRead = (filter & PublicMemberFilters.MustRead) != 0;
      var mustWrite = (filter & PublicMemberFilters.MustWrite) != 0;
      result.AddRange(from field in fields
        where !mustWrite || !field.IsInitOnly && !field.IsLiteral
        select new PublicMemberInfo(field));
      result.AddRange(from property in properties
        where property.GetIndexParameters().Length == 0
        where !mustRead || property.CanRead && property.GetGetMethod() != null
        where !mustWrite || property.CanWrite && property.GetSetMethod() != null
        select new PublicMemberInfo(property));

      return result;
    }

    public static bool IsEntityIndex(Type type, EntityIndexType kind, string specificContext = null) {
      if (!typeof(IComponent).IsAssignableFrom(type)) return false;

      Type indexType;
      switch (kind) {
        case EntityIndexType.EntityIndex:
          indexType = typeof(EntityIndexAttribute);
          break;
        case EntityIndexType.PrimaryEntityIndex:
          indexType = typeof(PrimaryEntityIndexAttribute);
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
      }

      return type.GetPublicMemberInfo(PublicMemberFilters.MustReadWrite)
        .SelectMany(typeInfo => typeInfo.attributes)
        .Any(attributeInfo => indexType.IsInstanceOfType(attributeInfo.attribute)
                              && (specificContext == null || type.IsComponentFromContext(specificContext)));
    }

    public static bool IsFromContext(this IComponent component, string contextName) {
      return IsComponentFromContext(component.GetType(), contextName);
    }

    public static bool IsComponentFromContext(this Type type, string contextName) {
      return typeof(IComponent).IsAssignableFrom(type)
             && type.GetCustomAttributes<ContextAttribute>()
               .Any(contextAttribute => contextAttribute.contextName == contextName);
    }
  }
}