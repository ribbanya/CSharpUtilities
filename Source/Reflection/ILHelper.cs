using System;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;

namespace Ribbanya.Utilities.Reflection {
  [PublicAPI]
  public static class ILHelper {
    public static (OpCode opCode, byte index) ResolveShortMacroInstruction(OpCode opCode, byte index) {
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

    public static int GetInstructionLength(this OpCode opCode) {
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

    public static void EmitInstruction(this ILGenerator generator, (OpCode, object) instruction) {
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
  }
}