using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Ribbanya.Utilities.Collections {
  [PublicAPI]
  public static class CollectionsHelper {
    public static int IndexOf<T>(this IEnumerable<T> @this, T value) where T : IEquatable<T> {
      var index = 0;
      foreach (var item in @this) {
        if (item.Equals(value)) return index;
        index += 1;
      }

      throw new IndexOutOfRangeException();
    }

    public static bool EnumerableEquals<T>(this IEnumerable<T> a, IEnumerable<T> b) {
      T Identity(T value) {
        return value;
      }

      return a.OrderBy(Identity).SequenceEqual(b.OrderBy(Identity));
    }

    public static IEnumerable<(T value, int index)> Iterate<T>(this IEnumerable<T> @this) {
      return @this.Select((value, index) => (value, index));
    }

    public static IEnumerable<(T value, int index)> Iterate<T>(this IEnumerable<T> @this, Action<T, int> action) {
      var i = 0;
      foreach (var item in @this) {
        action?.Invoke(item, i);
        yield return (item, i);
        i += 1;
      }
    }

    public static IEnumerable<T> CoalesceNull<T>(this IEnumerable<T> @this) {
      return @this ?? Enumerable.Empty<T>();
    }

    public static IEnumerable<T> Traverse<T>(this IEnumerable<T> items, Func<T, IEnumerable<T>> childSelector) {
      var stack = new Stack<T>(items);
      while (stack.Count > 0) {
        var next = stack.Pop();
        yield return next;
        foreach (var child in childSelector(next)) {
          if (ReferenceEquals(child, next))
            throw new InvalidOperationException($"Infinite recursive loop detected in {nameof(Traverse)}.");

          stack.Push(child);
        }
      }
    }

    public static IEnumerable<T> Traverse<T>(T item, Func<T, IEnumerable<T>> childSelector) {
      return Traverse(childSelector(item), childSelector);
    }

    public static Dictionary<int, T> ToDictionary<T>(this IReadOnlyList<T> @this) {
      var count = @this.Count;
      var @return = new Dictionary<int, T>(count);
      for (var index = 0; index < count; index++) {
        @return.Add(index, @this[index]);
      }

      return @return;
    }

    public static Dictionary<int, T> ToDictionary<T>(this IReadOnlyList<T> @this, Func<int, int> indexModifier) {
      var count = @this.Count;
      var @return = new Dictionary<int, T>(count);
      for (var index = 0; index < count; index++) {
        @return.Add(indexModifier(index), @this[index]);
      }

      return @return;
    }
  }
}