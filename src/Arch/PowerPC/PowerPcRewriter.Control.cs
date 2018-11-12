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
using Reko.Core.Expressions;
using Reko.Core.Machine;
using Reko.Core.Operators;
using Reko.Core.Rtl;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Arch.PowerPC
{
    public partial class PowerPcRewriter
    {
        private void RewriteB()
        {
            var dst = RewriteOperand(instr.op1);
            m.Goto(dst);
        }

        private void RewriteBc(bool linkRegister)
        {
            throw new NotImplementedException();
        }

        private void RewriteBcctr(bool linkRegister)
        {
            RewriteBranch(linkRegister, binder.EnsureRegister(arch.ctr));
        }

        private void RewriteBl()
        {
            var dst = RewriteOperand(instr.op1);
            var addrDst = dst as Address;
            if (addrDst != null && instr.Address.ToLinear() + 4 == addrDst.ToLinear())
            {
                // PowerPC idiom to get the current instruction pointer in the lr register
                rtlc = InstrClass.Linear;
                m.Assign(binder.EnsureRegister(arch.lr), addrDst);
            }
            else
            {
                m.Call(dst, 0);
            }
        }

        private void RewriteBlr()
        {
            m.Return(0, 0);
        }

        private void RewriteBranch(bool updateLinkregister, bool toLinkRegister, ConditionCode cc)
        {
            var ccrOp = instr.op1 as RegisterOperand;
            Expression cr;
            if (ccrOp != null)
            {
                cr = RewriteOperand(instr.op1);
            }
            else 
            {
                cr = binder.EnsureFlagGroup(arch.GetCcFieldAsFlagGroup(arch.CrRegisters[0]));
            }
            if (toLinkRegister)
            {
                m.BranchInMiddleOfInstruction(
                    m.Test(cc, cr).Invert(),
                    instr.Address + instr.Length,
                    InstrClass.ConditionalTransfer);
                var dst = binder.EnsureRegister(arch.lr);
                if (updateLinkregister)
                {
                    m.Call(dst, 0);
                }
                else
                {
                    m.Return(0, 0);
                }
            }
            else
            {
                var dst = RewriteOperand(ccrOp != null ? instr.op2 : instr.op1);
                if (updateLinkregister)
                {
                    m.BranchInMiddleOfInstruction(
                        m.Test(cc, cr).Invert(),
                        instr.Address + instr.Length,
                        InstrClass.ConditionalTransfer);
                    m.Call(dst, 0);
                }
                else
                {
                    m.Branch(m.Test(cc, cr), (Address)dst, InstrClass.ConditionalTransfer);
                }
            }
        }

        private ConditionCode CcFromOperand(ConditionOperand ccOp)
        {
            switch (ccOp.condition & 3)
            {
            case 0: return ConditionCode.LT;
            case 1: return ConditionCode.GT;
            case 2: return ConditionCode.EQ;
            case 3: return ConditionCode.OV;
            default: throw new NotImplementedException();
            }
        }

        private RegisterStorage CrFromOperand(ConditionOperand ccOp)
        {
            return arch.CrRegisters[(int)ccOp.condition >> 2];
        }
        
        private void RewriteCtrBranch(bool updateLinkRegister, bool toLinkRegister, Func<Expression,Expression,Expression> decOp, bool ifSet)
        {
            var ctr = binder.EnsureRegister(arch.ctr);
            Expression dest;

            Expression cond = decOp(ctr, Constant.Zero(ctr.DataType));

            if (instr.op1 is ConditionOperand ccOp)
            {
                Expression test = m.Test(
                    CcFromOperand(ccOp),
                    binder.EnsureRegister(CrFromOperand(ccOp)));
                if (!ifSet)
                    test = test.Invert();
                cond = m.Cand(cond, test);
                dest = RewriteOperand(instr.op2);
            }
            else
            {
                dest = RewriteOperand(instr.op1);
            }
            
            m.Assign(ctr, m.ISub(ctr, 1));
            if (updateLinkRegister)
            {
                m.BranchInMiddleOfInstruction(
                    cond.Invert(),
                    instr.Address + instr.Length,
                    InstrClass.ConditionalTransfer);
                m.Call(dest, 0);
            }
            else
            {
                m.Branch(
                    cond,
                    (Address)dest,
                    InstrClass.ConditionalTransfer);
            }
        }

        private void RewriteBranch(bool linkRegister, Expression destination)
        {
            var ctr = binder.EnsureRegister(arch.ctr);
            var bo = ((Constant)RewriteOperand(instr.op1)).ToByte();
            switch (bo)
            {
            case 0x00:
            case 0x01: throw new NotImplementedException("dec ctr");
            case 0x02:
            case 0x03: throw new NotImplementedException("dec ctr");
            case 0x04:
            case 0x05:
            case 0x06:
            case 0x07: throw new NotImplementedException("condition false");
            case 0x08:
            case 0x09: throw new NotImplementedException("dec ctr; condition false");
            case 0x0A:
            case 0x0B: throw new NotImplementedException("dec ctr; condition false");
            case 0x0C:
            case 0x0D:
            case 0x0E:
            case 0x0F: throw new NotImplementedException("condition true");
            case 0x10:
            case 0x11:
            case 0x18:
            case 0x19: throw new NotImplementedException("condition true");
            case 0x12:
            case 0x13:
            case 0x1A:
            case 0x1B: throw new NotImplementedException("condition true");
            default:
                if (linkRegister)
                    m.Call(ctr, 0);
                else
                    m.Goto(ctr);
                return;
            }
        }

        private void RewriteSc()
        {
            m.SideEffect(host.PseudoProcedure(PseudoProcedure.Syscall, arch.WordWidth));
        }
    }
}
