using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;

namespace Ribbanya.Utilities.Reflection {
  [PublicAPI]
  internal static class ILHelper {
    internal static (OpCode opCode, byte index) ResolveShortMacroInstruction(OpCode opCode, byte index) {
      if (opCode == OpCodes.Ldarg_S) {
        switch (index) {
          case 0: return (OpCodes.Ldarg_0, 0);
          case 1: return (OpCodes.Ldarg_1, 0);
          case 2: return (OpCodes.Ldarg_2, 0);
          case 3: return (OpCodes.Ldarg_3, 0);
          default: return (OpCodes.Ldarg_S, index);
        }
      }

      if (opCode == OpCodes.Ldarga_S) return (OpCodes.Ldarga_S, index);

      if (opCode == OpCodes.Ldloc_S) {
        switch (index) {
          case 0: return (OpCodes.Ldloc_0, 0);
          case 1: return (OpCodes.Ldloc_1, 0);
          case 2: return (OpCodes.Ldloc_2, 0);
          case 3: return (OpCodes.Ldloc_3, 0);
          default: return (OpCodes.Ldloc_S, index);
        }
      }

      if (opCode == OpCodes.Ldloca_S) return (OpCodes.Ldloca_S, index);
      throw new InvalidOperationException($"Unexpected macro OpCode {opCode.Name}.");
    }

    internal static int GetInstructionLength(this OpCode opCode) {
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

    internal static void EmitInstruction(this ILGenerator generator, (OpCode, object) instruction) {
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
          throw new InvalidOperationException("Unexpected operand type.");
      }
    }

    internal static IEnumerable<(OpCode opCode, object parameter)> GetParameterInstructions(
      object parameter, Type type, Queue<Type> localTypes
    ) {
      if (parameter == null) {
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
        Debug.Assert((bool) nullableHasValue.GetValue(parameter));
#endif
        var nullableValue = type.GetProperty("Value", flags);
        parameter = nullableValue?.GetValue(parameter) ?? throw new MethodAccessException();

        UtilityHelper.Swap(ref type, ref nullableType);
      }


      Debug.Assert(parameter != null);

      if (type == typeof(bool)) yield return ((bool) parameter ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0, parameter);

      else if (type == typeof(byte)) yield return (OpCodes.Ldc_I4_S, parameter);

      else if (type == typeof(short) || type == typeof(int)) yield return (OpCodes.Ldc_I4, parameter);

      else if (type == typeof(long)) yield return (OpCodes.Ldc_I8, parameter);

      else if (type == typeof(float)) yield return (OpCodes.Ldc_R4, parameter);

      else if (type == typeof(double)) yield return (OpCodes.Ldc_R8, parameter);

      else if (type == typeof(string)) yield return (OpCodes.Ldstr, parameter);

      else throw new InvalidCastException($"Cannot convert {parameter} to {type}.");

      if (nullableType == null) yield break;
      yield return (OpCodes.Newobj, nullableType.GetConstructor(new[] {type}));
    }
  }
}