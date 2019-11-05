using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using Ribbanya.Utilities.Collections;

////TODO: Should throw an exception when too many parameters are calling.
////TODO: Test edge cases and overloads
namespace Ribbanya.Utilities.Reflection {
  [PublicAPI]
  public static class OverloadFactory {
    /// <summary>
    /// A special value indicating that a parameter should appear as a parameter in the generated method.
    /// </summary>
    public static readonly object Unspecified = new object();

    /// <summary>
    /// A special value indicating that a parameter should use the default parameter value as specified by the
    /// overloaded method.
    /// </summary>
    public static readonly object ParameterDefault = new object();

    /// <summary>
    /// A special value indicating that a parameter should use the default value for its type as specified by the
    /// overloaded method.
    /// </summary>
    public static readonly object TypeDefault = new object();

    /// <summary>
    /// A special value indicating that a parameter should use the default parameter value if available, otherwise
    /// the default value for its type.
    /// </summary>
    public static readonly object ParameterOrTypeDefault = new object();


//    public static TSignature CreateOverload<TSignature>(
//      this MethodInfo method, string overloadName, [CanBeNull] IReadOnlyList<object> defaultParameters
//    ) where TSignature : Delegate {
//      return CreateOverload<TSignature>(method, overloadName, defaultParameters, false);
//    }
//
//    public static TSignature CreateOverload<TSignature>(
//      this MethodInfo method, string overloadName, [CanBeNull] IReadOnlyDictionary<string, object> defaultParameters
//    ) where TSignature : Delegate {
//      return CreateOverload<TSignature>(method, overloadName, defaultParameters, true);
//    }
//
//    private static TSignature CreateOverload<TSignature>(
//      this MethodInfo method, string overloadName, [CanBeNull] IEnumerable defaultParameters, bool usesNamedParameters
//    ) where TSignature : Delegate {
//      const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
//
//      const string invokeName = "Invoke";
//      var invoke = typeof(TSignature).GetMethod(invokeName, flags);
//      var invokeFullName = $@"{typeof(TSignature).FullName}.""{invokeName}""";
//
//      if (invoke == null)
//        throw new MethodAccessException($"Could not load {invokeFullName}.");
//
//      var overload = CreateOverload(invoke, innerMethod, overloadName, defaultParameters, usesNamedParameters);
//      return (TSignature) overload.CreateDelegate(typeof(TSignature));
//    }
//
//    public static DynamicMethod CreateOverload(MethodInfo @this, MethodInfo signature,
//                                               string overloadName,
//                                               [CanBeNull] IReadOnlyList<object> implementationParameters) {
//      return CreateOverload(signature, @this, overloadName, implementationParameters, false);
//    }
//
//    public static DynamicMethod CreateOverload(
//      this MethodInfo @this, MethodInfo callTarget, string callerName,
//      [CanBeNull] IReadOnlyDictionary<string, object> defaultParameters
//    ) {
//      return CreateOverload(@this, callTarget, callerName, defaultParameters, true);
//    }
//

    public static TDelegate CreateOverload<TDelegate>(
      this MethodInfo @this, string overloadName, IReadOnlyList<object> defaultParameters, int startOffset
    ) where TDelegate : Delegate {
      //TODO: Validate unspecifiedParameterTypes against Invoke
      ArrangeParameters(@this, defaultParameters, startOffset,
        out var paddedParameters, out var unspecifiedParameterTypes);

      var overload = CreateOverloadInternal(@this, overloadName, paddedParameters, unspecifiedParameterTypes);

      return (TDelegate) overload.CreateDelegate(typeof(TDelegate));
    }

    public static Delegate CreateOverload(
      this MethodInfo @this, Type delegateType, string overloadName, IReadOnlyList<object> defaultParameters,
      int startOffset
    ) {
      //TODO: Validate parameters

      ArrangeParameters(@this, defaultParameters, startOffset,
        out var paddedParameters, out var unspecifiedParameterTypes);

      var overload = CreateOverloadInternal(@this, overloadName, paddedParameters, unspecifiedParameterTypes);

      return overload.CreateDelegate(delegateType);
    }

