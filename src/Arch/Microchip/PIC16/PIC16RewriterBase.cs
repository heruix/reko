﻿#region License
/* 
 * Copyright (C) 2017-2018 Christian Hostelet.
 * inspired by work from:
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
using Reko.Core.Rtl;
using Reko.Core.Types;
using System;

namespace Reko.Arch.MicrochipPIC.PIC16
{
    using Common;

    public abstract class PIC16RewriterBase : PICRewriter
    {

        protected PIC16RewriterBase(PICArchitecture arch, PICDisassemblerBase disasm, PICProcessorState state, IStorageBinder binder, IRewriterHost host)
            : base(arch, disasm, state, binder, host)
        {
        }

        /// <summary>
        /// Actual instruction rewriter method for all PIC16 families.
        /// </summary>
        /// <exception cref="AddressCorrelatedException">Thrown when the Address Correlated error
        ///                                              condition occurs.</exception>
        protected override void RewriteInstr()
        {
            var addr = instrCurr.Address;
            var len = instrCurr.Length;

            switch (instrCurr.Opcode)
            {
            default:
                host.Warn(
                    instrCurr.Address,
                    $"PIC16 instruction {instrCurr.Opcode}' is not supported yet.");
                goto case Opcode.invalid;
            case Opcode.invalid:
            case Opcode.unaligned:
                m.Invalid();
                break;

                case Opcode.ADDLW:
                    Rewrite_ADDLW();
                    break;
                case Opcode.ADDWF:
                    Rewrite_ADDWF();
                    break;
                case Opcode.ANDLW:
                    Rewrite_ANDLW();
                    break;
                case Opcode.ANDWF:
                    Rewrite_ANDWF();
                    break;
                case Opcode.BCF:
                    Rewrite_BCF();
                    break;
                case Opcode.BSF:
                    Rewrite_BSF();
                    break;
                case Opcode.BTFSC:
                    Rewrite_BTFSC();
                    break;
                case Opcode.BTFSS:
                    Rewrite_BTFSS();
                    break;
                case Opcode.CALL:
                    Rewrite_CALL();
                    break;
                case Opcode.CLRF:
                    Rewrite_CLRF();
                    break;
                case Opcode.CLRW:
                    Rewrite_CLRW();
                    break;
                case Opcode.CLRWDT:
                    Rewrite_CLRWDT();
                    break;
                case Opcode.COMF:
                    Rewrite_COMF();
                    break;
                case Opcode.DECF:
                    Rewrite_DECF();
                    break;
                case Opcode.DECFSZ:
                    Rewrite_DECFSZ();
                    break;
                case Opcode.GOTO:
                    Rewrite_GOTO();
                    break;
                case Opcode.INCF:
                    Rewrite_INCF();
                    break;
                case Opcode.INCFSZ:
                    Rewrite_INCFSZ();
                    break;
                case Opcode.IORLW:
                    Rewrite_IORLW();
                    break;
                case Opcode.IORWF:
                    Rewrite_IORWF();
                    break;
                case Opcode.MOVF:
                    Rewrite_MOVF();
                    break;
                case Opcode.MOVLW:
                    Rewrite_MOVLW();
                    break;
                case Opcode.MOVWF:
                    Rewrite_MOVWF();
                    break;
                case Opcode.NOP:
                    m.Nop();
                    break;
                case Opcode.RETFIE:
                    Rewrite_RETFIE();
                    break;
                case Opcode.RETLW:
                    Rewrite_RETLW();
                    break;
                case Opcode.RETURN:
                    Rewrite_RETURN();
                    break;
                case Opcode.RLF:
                    Rewrite_RLF();
                    break;
                case Opcode.RRF:
                    Rewrite_RRF();
                    break;
                case Opcode.SLEEP:
                    Rewrite_SLEEP();
                    break;
                case Opcode.SUBLW:
                    Rewrite_SUBLW();
                    break;
                case Opcode.SUBWF:
                    Rewrite_SUBWF();
                    break;
                case Opcode.SWAPF:
                    Rewrite_SWAPF();
                    break;
                case Opcode.XORLW:
                    Rewrite_XORLW();
                    break;
                case Opcode.XORWF:
                    Rewrite_XORWF();
                    break;


                // Pseudo-instructions
                case Opcode.__CONFIG:
                case Opcode.DA:
                case Opcode.DB:
                case Opcode.DE:
                case Opcode.DT:
                case Opcode.DTM:
                case Opcode.DW:
                case Opcode.__IDLOCS:
                    m.Invalid();
                    break;
            }

        }

        protected override void SetStatusFlags(Expression dst)
        {
            FlagM flags = PIC16CC.Defined(instrCurr.Opcode);
            if (flags != 0)
                m.Assign(FlagGroup(flags), m.Cond(dst));
        }

        protected void GetSrc(out Expression srcMem)
        {
            var src = instrCurr.op1 as PICOperandBankedMemory ?? throw new InvalidOperationException($"Invalid memory operand: {instrCurr.op1}");
            GetUnaryPtrs(src, out srcMem);
        }

        protected void GetSrcAndDst(out Expression srcMem, out Expression dstMem)
        {
            GetSrc(out srcMem);
            var dst = instrCurr.op2 as PICOperandMemWRegDest ?? throw new InvalidOperationException($"Invalid destination operand: {instrCurr.op2}");
            dstMem = dst.WRegIsDest ? Wreg : srcMem;
        }

        private void Rewrite_ADDLW()
        {
            var k = instrCurr.op1 as PICOperandImmediate ?? throw new InvalidOperationException($"Invalid immediate operand: {instrCurr.op1}");
            m.Assign(Wreg, m.IAdd(Wreg, k.ImmediateValue));
            SetStatusFlags(Wreg);
        }

        private void Rewrite_ADDWF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.IAdd(Wreg, srcMem));
            SetStatusFlags(dstMem);
        }

        private void Rewrite_ANDLW()
        {
            var k = instrCurr.op1 as PICOperandImmediate ?? throw new InvalidOperationException($"Invalid immediate operand: {instrCurr.op1}");
            m.Assign(Wreg, m.And(Wreg, k.ImmediateValue));
            SetStatusFlags(Wreg);
        }

        private void Rewrite_ANDWF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.And(Wreg, srcMem));
            SetStatusFlags(dstMem);
        }

        private void Rewrite_BCF()
        {
            GetSrc(out var srcMem);
            var mask = GetBitMask(instrCurr.op2, true);
            m.Assign(srcMem, m.And(srcMem, mask));
        }

        private void Rewrite_BSF()
        {
            GetSrc(out var srcMem);
            var mask = GetBitMask(instrCurr.op2, false);
            m.Assign(srcMem, m.Or(srcMem, mask));
        }

        private void Rewrite_BTFSC()
        {
            rtlc = InstrClass.ConditionalTransfer;
            GetSrc(out var srcMem);
            var mask = GetBitMask(instrCurr.op2, false);
            var res = m.And(srcMem, mask);
            m.Branch(m.Eq0(res), SkipToAddr(), rtlc);
        }

        private void Rewrite_BTFSS()
        {
            rtlc = InstrClass.ConditionalTransfer;
            GetSrc(out var srcMem);
            var mask = GetBitMask(instrCurr.op2, false);
            var res = m.And(srcMem, mask);
            m.Branch(m.Ne0(res), SkipToAddr(), rtlc);
        }

        private void Rewrite_CALL()
        {
            rtlc = InstrClass.Transfer | InstrClass.Call;
            var target = instrCurr.op1 as PICOperandProgMemoryAddress ?? throw new InvalidOperationException($"Invalid program address operand: {instrCurr.op1}");
            Address retaddr = instrCurr.Address + instrCurr.Length;
            var dst = PushToHWStackAccess();
            m.Assign(dst, retaddr);
            m.Call(target.CodeTarget, 0);
        }

        private void Rewrite_CLRF()
        {
            GetSrc(out var srcMem);
            m.Assign(srcMem, Constant.Byte(0));
            m.Assign(binder.EnsureFlagGroup(PICRegisters.Z), Constant.Bool(true));
        }

        private void Rewrite_CLRW()
        {
            m.Assign(Wreg, Constant.Byte(0));
            m.Assign(binder.EnsureFlagGroup(PICRegisters.Z), Constant.Bool(true));
        }

        private void Rewrite_CLRWDT()
        {
            byte mask;

            PICRegisterBitFieldStorage pd = PICRegisters.PD;
            PICRegisterBitFieldStorage to = PICRegisters.TO;
            var status = binder.EnsureRegister(PICRegisters.STATUS);
            mask = (byte)((1 << pd.BitPos) | (1 << to.BitPos));
            m.Assign(status, m.Or(status, Constant.Byte(mask)));
        }

        private void Rewrite_COMF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.Comp(srcMem));
            SetStatusFlags(dstMem);
        }

        private void Rewrite_DECF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.ISub(srcMem, Constant.Byte(1)));
            SetStatusFlags(dstMem);
        }

        private void Rewrite_DECFSZ()
        {
            rtlc = InstrClass.ConditionalTransfer;
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.ISub(srcMem, Constant.Byte(1)));
            m.Branch(m.Eq0(dstMem), SkipToAddr(), rtlc);
        }

        private void Rewrite_GOTO()
        {
            rtlc = InstrClass.Transfer;
            var target = instrCurr.op1 as PICOperandProgMemoryAddress ?? throw new InvalidOperationException($"Invalid program address operand: {instrCurr.op1}");
            m.Goto(target.CodeTarget);
        }

        private void Rewrite_INCF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.IAdd(srcMem, Constant.Byte(1)));
            SetStatusFlags(dstMem);
        }

        private void Rewrite_INCFSZ()
        {
            rtlc = InstrClass.ConditionalTransfer;
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.IAdd(srcMem, Constant.Byte(1)));
            m.Branch(m.Eq0(dstMem), SkipToAddr(), rtlc);
        }

        private void Rewrite_IORLW()
        {
            var k = instrCurr.op1 as PICOperandImmediate ?? throw new InvalidOperationException($"Invalid immediate operand: {instrCurr.op1}");
            m.Assign(Wreg, m.Or(Wreg, k.ImmediateValue));
            SetStatusFlags(Wreg);
        }

        private void Rewrite_IORWF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.Or(Wreg, srcMem));
            SetStatusFlags(dstMem);
        }

        private void Rewrite_MOVF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, srcMem);
            SetStatusFlags(dstMem);
        }

        private void Rewrite_MOVLW()
        {
            var k = instrCurr.op1 as PICOperandImmediate ?? throw new InvalidOperationException($"Invalid immediate operand: {instrCurr.op1}");
            m.Assign(Wreg, k.ImmediateValue);
            SetStatusFlags(Wreg);
        }

        private void Rewrite_MOVWF()
        {
            GetSrc(out var srcMem);
            m.Assign(srcMem, Wreg);
            SetStatusFlags(srcMem);
        }

        private void Rewrite_RETFIE()
        {
            rtlc = InstrClass.Transfer;
            PICRegisterBitFieldStorage gie = PIC16Registers.GIE;
            byte mask = (byte)(1 << gie.BitPos);
            var intcon = binder.EnsureRegister(PIC16Registers.INTCON);
            m.Assign(intcon, m.Or(intcon, Constant.Byte(mask)));
            PopFromHWStackAccess();
            m.Return(0, 0);
        }

        private void Rewrite_RETLW()
        {
            rtlc = InstrClass.Transfer;
            var k = instrCurr.op1 as PICOperandImmediate ?? throw new InvalidOperationException($"Invalid immediate operand: {instrCurr.op1}");
            m.Assign(Wreg, k.ImmediateValue);
            PopFromHWStackAccess();
            m.Return(0, 0);
        }

        private void Rewrite_RETURN()
        {
            rtlc = InstrClass.Transfer;
            PopFromHWStackAccess();
            m.Return(0, 0);
        }

        private void Rewrite_RLF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.Fn(host.PseudoProcedure("__rlf", PrimitiveType.Byte, srcMem)));
        }

        private void Rewrite_RRF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.Fn(host.PseudoProcedure("__rrf", PrimitiveType.Byte, srcMem)));
        }

        private void Rewrite_SLEEP()
        {
            m.Nop();
        }

        private void Rewrite_SUBLW()
        {
            var k = instrCurr.op1 as PICOperandImmediate ?? throw new InvalidOperationException($"Invalid immediate operand: {instrCurr.op1}");
            m.Assign(Wreg, m.ISub(k.ImmediateValue, Wreg));
            SetStatusFlags(Wreg);
        }

        private void Rewrite_SUBWF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.ISub(srcMem, Wreg));
            SetStatusFlags(dstMem);
        }

        private void Rewrite_SWAPF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.Fn(host.PseudoProcedure("__swapf", PrimitiveType.Byte, srcMem)));
        }

        private void Rewrite_XORLW()
        {
            var k = instrCurr.op1 as PICOperandImmediate ?? throw new InvalidOperationException($"Invalid immediate operand: {instrCurr.op1}");
            m.Assign(Wreg, m.Xor(Wreg, k.ImmediateValue));
            SetStatusFlags(Wreg);
        }

        private void Rewrite_XORWF()
        {
            GetSrcAndDst(out var srcMem, out var dstMem);
            m.Assign(dstMem, m.Xor(Wreg, srcMem));
            SetStatusFlags(dstMem);
        }

    }

}
