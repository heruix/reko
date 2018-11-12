#region License
/* 
 * Copyright (C) 1999-2018 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Reko.Core;
using Reko.Core.Machine;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Arch.M68k
{
    public class M68kInstruction : MachineInstruction
    {
        public Opcode code;
        public InstrClass iclass;
        public PrimitiveType dataWidth;
        public MachineOperand op1;
        public MachineOperand op2;
        public MachineOperand op3;

        public override InstrClass InstructionClass => iclass;

        public override int OpcodeAsInteger => (int) code;

        public override MachineOperand GetOperand(int i)
        {
            switch (i)
            {
            case 0: return op1;
            case 1: return op2;
            case 2: return op3;
            default: return null;
            }
        }

        public override void Render(MachineInstructionWriter writer, MachineInstructionWriterOptions options)
        {
            if (code == Opcode.illegal && op1 != null && writer.Platform != null)
            {
                var imm = op1 as M68kImmediateOperand;
                // MacOS uses invalid opcodes to invoke Macintosh Toolbox services. 
                // We may have to generalize the Platform API to allow specifying 
                // the opcode of the invoking instruction, to disambiguate from 
                // "legitimate" TRAP calls.
                var svc = writer.Platform.FindService((int)imm.Constant.ToUInt32(), null);
                if (svc != null)
                {
                    writer.WriteString(svc.Name);
                    return;
                }
            }
            if (dataWidth != null)
            {
                writer.WriteOpcode(string.Format("{0}{1}", code, DataSizeSuffix(dataWidth)));
            }
            else
            {
                writer.WriteOpcode(code.ToString());
            }
            writer.Tab();
            if (op1 != null)
            {
                WriteOperand(op1, writer, options);
                if (op2 != null)
                {
                    writer.WriteChar(',');
                    WriteOperand(op2, writer, options);
                }
            }
        }

        private void WriteOperand(MachineOperand op, MachineInstructionWriter writer, MachineInstructionWriterOptions options)
        {
            var memOp = op as MemoryOperand;
            if (memOp != null && memOp.Base == Registers.pc)
            {
                var uAddr = Address.ToUInt32() + memOp.Offset.ToInt32();
                var addr = Address.Ptr32((uint)uAddr);
                if ((options & MachineInstructionWriterOptions.ResolvePcRelativeAddress) != 0)
                {
                    writer.WriteAddress(addr.ToString(), addr);
                    writer.AddAnnotation(op.ToString());
                }
                else
                {
                    op.Write(writer, options);
                    writer.AddAnnotation(addr.ToString());
                }
                return;

            }
            op.Write(writer, options);
        }

        private string DataSizeSuffix(PrimitiveType dataWidth)
        {
            if (dataWidth.Domain == Domain.Real)
            {
                switch (dataWidth.BitSize)
                {
                case 32: return ".s";
                case 64: return ".d";
                case 80: return ".x";   //$REVIEW: not quite true?
                case 96: return ".x";
                }
            }
            else
            {
                switch (dataWidth.BitSize)
                {
                case 8: return ".b";
                case 16: return ".w";
                case 32: return ".l";
                case 64: return ".q";
                }
            }
            throw new InvalidOperationException(string.Format("Unsupported data width {0}.", dataWidth.BitSize));
        }
    }
}
