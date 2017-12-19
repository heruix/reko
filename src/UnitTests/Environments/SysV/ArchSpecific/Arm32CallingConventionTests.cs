﻿#region License
/* 
 * Copyright (C) 1999-2017 John Källén.
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
using Reko.Arch.Arm;
using Reko.Core;
using Reko.Core.Serialization;
using Reko.Core.Types;
using Reko.Environments.SysV.ArchSpecific;
using System.Collections.Generic;

namespace Reko.UnitTests.Environments.SysV.ArchSpecific
{
    [TestFixture]
    [Category(Categories.Capstone)]
    public class Arm32CallingConventionTests
    {
        private readonly PrimitiveType i32 = PrimitiveType.Int32;
        private readonly PrimitiveType u32 = PrimitiveType.UInt32;
        private readonly PrimitiveType i8 = PrimitiveType.SByte;
        private readonly PrimitiveType u8 = PrimitiveType.Byte;
        private readonly PrimitiveType i16 = PrimitiveType.Int16;
        private readonly VoidType v = VoidType.Instance;

        private Arm32ProcessorArchitecture arch;
        private CallingConvention cc;
        private ICallingConventionEmitter ccr;

        [SetUp]
        public void Setup()
        {
            arch = new Arm32ProcessorArchitecture();
        }

        private Pointer Ptr(DataType dt)
        {
            return new Pointer(dt, 4);
        }

        private void Given_CallingConvention()
        {
            this.cc = new Arm32CallingConvention(arch);
            this.ccr = new CallingConventionEmitter();
        }

        private Argument_v1 RegArg(SerializedType type, string regName)
        {
            return new Argument_v1
            {
                Type = type,
                Kind = new Register_v1 { Name = regName },
                Name = regName
            };
        }

        private Argument_v1 FpuArg(SerializedType type, string name)
        {
            return new Argument_v1(
                name,
                type,
                new Register_v1 { Name = name },
                false);
        }

        [Test]
        public void SvArm32Cc_DeserializeFpuReturnValue()
        {
            Given_CallingConvention();
            cc.Generate(ccr, PrimitiveType.Real64, null, new List<DataType>());
            Assert.AreEqual("Stk: 0 Sequence r1:r0 ()", ccr.ToString());
        }

        [Test]
        public void SvArm32Cc_Load_cdecl()
        {
            Given_CallingConvention();
            cc.Generate(ccr, null, null, new List<DataType> { i32 });
            Assert.AreEqual("Stk: 0 void (r0)", ccr.ToString());
        }

        [Test]
        public void SvArm32Cc_Load_IntArgs()
        {
            Given_CallingConvention();
            cc.Generate(ccr, null, null, new List<DataType> { i16, i8, i32, i16, u8, i32, i32 });
            Assert.AreEqual("Stk: 0 void (r0, r1, r2, r3, Stack +0010, Stack +0014, Stack +0018)", ccr.ToString());
        }

        [Test]
        public void SvArm32Cc_mmap()
        {
            Given_CallingConvention();
            cc.Generate(ccr, Ptr(v), null, new List<DataType> { Ptr(v), u32, i32, i32, i32, i32 });
            Assert.AreEqual("Stk: 0 r0 (r0, r1, r2, r3, Stack +0010, Stack +0014)", ccr.ToString());
        }
    }
}
