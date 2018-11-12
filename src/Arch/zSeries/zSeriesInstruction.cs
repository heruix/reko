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

namespace Reko.Arch.zSeries
{
    public class zSeriesInstruction : MachineInstruction
    {
        internal Opcode Opcode;
        internal MachineOperand[] Ops;

        public override InstrClass InstructionClass
        {
            get { throw new NotImplementedException(); }
        }

        public override int OpcodeAsInteger => (int) Opcode;

        public override MachineOperand GetOperand(int i)
        {
            throw new System.NotImplementedException();
        }

        public override void Render(MachineInstructionWriter writer, MachineInstructionWriterOptions options)
        {
            writer.WriteOpcode(this.Opcode.ToString());
            if (Ops.Length == 0)
                return;
            writer.Tab();
            var sep = "";
            foreach (var op in Ops)
            {
                writer.WriteString(sep);
                sep = ",";
                op.Write(writer, options);
        }
    }
    }
}