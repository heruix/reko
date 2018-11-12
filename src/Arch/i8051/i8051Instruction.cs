﻿#region License
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Arch.i8051
{
    public class i8051Instruction : MachineInstruction
    {
        public override InstrClass InstructionClass
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override int OpcodeAsInteger
        {
            get { return (int)Opcode; }
        }

        public Opcode Opcode { get; set; }
        public MachineOperand Operand1 { get; set; }
        public MachineOperand Operand2 { get; set; }
        public MachineOperand Operand3 { get; set; }

        public override MachineOperand GetOperand(int i)
        {
            if (i == 0) return Operand1;
            if (i == 1) return Operand2;
            if (i == 2) return Operand3;
            return null;
        }

        public override void Render(MachineInstructionWriter writer, MachineInstructionWriterOptions options)
        {
            writer.WriteOpcode(Opcode.ToString());
            if (Operand1 == null)
                return;
            writer.Tab();
            Operand1.Write(writer, options);
            if (Operand2 == null)
                return;
            writer.WriteString(",");
            Operand2.Write(writer, options);
            if (Operand3 == null)
                return;
            writer.WriteString(",");
            Operand3.Write(writer, options);
        }
    }
}
