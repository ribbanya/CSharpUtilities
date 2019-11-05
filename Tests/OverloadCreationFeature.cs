using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using JetBrains.Annotations;
using Ribbanya.Utilities.Reflection;
using Xbehave;
using Xunit;

namespace Ribbanya.Utilities.Tests {
  [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
  public sealed class OverloadCreationFeature {
    //TODO: Test special object parameters (Unspecified, Defaults)
    //TODO: Test exceptions

    [UsedImplicitly]
    internal static float APlusBTimesCMinusD(float a, float b, float c, float d) => (a + b) * c - d;

    [UsedImplicitly]
    internal static float APlusBTimes3Minus4(float a, float b) => APlusBTimesCMinusD(a, b, 3, 4);

    [Scenario(DisplayName = "Overloads can be created from a static method and a list of parameters")]
    [ClassData(typeof(OverloadCreationDataProvider))]
    public void OverloadsCanBeCreatedFromAStaticMethodAndAListOfParameters(
      string methodName, string overloadName, object[] defaultParameters, object[] givenParameters
    ) {
      var method = default(MethodInfo);
      "Given a method to overload".x(() => {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        method = typeof(OverloadCreationFeature).GetMethod(methodName, flags);
      });
      "And a name for the overload".x(() => { });
      "And some parameters to use as default".x(() => { });
      var @delegate = default(Delegate);
      "When an overload is generated as a delegate".x(() => {
        @delegate = method.CreateOverload(overloadName, defaultParameters);
      });
      var result = default(float);
      "And the delegate is dynamically invoked"
        .x(() => result = (float) @delegate.DynamicInvoke(givenParameters));

      "Then the result should be the same as if the original method had been invoked".x(() => {
        switch (methodName) {
          case nameof(APlusBTimesCMinusD): {
            var a = (float) givenParameters[0];
            var b = (float) givenParameters[1];
            var c = (float) defaultParameters[0];
            var d = (float) defaultParameters[1];
            result.Should().Be(APlusBTimesCMinusD(a, b, c, d));
            break;
          }
          default: throw new NotImplementedException();
        }
      });
      "And its name should be the name provided".x(() => @delegate.Method.Name.Should().Be(overloadName));
    }
  }
}