    public static Delegate CreateOverload(
      this MethodInfo @this, string overloadName, IReadOnlyList<object> defaultParameters
    ) {
      //TODO: Validate lengths
      var startOffset = @this.GetParameters().Length - defaultParameters.Count;
      return CreateOverload(@this, overloadName, defaultParameters, startOffset);
    }

    public static Delegate CreateOverload(
      this MethodInfo @this, string overloadName, IReadOnlyList<object> defaultParameters, int startOffset
    ) {
      //TODO: Validate parameters

      ArrangeParameters(@this, defaultParameters, startOffset,
        out var paddedParameters, out var unspecifiedParameterTypes);

      var delegateParameterTypes = new Type[unspecifiedParameterTypes.Length + 1];
      Array.Copy(unspecifiedParameterTypes, delegateParameterTypes,
        unspecifiedParameterTypes.Length);
      delegateParameterTypes[unspecifiedParameterTypes.Length] = @this.ReturnType;
      var delegateType = Expression.GetDelegateType(delegateParameterTypes);

      var overload = CreateOverloadInternal(@this, overloadName, paddedParameters, unspecifiedParameterTypes);

      return overload.CreateDelegate(delegateType);
    }

    private static DynamicMethod CreateOverloadInternal(
      MethodInfo @base, string overloadName, IReadOnlyList<object> defaultParameters, Type[] parameterTypes
    ) {
      //TODO: Shouldn't throw an exception based on user input

      //TODO: Support any return type

      var @return = new DynamicMethod(overloadName, @base.ReturnType, parameterTypes, @base.Module);

      var localTypes = new Queue<Type>();
      var overloadBody = new List<(OpCode opCode, object parameter)>();

      var baseParameters = @base.GetParameters();
      var argS = (byte) 0;
      for (var index = (byte) 0; index < defaultParameters.Count; index++) {
        var overloadParameter = defaultParameters[index];
        var baseParameter = baseParameters[index];

        if (ReferenceEquals(overloadParameter, Unspecified)) {
          @return.DefineParameter(argS + 1, ParameterAttributes.None, baseParameter.Name);
          var macroInstruction = ILHelper.ResolveShortMacroInstruction(OpCodes.Ldarg_S, argS);
          overloadBody.Add(macroInstruction);
          argS += 1;
          continue;
        }

        var baseParameterType = baseParameter.ParameterType;


        var parameterInstructions = ILHelper.GetParameterInstructions(overloadParameter, baseParameterType, localTypes);

        overloadBody.AddRange(parameterInstructions);
      }

      overloadBody.Add((OpCodes.Call, @base));
      overloadBody.Add((OpCodes.Ret, null));

      var streamSize = overloadBody.Sum(instruction => instruction.opCode.GetInstructionLength());
      var generator = @return.GetILGenerator(streamSize);

      while (localTypes.Count > 0) generator.DeclareLocal(localTypes.Dequeue());

      foreach (var instruction in overloadBody) generator.EmitInstruction(instruction);

      return @return;
    }

    private static void ArrangeParameters(
      MethodInfo @base, IReadOnlyList<object> defaultParameters, int startOffset,
      out IReadOnlyList<object> paddedParameters, out Type[] unspecifiedParameterTypes
    ) {
      ArrangeParameters(
        @base.GetParameters().ToDictionary(),
        defaultParameters.ToDictionary(index => index + startOffset),
        out paddedParameters, out unspecifiedParameterTypes
      );
    }

