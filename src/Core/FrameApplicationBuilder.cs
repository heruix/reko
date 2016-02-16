﻿#region License
/* 
 * Copyright (C) 1999-2016 John Källén.
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

using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Core
{
    public class FrameApplicationBuilder : ApplicationBuilder, StorageVisitor<Expression>
    {
        protected IProcessorArchitecture arch;
        protected Frame frame;
        protected bool ensureVariables;

        /// <summary>
        /// Creates an application builder that creates references
        /// to the identifiers in the frame.
        /// </summary>
        /// <param name="arch">The processor architecture to use.</param>
        /// <param name="frame">The Frame of the calling procedure.</param>
        /// <param name="site">The call site of the calling instruction.</param>
        /// <param name="callee">The procedure being called.</param>
        /// <param name="sigCallee">The signature of the procedure being called.</param>
        /// <param name="ensureVariables">If true, creates variables in the <paramref name="frame"/> if needed.</param>
        public FrameApplicationBuilder(
            IProcessorArchitecture arch,
            Frame frame,
            CallSite site,
            Expression callee,
            ProcedureSignature sigCallee,
            bool ensureVariables) :
            base(site, callee, sigCallee)
        {
            this.arch = arch;
            this.site = site;
            this.frame = frame;
            this.callee = callee;
            this.sigCallee = sigCallee;
            this.ensureVariables = ensureVariables;
        }

        public override Expression Bind(Identifier id)
        {
            if (id == null)
                return null;
            return id.Storage.Accept(this);
        }

        public override Identifier BindReturnValue(Identifier id)
        {
            Identifier idRet = null;
            if (id != null)
            {
                idRet = (Identifier)Bind(id);
            }
            return idRet;
        }

        public override OutArgument BindOutArg(Identifier id)
        {
            var actualArg = id.Storage.Accept(this);
            return new OutArgument(frame.FramePointer.DataType, actualArg);
        }

        #region StorageVisitor<Expression> Members

        public Expression VisitFlagGroupStorage(FlagGroupStorage grf)
        {
            return frame.EnsureFlagGroup(grf.FlagRegister, grf.FlagGroupBits, grf.Name, grf.DataType);
        }

        public Expression VisitFlagRegister(FlagRegister freg)
        {
            throw new NotSupportedException();
        }

        public Expression VisitFpuStackStorage(FpuStackStorage fpu)
        {
            return frame.EnsureFpuStackVariable(fpu.FpuStackOffset - site.FpuStackDepthBefore, fpu.DataType);
        }

        public Expression VisitMemoryStorage(MemoryStorage global)
        {
            throw new NotSupportedException(string.Format("A {0} can't be used as a formal parameter.", global.GetType().FullName));
        }

        public Expression VisitStackLocalStorage(StackLocalStorage local)
        {
            throw new NotSupportedException(string.Format("A {0} can't be used as a formal parameter.", local.GetType().FullName));
        }

        public Expression VisitOutArgumentStorage(OutArgumentStorage arg)
        {
            return arg.OriginalIdentifier.Storage.Accept(this);
        }

        public Expression VisitRegisterStorage(RegisterStorage reg)
        {
            return frame.EnsureRegister(reg);
        }

        public Expression VisitSequenceStorage(SequenceStorage seq)
        {
            var h = seq.Head.Storage.Accept(this);
            var t = seq.Tail.Storage.Accept(this);
            var idHead = h as Identifier;
            var idTail = t as Identifier;
            if (idHead != null && idTail != null)
                return frame.EnsureSequence(idHead, idTail, PrimitiveType.CreateWord(idHead.DataType.Size + idTail.DataType.Size));
            throw new NotImplementedException("Handle case when stack parameter is passed.");
        }

        public Expression VisitStackArgumentStorage(StackArgumentStorage stack)
        {
            if (ensureVariables)
                return frame.EnsureStackVariable(
                    stack.StackOffset - (site.StackDepthOnEntry + sigCallee.ReturnAddressOnStack),
                    stack.DataType);
            else
                return arch.CreateStackAccess(frame, stack.StackOffset, stack.DataType);
        }

        public Expression VisitTemporaryStorage(TemporaryStorage temp)
        {
            throw new NotSupportedException(string.Format("A {0} can't be used as a formal parameter.", temp.GetType().FullName));
        }

        #endregion

    }
}
