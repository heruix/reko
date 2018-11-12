using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Rtl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Arch.zSeries
{
    public partial class zSeriesRewriter
    {
        private void RewriteBasr()
        {
            this.rtlc = InstrClass.Transfer | InstrClass.Call;
            var lr = Reg(instr.Ops[0]);
            m.Assign(lr, instr.Address + instr.Length);
            var dst = Reg(instr.Ops[1]);
            m.Call(dst, 0);
        }

        private void RewriteBr()
        {
            this.rtlc = InstrClass.Transfer;
            var dst = Reg(instr.Ops[0]);
            m.Goto(dst);
        }

        private void RewriteBranch(ConditionCode condCode)
        {
            this.rtlc = InstrClass.ConditionalTransfer;
            this.rtlc = InstrClass.ConditionalTransfer;
            var dst = Reg(instr.Ops[0]);
            var cc = binder.EnsureFlagGroup(Registers.CC);
            m.BranchInMiddleOfInstruction(m.Test(condCode, cc).Invert(), instr.Address + instr.Length, rtlc);
            m.Goto(dst);
        }

        private void RewriteBrasl()
        {
            this.rtlc = InstrClass.Transfer | InstrClass.Call;
            var dst = Reg(instr.Ops[0]);
            m.Assign(dst, instr.Address + instr.Length);
            m.Call(Addr(instr.Ops[1]), 0);
        }

        private void RewriteBrctg()
        {
            this.rtlc = InstrClass.ConditionalTransfer;
            var reg = Reg(instr.Ops[0]);
            m.Assign(reg, m.ISubS(reg, 1));
            var ea = EffectiveAddress(instr.Ops[1]);
            if (ea is Address addr)
            {
                m.Branch(m.Ne0(reg), addr, rtlc);
            }
            else
            {
                EmitUnitTest();
                m.Invalid();
            }
        }

        private void RewriteJ()
        {
            this.rtlc = InstrClass.Transfer;
            var dst = Addr(instr.Ops[0]);
            m.Goto(dst);
        }

        private void RewriteJcc(ConditionCode condCode)
        {
            this.rtlc = InstrClass.ConditionalTransfer;
            var dst = Addr(instr.Ops[0]);
            var cc = binder.EnsureFlagGroup(Registers.CC);
            m.Branch(m.Test(condCode, cc), dst, rtlc);
        }
    }
}