    private static void ArrangeParameters<TKey>(
      IReadOnlyDictionary<TKey, ParameterInfo> baseParameters, IReadOnlyDictionary<TKey, object> defaultParameters,
      out IReadOnlyList<object> paddedParameters, out Type[] unspecifiedParameterTypes
    ) {
      var outPaddedParameters = new object[baseParameters.Count];
      var outUnspecifiedParameterTypes = new List<Type>(baseParameters.Count - defaultParameters.Count);

      foreach (var kvp in baseParameters) {
        var baseParameter = kvp.Value;
        var position = baseParameter.Position;
        var baseParameterKey = kvp.Key;

        if (defaultParameters.ContainsKey(baseParameterKey)) {
          var defaultParameter = defaultParameters[baseParameterKey];
          var baseParameterType = baseParameter.ParameterType;

          outPaddedParameters[position] =
            ReferenceEquals(defaultParameter, ParameterDefault)
              ? baseParameter.GetDefaultValue()
              : ReferenceEquals(defaultParameter, TypeDefault)
                ? baseParameterType.GetDefaultValue()
                : ReferenceEquals(defaultParameter, ParameterOrTypeDefault)
                  ? baseParameter.IsActuallyOptional()
                    ? baseParameter.DefaultValue
                    : baseParameter.ParameterType.GetDefaultValue()
                  : Convert.ChangeType(defaultParameter, baseParameterType);
        }
        else {
          outPaddedParameters[position] = Unspecified;
          outUnspecifiedParameterTypes.Add(baseParameter.ParameterType);
        }
      }

      paddedParameters = outPaddedParameters;
      unspecifiedParameterTypes = outUnspecifiedParameterTypes.ToArray();
    }

//
//    [CanBeNull]
//    private static Exception ValidateOverloadSignature(
    //TODO: Validate unknown parameter names in calling method
//      MethodInfo method, Type returnType, IReadOnlyList<(Type type, object value) parameters
//    ) {

//      if (!type.IsSimpleType()) throw new NotSupportedException("Reference types are not supported.");


//      var callingParameterCount = callingParameters.Length;
//      var targetParameterCount = targetParameters.Length;
//      var parameterCount = ((ICollection) defaultParameters)?.Count ?? 0;
//
//      var callingMethodFullName = callTarget.GetFullName();
//      var callTargetFullName = method.GetFullName();

//


////      if (method == null) throw new ArgumentNullException(nameof(method));
////
////      if (!method.ReturnType.IsAssignableFrom(callTarget.ReturnType))

////        throw new InvalidCastException(

////          $"Could not convert return type {method.ReturnType.FullName} of {method.Name} to" +

////          $" {callTarget.ReturnType.FullName} of {callTarget.Name}.");

//

////      var methodReturnType = method.ReturnType;

////      var invokeReturnType = invoke.ReturnType;

////

////      if (!methodReturnType.IsAssignableFrom(invokeReturnType))

////        throw new InvalidOperationException(

////          $"{invokeFullName} returns {invokeReturnType.FullName} but must return {methodReturnType.FullName}" +

////          $" to match {method.GetFullName()}.");

//

//

////      if (callingParameterCount < targetParameterCount)

////        throw new TargetParameterCountException(

////          $"Too few parameters in {callingMethodFullName} to be given to {callTargetFullName}.");

////

////

////      if (targetParameterCount < callingParameterCount)

////        throw new TargetParameterCountException($"Too many parameters given to method {callTargetFullName}.");

////

////      var requiredParameterCount = callingParameters.Skip(targetParameterCount)

////        .Count(parameter => !(parameter.IsOptional || parameter.HasDefaultValue));

////

////      if (parameterCount < requiredParameterCount)

////        throw new TargetParameterCountException($"Too few parameters given to method {callingMethodFullName}.");

//

////      if (!callingParameterTypes[index].IsAssignableFrom(targetParameterType))

////        throw new InvalidCastException(

////          $"Parameter of type {targetParameterType.FullName} cannot be cast to" +

////          $" {callingParameterTypes[index].FullName}");

//

//      throw new NotImplementedException();

//    }

//
//

//    private static Instructions ArrangeUnnamedDefaultParameters(

//      MemberInfo method, IReadOnlyList<object> callingParameters, int startOffset,

//      Queue<Type> localTypes

//    ) {

//      var length = targetParameters.Count - startOffset;

//      if (length < callingParameters.Count) throw new TargetParameterCountException();

//

//      for (var index = 0; index < length; index++) {

//        var targetParameter = targetParameters[index + startOffset];

//        var callingParameter = index < callingParameters.Count

//          ? callingParameters[index]

//          : targetParameter.GetDefaultParameterValue();

//        var instructions = GetParameterInstructions(callingParameter, targetParameter, localTypes);

//

//        foreach (var instruction in instructions) yield return instruction;

//      }

//    }

//
  }
}