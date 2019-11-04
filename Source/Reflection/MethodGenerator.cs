using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using Ribbanya.Utilities;
using Ribbanya.Utilities.Reflection;
using Instructions = System.Collections.Generic.IEnumerable<(System.Reflection.Emit.OpCode opCode, object parameter)>;

namespace Ribbanya.Reflection {
  [PublicAPI]
  public static class MethodGenerator {
    public static TDelegate GenerateDelegateCallerWithParameters<TDelegate>(
      MethodInfo callTarget, string callerName, IReadOnlyList<object> parameters
    ) where TDelegate : Delegate {
      return GenerateDelegateCallerWithParametersInternal<TDelegate>(
        callTarget, callerName, parameters, false);
    }

    public static TDelegate GenerateDelegateCallerWithParameters<TDelegate>(
      MethodInfo callTarget, string callerName, IReadOnlyDictionary<string, object> parameters
    ) where TDelegate : Delegate {
      return GenerateDelegateCallerWithParametersInternal<TDelegate>(
        callTarget, callerName, parameters, true);
    }

    private static TDelegate GenerateDelegateCallerWithParametersInternal<TDelegate>(
      MethodInfo callTarget, string callerName, IEnumerable parameters, bool isDictionary
    ) where TDelegate : Delegate {
      const BindingFlags prefixFlags = BindingFlags.Public | BindingFlags.Instance;

      var invoke = typeof(TDelegate).GetMethod("Invoke", prefixFlags);
      if (invoke == null)
        throw new MethodAccessException($@"Could not load {typeof(TDelegate).FullName}.""Invoke"".");

      var method = GenerateMethodCallerWithParametersInternal(
        invoke, callTarget, callerName, parameters, isDictionary);

      return (TDelegate) method.CreateDelegate(typeof(TDelegate));
    }

    public static DynamicMethod GenerateMethodCallerWithParameters(
      this MethodInfo @this, MethodInfo callTarget, string callerName, IReadOnlyList<object> parameters
    ) {
      return GenerateMethodCallerWithParametersInternal(
        @this, callTarget, callerName, parameters, false);
    }

    public static DynamicMethod GenerateMethodCallerWithParameters(
      this MethodInfo @this, MethodInfo callTarget, string callerName, IReadOnlyDictionary<string, object> parameters
    ) {
      return GenerateMethodCallerWithParametersInternal(
        @this, callTarget, callerName, parameters, true);
    }

    private static DynamicMethod GenerateMethodCallerWithParametersInternal( //TODO: Better name!
      MethodInfo suppliedMethod, MethodInfo callTarget, string callerName, object parameters, bool isDictionary
    ) {
      //TODO: Support return types other than void

      if (callTarget == null) throw new ArgumentNullException(nameof(callTarget));

      if (!callTarget.ReturnType.IsAssignableFrom(suppliedMethod.ReturnType))
        throw new InvalidCastException(
          $"Could not convert return type {callTarget.ReturnType.FullName} of {callTarget.Name} to" +
          $" {suppliedMethod.ReturnType.FullName} of {suppliedMethod.Name}.");

      var suppliedParameters = suppliedMethod.GetParameters();
      var targetParameters = callTarget.GetParameters();

      var suppliedParameterTypes = suppliedParameters.Select(parameter => parameter.ParameterType).ToArray();
      var targetParameterTypes = targetParameters.Select(parameter => parameter.ParameterType).ToList();

      var @return = new DynamicMethod(callerName, suppliedMethod.ReturnType, suppliedParameterTypes, callTarget.Module);

      var localTypes = new Queue<Type>();


      var arrangedParameters =
        isDictionary
          ? ArrangeNamedSuffixParameters(targetParameters,
            (IReadOnlyDictionary<string, object>) parameters, suppliedParameters.Length, localTypes)
          : ArrangeUnnamedSuppliedParameters(targetParameters, (IReadOnlyList<object>) parameters ?? new List<object>(),
            suppliedParameters.Length, localTypes);

      Instructions methodBody =
        ArrangeTargetParameters(@return, suppliedParameters, targetParameterTypes)
          .Concat(arrangedParameters)
          .Append((OpCodes.Call, callTarget)).Append((OpCodes.Ret, null)).ToList();

      var streamSize = methodBody.Select(instruction => GetInstructionLength(instruction.opCode)).Sum();
      var generator = @return.GetILGenerator(streamSize);

      while (localTypes.Count > 0) generator.DeclareLocal(localTypes.Dequeue());

      foreach (var instruction in methodBody) EmitInstruction(generator, instruction);

      return @return;
    }

