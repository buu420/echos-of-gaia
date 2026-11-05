using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
namespace FF9Access.Tests
{
    internal static partial class NativeReaderVerification
    {
        private static readonly Dictionary<ushort, OpCode> Opcodes = BuildOpcodes();
        private static IEnumerable<string> Calls(MetadataReader metadata, byte[] il)
        {
            for (int offset = 0; offset < il.Length;) {
                ushort code = il[offset++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[offset++]);
                var opcode = Opcodes[code]; int size;
                switch (opcode.OperandType) {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(il, offset); break;
                    default: size = 4; break;
                }
                // The legacy game build's compiler emits ReferenceEquals as
                // reference branches/ceq, without an Object.ReferenceEquals call.
                if(opcode==OpCodes.Ceq || opcode==OpCodes.Bne_Un || opcode==OpCodes.Bne_Un_S)yield return "IL.ReferenceComparison";
                if(opcode.OperandType==OperandType.InlineField && (opcode==OpCodes.Ldfld || opcode==OpCodes.Ldflda || opcode==OpCodes.Ldsfld || opcode==OpCodes.Stfld || opcode==OpCodes.Stsfld)) {
                    string access=opcode==OpCodes.Stfld || opcode==OpCodes.Stsfld?"FieldWrite.":"FieldRead.";
                    EntityHandle fieldHandle=MetadataTokens.EntityHandle(BitConverter.ToInt32(il,offset));
                    if(fieldHandle.Kind==HandleKind.FieldDefinition) {
                        var field=metadata.GetFieldDefinition((FieldDefinitionHandle)fieldHandle);
                        yield return access+metadata.GetString(metadata.GetTypeDefinition(field.GetDeclaringType()).Name)+"."+metadata.GetString(field.Name);
                    }else if(fieldHandle.Kind==HandleKind.MemberReference) {
                        var member=metadata.GetMemberReference((MemberReferenceHandle)fieldHandle);
                        if(member.Parent.Kind==HandleKind.TypeReference)yield return access+metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)member.Parent).Name)+"."+metadata.GetString(member.Name);
                    }
                }
                if (opcode.OperandType == OperandType.InlineMethod) {
                    EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                    if(handle.Kind==HandleKind.MethodSpecification)handle=metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
                    if (handle.Kind == HandleKind.MemberReference) {
                        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
                        if (member.Parent.Kind == HandleKind.TypeReference) yield return metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)member.Parent).Name) + "." + metadata.GetString(member.Name);
                    } else if (handle.Kind == HandleKind.MethodDefinition) {
                        var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
                        string owner=metadata.GetString(metadata.GetTypeDefinition(method.GetDeclaringType()).Name),name=metadata.GetString(method.Name);
                        yield return owner + "." + name;
                        if(owner=="FieldAnalogMovement" && name=="StepMagnitude")yield return "Signature."+owner+"."+name+"/"+method.GetParameters().Count;
                    }
                }
                offset += size;
            }
        }
        private static Dictionary<ushort, OpCode> BuildOpcodes()
        {
            var result = new Dictionary<ushort, OpCode>(); foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) if (field.FieldType == typeof(OpCode)) { var value = (OpCode)field.GetValue(null); result[unchecked((ushort)value.Value)] = value; } return result;
        }
    }
}
