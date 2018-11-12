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
using System;

namespace Reko.Arch.M6800.M6812
{
    public class M6812Instruction : MachineInstruction
    {
        public InstrClass iclass;
        public MachineOperand[] Operands;

        public override InstrClass InstructionClass => iclass;

        public override int OpcodeAsInteger => (int) Opcode;

        public Opcode Opcode { get; set; }

        public override MachineOperand GetOperand(int i)
        {
            throw new NotImplementedException();
        }

        public override void Render(MachineInstructionWriter writer, MachineInstructionWriterOptions options)
        {
            writer.WriteOpcode(Opcode.ToString());
            if (Operands.Length > 0)
            {
                writer.Tab();
                var sep = "";
                foreach (var op in Operands)
                {
                    writer.WriteString(sep);
                    sep = ",";
                    RenderOperand(op, writer, options);
                }
            }
        }

        private static void RenderOperand(MachineOperand op, MachineInstructionWriter writer, MachineInstructionWriterOptions options)
        {
            switch (op)
            {
            case ImmediateOperand immOp:
                writer.WriteString(MachineOperand.FormatUnsignedValue(immOp.Value, "#${1}"));
                return;
            }
            op.Write(writer, options);
        }
    }
}