    private static Instructions ArrangeTargetParameters(
      DynamicMethod @return, IReadOnlyList<ParameterInfo> targetParameters, IReadOnlyList<Type> suppliedParameterTypes
    ) {
      if (suppliedParameterTypes.Count < targetParameters.Count)
        throw new ArgumentOutOfRangeException(nameof(suppliedParameterTypes));

      for (byte index = 0; index < targetParameters.Count; index++) {
        var targetParameterType = targetParameters[index].ParameterType;
        if (!suppliedParameterTypes[index].IsAssignableFrom(targetParameterType))
          throw new InvalidCastException(
            $"Parameter of type {targetParameterType.FullName} cannot be cast to" +
            $" {suppliedParameterTypes[index].FullName}");

        @return.DefineParameter(index + 1, ParameterAttributes.None, targetParameters[index].Name);
        yield return ResolveMacroInstruction(OpCodes.Ldarg_S, index);
      }
    }

    private static Instructions ArrangeUnnamedSuppliedParameters(
      IReadOnlyList<ParameterInfo> targetParameters, IReadOnlyList<object> suppliedParameters, int startOffset,
      Queue<Type> localTypes
    ) {
      var length = targetParameters.Count - startOffset;
      if (length < suppliedParameters.Count) throw new TargetParameterCountException();

      for (var index = 0; index < length; index++) {
        var targetParameter = targetParameters[index + startOffset];
        var suppliedParameter = index < suppliedParameters.Count
          ? suppliedParameters[index]
          : targetParameter.GetDefaultValue();
        var instructions = GetParameterInstructions(suppliedParameter, targetParameter, localTypes);

        foreach (var instruction in instructions) yield return instruction;
      }
    }

    private static Instructions ArrangeNamedSuffixParameters(
      IEnumerable<ParameterInfo> targetParameters, IReadOnlyDictionary<string, object> suppliedParameters,
      int startOffset, Queue<Type> localTypes
    ) {
      var trimmedParameters = targetParameters.Skip(startOffset).ToList();

      var keys = new HashSet<string>(trimmedParameters.Select(parameter => parameter.Name));

      foreach (var suppliedParameter in suppliedParameters)
        if (!keys.Contains(suppliedParameter.Key))
          throw new KeyNotFoundException($"Parameter {suppliedParameter} is not defined.");

      var instructionQuery = from parameter in trimmedParameters
        let key = parameter.Name
        let jParameter = suppliedParameters.ContainsKey(key)
          ? suppliedParameters[key]
          : parameter.GetDefaultValue()
        select GetParameterInstructions(jParameter, parameter, localTypes)
        into instructions
        from instruction in instructions
        select instruction;

      foreach (var instruction in instructionQuery) yield return instruction;
    }

    private static int GetInstructionLength(OpCode opCode) {
      int operandSize;
      switch (opCode.OperandType) {
        case OperandType.InlineNone:
          operandSize = 0;
          break;
        case OperandType.ShortInlineI:
        case OperandType.ShortInlineVar:
          operandSize = sizeof(byte);
          break;
        case OperandType.InlineI:
          operandSize = sizeof(int);
          break;
        case OperandType.InlineI8:
          operandSize = sizeof(long);
          break;
        case OperandType.ShortInlineR:
          operandSize = sizeof(float);
          break;
        case OperandType.InlineR:
          operandSize = sizeof(double);
          break;
        case OperandType.InlineMethod:
        case OperandType.InlineString:
        case OperandType.InlineType:
          operandSize = UIntPtr.Size;
          break;
        default:
          throw new InvalidOperationException($"Unexpected operand type {opCode.OperandType}.");
      }

      return opCode.Size + operandSize;
    }

