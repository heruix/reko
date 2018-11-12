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

using NUnit.Framework;
using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using Reko.Core.Types;
using System;
using System.Collections.Generic;

namespace Reko.UnitTests.Core.Machine
{
    [TestFixture]
    public class X86InstructionComparerTests
    {
        private MachineOperand Op(object o)
        {
            if (o == null)
                return null;
            if (o is MachineOperand)
                return (MachineOperand)o;
            if (o is int)
            {
                return new ImmediateOperand(Constant.Word32((int)o));
            }
            else if (o is string)
            {
                return new RegisterOperand(Registers.GetRegister((string)o));
            }
            else
                throw new NotImplementedException();
        }

        private X86Instruction Create(Opcode op, object a = null, object b = null)
        {
            var ops = new List<MachineOperand>();
            if (a != null)
            {
                ops.Add(Op(a));
                if (b != null)
                    ops.Add(Op(b));
            }

            return new X86Instruction(op, InstrClass.Linear, PrimitiveType.Word32, PrimitiveType.Word32, ops.ToArray());
        }

        private MachineOperand Mem32(RegisterStorage baseReg)
        {
            var mem = new MemoryOperand(PrimitiveType.Word32);
            mem.Base = baseReg;
            return mem;
        }
        private MachineOperand Mem32(RegisterStorage baseReg, int off)
        {
            var mem = new MemoryOperand(PrimitiveType.Word32);
            mem.Base = baseReg;
            mem.Offset = Constant.Word32(off);
            return mem;
        }
        private MachineOperand Mem32(int off)
        {
            var mem = new MemoryOperand(PrimitiveType.Word32);
            mem.Offset = Constant.Word32(off);
            return mem;
        }

        [Test]
        public void X86ic_CompareOpcodes()
        {
            var a = Create(Opcode.nop);
            var b = Create(Opcode.nop);
            var cmp = new X86InstructionComparer(Normalize.Nothing);
            Assert.IsTrue(cmp.Equals(a, b));
            Assert.IsTrue(cmp.GetHashCode(a) == cmp.GetHashCode(b));
        }

        [Test]
        public void X86ic_CompareRegisters_Pass()
        {
            var a = Create(Opcode.neg, "eax");
            var b = Create(Opcode.neg, "eax");
            var cmp = new X86InstructionComparer(Normalize.Nothing);
            Assert.IsTrue(cmp.Equals(a, b));
            Assert.IsTrue(cmp.GetHashCode(a) == cmp.GetHashCode(b));
        }

        [Test]
        public void X86ic_CompareRegisters_NoNormalize_Fail()
        {
            var a = Create(Opcode.neg, "eax");
            var b = Create(Opcode.neg, "ecx");
            var cmp = new X86InstructionComparer(Normalize.Nothing);
            Assert.IsFalse(cmp.Equals(a, b));
            Assert.IsFalse(cmp.GetHashCode(a) == cmp.GetHashCode(b));
        }

        [Test]
        public void X86ic_CompareRegisters_Normalize_Succeed()
        {
            var a = Create(Opcode.neg, "eax");
            var b = Create(Opcode.neg, "ecx");
            var cmp = new X86InstructionComparer(Normalize.Registers);
            Assert.IsTrue(cmp.Equals(a, b));
            Assert.IsTrue(cmp.GetHashCode(a) == cmp.GetHashCode(b));
        }

        [Test]
        public void X86ic_CompareMem_Normalize_Succeed()
        {
            var a = Create(Opcode.neg, Mem32(Registers.eax));
            var b = Create(Opcode.neg, Mem32(Registers.ecx));
            var cmp = new X86InstructionComparer(Normalize.Registers);
            Assert.IsTrue(cmp.Equals(a, b));
            Assert.IsTrue(cmp.GetHashCode(a) == cmp.GetHashCode(b));
        }

        [Test]
        public void X86ic_CompareMemOff_Normalize_Succeed()
        {
            var a = Create(Opcode.neg, Mem32(Registers.eax, 30));
            var b = Create(Opcode.neg, Mem32(Registers.ecx, 30));
            var cmp = new X86InstructionComparer(Normalize.Registers);
            Assert.IsTrue(cmp.Equals(a, b));
            Assert.IsTrue(cmp.GetHashCode(a) == cmp.GetHashCode(b));
        }
    }
}
