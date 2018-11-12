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
using Reko.Core.Lib;
using NUnit.Framework;
using System;
using Rhino.Mocks;

namespace Reko.UnitTests.Core
{
	[TestFixture]
	public class ProcedureTests
	{
        private MockRepository mr;
        private IProcessorArchitecture arch;

        public ProcedureTests()
		{
            mr = new MockRepository();
            arch = mr.Stub<IProcessorArchitecture>();
            arch.Replay();
		}

		[Test]
		public void CreateProcedure()
		{
			Procedure proc = Procedure.Create(arch, Address.SegPtr(0xBAFE, 0x0123), null);
			Assert.IsTrue(proc.EntryBlock != null);
			Assert.IsTrue(proc.ExitBlock != null);
		}

		[Test]
		public void ProcToString()
		{
			Procedure proc1 = Procedure.Create(arch, Address.SegPtr(0x0F00, 0x0BA9), null);
			Assert.AreEqual("fn0F00_0BA9", proc1.Name);
			Assert.AreEqual("void fn0F00_0BA9()", proc1.ToString());
			Procedure proc2 = Procedure.Create(arch, Address.Ptr32(0x0F000BA9), null);
			Assert.AreEqual("fn0F000BA9", proc2.Name);
			Assert.AreEqual("void fn0F000BA9()", proc2.ToString());
			Procedure proc3 = new Procedure(arch, "foo",  Address.Ptr32(0x00123400), null);
			Assert.AreEqual("foo", proc3.Name);
			Assert.AreEqual("void foo()", proc3.ToString());
		}


		[Test]
		public void ProcCharacteristicIsAlloca()
		{
			Procedure proc = new Procedure(arch, "foo", Address.Ptr32(0x00123400), null);
			Assert.IsFalse(proc.Characteristics.IsAlloca);
		}

	}
}