    private static void EmitInstruction(ILGenerator generator, (OpCode, object) instruction) {
      var (opCode, parameter) = instruction;
      switch (opCode.OperandType) {
        case OperandType.InlineNone:
          generator.Emit(opCode);
          return;
        case OperandType.ShortInlineI:
        case OperandType.ShortInlineVar:
          generator.Emit(opCode, (byte) parameter);
          return;
        case OperandType.InlineI:
          generator.Emit(opCode, (int) parameter);
          return;
        case OperandType.InlineI8:
          generator.Emit(opCode, (long) parameter);
          return;
        case OperandType.ShortInlineR:
          generator.Emit(opCode, (float) parameter);
          return;
        case OperandType.InlineR:
          generator.Emit(opCode, (double) parameter);
          return;
        case OperandType.InlineMethod: {
          switch (parameter) {
            case MethodInfo method:
              generator.EmitCall(opCode, method, null);
              return;
            case ConstructorInfo constructor:
              generator.Emit(opCode, constructor);
              return;
            default:
              throw new InvalidOperationException(
                $"Operand was of type {OperandType.InlineMethod} but was not a method or constructor.");
          }
        }
        case OperandType.InlineString:
          generator.Emit(opCode, (string) parameter);
          return;
        case OperandType.InlineType:
          generator.Emit(opCode, (Type) parameter);
          return;
        default:
          Debug.Fail("Unexpected operand type.");
          return;
      }
    }

    private static (OpCode opCode, object parameter) ResolveMacroInstruction(OpCode opCode, byte index) {
      if (opCode == OpCodes.Ldarg_S) {
        switch (index) {
          case 0: return (OpCodes.Ldarg_0, null);
          case 1: return (OpCodes.Ldarg_1, null);
          case 2: return (OpCodes.Ldarg_2, null);
          case 3: return (OpCodes.Ldarg_3, null);
          default: return (OpCodes.Ldarg_S, index);
        }
      }

      if (opCode == OpCodes.Ldarga_S) return (OpCodes.Ldarga_S, index);

      if (opCode == OpCodes.Ldloc_S) {
        switch (index) {
          case 0: return (OpCodes.Ldloc_0, null);
          case 1: return (OpCodes.Ldloc_1, null);
          case 2: return (OpCodes.Ldloc_2, null);
          case 3: return (OpCodes.Ldloc_3, null);
          default: return (OpCodes.Ldloc_S, index);
        }
      }

      if (opCode == OpCodes.Ldloca_S) return (OpCodes.Ldloca_S, index);

      throw new InvalidOperationException($"Unexpected macro OpCode {opCode.Name}.");
    }

    private static Instructions GetParameterInstructions(
      object suppliedParameter, ParameterInfo targetParameter, Queue<Type> localTypes
    ) {
      var value = suppliedParameter;
      var type = targetParameter.ParameterType;

      if (!type.IsSimpleType()) throw new NotSupportedException("Reference types are not supported.");

      if (value == null) {
        localTypes.Enqueue(type);
        var localIndex = (byte) (localTypes.Count - 1);
        yield return (OpCodes.Ldloca_S, localIndex);
        yield return (OpCodes.Initobj, type);
        yield return (OpCodes.Ldloc_S, localIndex);
        yield break;
      }


      var nullableType = Nullable.GetUnderlyingType(type);

      if (nullableType != null) {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
#if DEBUG
        var nullableHasValue = type.GetProperty("HasValue", flags);
        Debug.Assert(nullableHasValue != null);
        Debug.Assert((bool) nullableHasValue.GetValue(value));
#endif
        var nullableValue = type.GetProperty("Value", flags);
        Debug.Assert(nullableValue != null);
        value = nullableValue.GetValue(value);

        UtilityHelper.Swap(ref type, ref nullableType);
      }

      if (!type.IsInstanceOfType(value))
        throw new InvalidCastException(
          $"Parameter {value} is of type {value.GetType().FullName}, expected {type.FullName}.");

      Debug.Assert(value != null);

      if (type == typeof(bool)) yield return ((bool) value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0, value);

      else if (type == typeof(byte)) yield return (OpCodes.Ldc_I4_S, value);

      else if (type == typeof(short) || type == typeof(int)) yield return (OpCodes.Ldc_I4, value);

      else if (type == typeof(long)) yield return (OpCodes.Ldc_I8, value);

      else if (type == typeof(float)) yield return (OpCodes.Ldc_R4, value);

      else if (type == typeof(double)) yield return (OpCodes.Ldc_R8, value);

      else if (type == typeof(string)) yield return (OpCodes.Ldstr, value);

      else
        throw new NotSupportedException(
          $"Parameters of type {value?.GetType().FullName} are not supported.");

      if (nullableType == null) yield break;
      yield return (OpCodes.Newobj, nullableType.GetConstructor(new[] {type}));
    }
  }
}