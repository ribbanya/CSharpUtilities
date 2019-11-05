using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using JetBrains.Annotations;
using Xbehave;

namespace Ribbanya.Tests {
  [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
  public sealed class OverloadCreationFeature {
    [UsedImplicitly]
    internal static float APlusBTimesCMinusD(float a, float b, float c, float d) => (a + b) * c - d;

    [Scenario(DisplayName = "Overloads can be created from a static method and a list of parameters")]
    public void OverloadsCanBeCreatedFromAStaticMethodAndAListOfParameters(
      string methodName, IReadOnlyList<object> defaultParameters
    ) {
      var method = default(MethodInfo);
      "Given a method to overload"
        .x(() => {
          const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
          method = typeof(OverloadCreationFeature).GetMethod(methodName, flags);
        });
    }
  }
}