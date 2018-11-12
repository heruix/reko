using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Arch.PowerPC
{
    public partial class PowerPcDisassembler
    {
        public abstract class Decoder
        {
            public abstract PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr);
        }

        public class InvalidDecoder : Decoder
        {
            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                return new PowerPcInstruction(Opcode.illegal);
            }
        }

        public class NyiDecoder : Decoder
        {
            private readonly string message;

            public NyiDecoder(string message)
            {
                this.message = message;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                EmitUnitTest(wInstr);
                return new PowerPcInstruction(Opcode.illegal);
            }

            [Conditional("DEBUG")]
            private void EmitUnitTest(uint wInstr)
            {
                Debug.Print("    // {0}", message);
                Debug.Print("    [Test]");
                Debug.Print("    public void PPCDis_{0:X8}()", wInstr);
                Debug.Print("    {");
                Debug.Print("        AssertCode(0x{0:X8}, \"@@@\");", wInstr);
                Debug.Print("    }");
                Debug.Print("");
            }
        }

        public class MaskDecoder : Decoder
        {
            private readonly int shift;
            private readonly uint mask;
            private readonly Decoder[] decoders;

            public MaskDecoder(int ppcBitPosition, int bits, params Decoder[] decoders)
            {
                // Convert awkward PPC bit numbering to something sane.
                this.shift = 32 - (ppcBitPosition + bits);
                this.mask = (1u << bits) - 1;
                Debug.Assert(decoders.Length == (1 << bits));
                this.decoders = decoders;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var bitfield = (wInstr >> shift) & mask;
                return decoders[bitfield].Decode(dasm, wInstr);
            }
        }

        public class FnDecoder : Decoder
        {
            private readonly Func<PowerPcDisassembler, uint, PowerPcInstruction> decoder;

            public FnDecoder(Func<PowerPcDisassembler, uint, PowerPcInstruction> decoder)
            {
                this.decoder = decoder;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                return decoder(dasm, wInstr);
            }
        }

        public class DOpRec : Decoder
        {
            public readonly Opcode opcode;
            public readonly InstrClass iclass;
            public readonly string opFmt;

            public DOpRec(Opcode opcode, string opFmt, InstrClass iclass = InstrClass.Linear)
            {
                this.opcode = opcode;
                this.iclass = iclass;
                this.opFmt = opFmt;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                return dasm.DecodeOperands(opcode, iclass, wInstr, opFmt);
            }
        }

        public class DSOpRec : Decoder
        {
            public readonly Opcode opcode0;
            public readonly Opcode opcode1;
            public readonly InstrClass iclass;
            public readonly string opFmt;

            public DSOpRec(Opcode opcode0, Opcode opcode1, string opFmt, InstrClass iclass = InstrClass.Linear)
            {
                this.opcode0 = opcode0;
                this.opcode1 = opcode1;
                this.iclass = iclass;
                this.opFmt = opFmt;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                Opcode opcode = ((wInstr & 1) == 0) ? opcode0 : opcode1;
                wInstr &= ~3u;
                return dasm.DecodeOperands(opcode, iclass, wInstr, opFmt);
            }
        }

        public class MDOpRec : Decoder
        {
            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                // Only supported on 64-bit arch.
                if (dasm.defaultWordWidth.BitSize == 32)
                {
                    return new PowerPcInstruction(Opcode.illegal);
                }
                else
                {
                    Opcode opcode;
                    switch ((wInstr >> 1) & 0xF)
                    {
                    case 0: case 1: opcode = Opcode.rldicl; break;
                    case 2: case 3: opcode = Opcode.rldicr; break;
                    case 4: case 5: opcode = Opcode.rldic; break;
                    case 6: case 7: opcode = Opcode.rldimi; break;
                    case 8: opcode = Opcode.rldcl; break;
                    case 9: opcode = Opcode.rldcr; break;
                    default: return new PowerPcInstruction(Opcode.illegal);
                    }

                    wInstr &= ~1u;
                    return new PowerPcInstruction(opcode)
                    {
                        iclass = InstrClass.Linear,
                        op1 = dasm.RegFromBits(wInstr >> 16),
                        op2 = dasm.RegFromBits(wInstr >> 21),
                        op3 = ImmediateOperand.Byte((byte)((wInstr >> 11) & 0x1F | (wInstr << 4) & 0x20)),
                        op4 = ImmediateOperand.Byte((byte)((wInstr >> 6) & 0x1F | (wInstr & 0x20))),
                    };
                }
            }
        }

        public class AOpRec : Decoder
        {
            private readonly Dictionary<uint, DOpRec> xOpRecs;

            public AOpRec(Dictionary<uint, DOpRec> xOpRecs)
            {
                this.xOpRecs = xOpRecs;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                return xOpRecs[(wInstr >> 1) & 0x3FF].Decode(dasm, wInstr);
            }
        }

        public class XOpRec : Decoder
        {
            private Dictionary<uint, Decoder> xOpRecs;

            public XOpRec(Dictionary<uint, Decoder> xOpRecs)
            {
                this.xOpRecs = xOpRecs;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var xOp = (wInstr >> 1) & 0x3FF;
                if (xOpRecs.TryGetValue(xOp, out var opRec))
                {
                    return opRec.Decode(dasm, wInstr);
                }
                else
                {
                    Debug.Print("Unknown PowerPC X instruction {0:X8} {1:X2}-{2:X3} ({2})", wInstr, wInstr >> 26, xOp);
                    return dasm.EmitUnknown(wInstr);
                }
            }
        }

        public class FpuOpRec : Decoder
        {
            private readonly Dictionary<uint, Decoder> fpuOpRecs;
            private readonly int shift;
            private readonly uint mask;

            public FpuOpRec(int shift, uint mask, Dictionary<uint, Decoder> fpuOpRecs)
            {
                this.shift = shift;
                this.mask = mask;
                this.fpuOpRecs = fpuOpRecs;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var x = (wInstr >> shift) & mask;
                if (fpuOpRecs.TryGetValue(x, out Decoder opRec))
                {
                    return opRec.Decode(dasm, wInstr);
                }
                else
                    return new PowerPcInstruction(Opcode.illegal);
            }
        }

        public class XlOpRecAux : DOpRec
        {
            private readonly Opcode opLink;

            public XlOpRecAux(Opcode opcode, Opcode opLink, string opFmt)
                : base(opcode, opFmt)
            {
                this.opLink = opLink;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                bool link = (wInstr & 1) != 0;
                var opcode = link ? this.opLink : this.opcode;
                var iclass = link ? InstrClass.Transfer | InstrClass.Call : InstrClass.Transfer;
                return dasm.DecodeOperands(opcode, iclass, wInstr, opFmt);
            }
        }

        public class FpuOpRecAux : Decoder
        {
            private readonly Opcode opcode;
            private readonly InstrClass iclass;
            private readonly string opFmt;

            public FpuOpRecAux(Opcode opcode, string opFmt, InstrClass iclass = InstrClass.Linear)
            {
                this.opcode = opcode;
                this.iclass = iclass;
                this.opFmt = opFmt;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                return dasm.DecodeOperands(opcode, iclass, wInstr, opFmt);
            }
        }

        public class IOpRec : Decoder
        {
            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var opcode = (wInstr & 1) == 1 ? Opcode.bl : Opcode.b;
                var iclass = (wInstr & 1) == 1 ? InstrClass.Transfer | InstrClass.Call : InstrClass.Transfer;
                var uOffset = wInstr & 0x03FFFFFC;
                if ((uOffset & 0x02000000) != 0)
                    uOffset |= 0xFF000000;
                var baseAddr = (wInstr & 2) != 0 ? Address.Create(dasm.defaultWordWidth, 0) : dasm.rdr.Address - 4;
                return new PowerPcInstruction(opcode)
                {
                    iclass = iclass,
                    op1 = new AddressOperand(baseAddr + uOffset),
                };
            }
        }

        public class BOpRec : Decoder
        {
            private static readonly Opcode[] opcBdnzf =
            {
                Opcode.bdnzf, Opcode.bdnzfl
            };

            private static readonly Opcode[] opcBdzf =
            {
                Opcode.bdzf, Opcode.bdzfl
            };

            private static readonly Opcode[,] opcBNcc =
            {
                { Opcode.bge, Opcode.bgel },
                { Opcode.ble, Opcode.blel },
                { Opcode.bne, Opcode.blel },
                { Opcode.bns, Opcode.bnsl },
            };


            private static readonly Opcode[,] opcBcc =
            {
                { Opcode.blt, Opcode.bgel },
                { Opcode.bgt, Opcode.blel },
                { Opcode.beq, Opcode.blel },
                { Opcode.bso, Opcode.bnsl },
            };

            private static readonly Opcode[] opcBdnzt =
            {
                Opcode.bdnzt, Opcode.bdnztl
            };

            private static readonly Opcode[] opcBdzt =
            {
                Opcode.bdzt, Opcode.bdztl
            };

            private static readonly Opcode[] opcBdnz =
            {
                Opcode.bdnz, Opcode.bdnzl
            };

            private static readonly Opcode[] opcBdz =
            {
                Opcode.bdz, Opcode.bdzl
            };

            private static readonly Opcode[] opcB =
            {
                Opcode.b, Opcode.bl
            };

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                uint link = (wInstr & 1);
                var uOffset = wInstr & 0x0000FFFC;
                if ((uOffset & 0x8000) != 0)
                    uOffset |= 0xFFFF0000;
                var grfBi = (wInstr >> 16) & 0x1F;
                var grfBo = (wInstr >> 21) & 0x1F;
                var crf = grfBi >> 2;

                Opcode opcode;
                InstrClass iclass = link == 1 ? InstrClass.Transfer | InstrClass.Call : InstrClass.Transfer;
                MachineOperand op1;
                MachineOperand op2;
                var baseAddr = (wInstr & 2) != 0 ? Address.Create(dasm.defaultWordWidth, 0) : dasm.rdr.Address - 4;
                var dst = new AddressOperand(baseAddr + uOffset);
                switch (grfBo)
                {
                case 0:
                case 1:
                    // Decrement ctr, branch if ctr != 0 and condition is false
                    opcode = opcBdnzf[link];
                    iclass |= InstrClass.Conditional;
                    op1 = new ConditionOperand(grfBi);
                    op2 = dst;
                    break;
                case 2:
                case 3:
                    // Decrement ctr, branch if ctr == 0 and condition is false
                    opcode = opcBdzf[link];
                    iclass |= InstrClass.Conditional;
                    op1 = new ConditionOperand(grfBi);
                    op2 = dst;
                    break;
                case 4:
                case 5:
                case 6:
                case 7:
                    // Branch if condition is false
                    opcode = opcBNcc[grfBi & 0b11, link];
                    iclass |= InstrClass.Conditional;
                    if (grfBi < 4)
                    {
                        op1 = dst;
                        op2 = null;
                    }
                    else
                    {
                        op1 = new RegisterOperand(dasm.arch.CrRegisters[(int)grfBi >> 2]);
                        op2 = dst;
                    }
                    break;
                case 8:
                case 9:
                    // Decrement ctr, branch if ctr != 0 and condition is true
                    opcode = opcBdnzt[link];
                    iclass |= InstrClass.Conditional;
                    op1 = new ConditionOperand(grfBi);
                    op2 = dst;
                    break;
                case 0xA:
                case 0xB:
                    // Decrement ctr, branch if ctr == 0 and condition is true
                    opcode = opcBdzt[link];
                    iclass |= InstrClass.Conditional;
                    op1 = new ConditionOperand(grfBi);
                    op2 = dst;
                    break;
                case 0xC:
                case 0xD:
                case 0xE:
                case 0xF:
                    // Branch if condition is true.
                    opcode = opcBcc[grfBi & 0b11, link];
                    iclass |= InstrClass.Conditional;
                    if (grfBi < 4)
                    {
                        op1 = dst;
                        op2 = null;
                    }
                    else
                    {
                        op1 = new RegisterOperand(dasm.arch.CrRegisters[(int)grfBi >> 2]);
                        op2 = dst;
                    }
                    break;
                case 0b10000:
                case 0b10001:
                case 0b11000:
                case 0b11001:
                    // Decrement ctr, Branch if ctr != 0
                    opcode = opcBdnz[link];
                    iclass |= InstrClass.Conditional;
                    op1 = dst;
                    op2 = null;
                    break;
                case 0b10010:
                case 0b10011:
                case 0b11010:
                case 0b11011:
                    // Decrement ctr, Branch if ctr == 0
                    opcode = opcBdz[link];
                    iclass |= InstrClass.Conditional;
                    op1 = dst;
                    op2 = null;
                    break;
                default:
                    opcode = opcB[link];
                    op1 = dst;
                    op2 = null;
                    break;
                }
                return new PowerPcInstruction(opcode)
                {
                    iclass = iclass,
                    op1 = op1,
                    op2 = op2,
                };
            }
        }

        public class BclrOpRec : Decoder
        {
            public BclrOpRec()
            {
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                bool link = (wInstr & 1) != 0;
                var opcode = link ? Opcode.blrl : Opcode.blr;
                var crBit = (wInstr >> 16) & 0x1F;
                var crf = crBit >> 2;
                var condCode = ((wInstr >> 22) & 4) | (crBit & 0x3);
                var bo = (wInstr >> 21) & 0x1F;
                if ((bo & 0x14) == 0x14)
                {
                    return new PowerPcInstruction(Opcode.blr)
                    {
                        iclass = InstrClass.Transfer
                    };
                }

                var iclass = link ? InstrClass.Call : 0;
                switch (condCode)
                {
                default:
                    return new PowerPcInstruction(link ? Opcode.bclrl : Opcode.bclr)
                    {
                        iclass = iclass | InstrClass.Transfer,
                        op1 = new ImmediateOperand(Constant.Byte((byte)((wInstr >> 21) & 0x1F))),
                        op2 = new ImmediateOperand(Constant.Byte((byte)((wInstr >> 16) & 0x1F))),
                    };
                case 0: opcode = link ? Opcode.bgelrl : Opcode.bgelr; break;
                case 1: opcode = link ? Opcode.blelrl : Opcode.blelr; break;
                case 2: opcode = link ? Opcode.bnelrl : Opcode.bnelr; break;
                case 3: opcode = link ? Opcode.bnslrl : Opcode.bnslr; break;
                case 4: opcode = link ? Opcode.bltlrl : Opcode.bltlr; break;
                case 5: opcode = link ? Opcode.bgtlrl : Opcode.bgtlr; break;
                case 6: opcode = link ? Opcode.beqlrl : Opcode.beqlr; break;
                case 7: opcode = link ? Opcode.bsolrl : Opcode.bsolr; break;
                }
                return new PowerPcInstruction(opcode)
                {
                    iclass = iclass | InstrClass.ConditionalTransfer,
                    op1 = dasm.CRegFromBits(crf),
                };
            }
        }

        public class XfxOpRec : DOpRec
        {
            public XfxOpRec(Opcode opcode, string fmt) : base(opcode, fmt)
            {
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var reg = dasm.RegFromBits(wInstr >> 21);
                var spr = (wInstr >> 11) & 0x3FF;
                return new PowerPcInstruction(opcode)
                {
                    iclass = base.iclass,
                    op1 = reg,
                    op2 = new ImmediateOperand(Constant.Word16((ushort)spr))
                };
            }
        }

        public class SprOpRec : Decoder
        {
            private readonly bool to;

            public SprOpRec(bool to)
            {
                this.to = to;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                MachineOperand op1 = dasm.RegFromBits(wInstr >> 21);
                MachineOperand op2 = null;
                var spr = ((wInstr >> 16) & 0x1F) | ((wInstr >> 6) & 0x3E0);
                Opcode opcode;
                switch (spr)
                {
                case 0x08: opcode = to ? Opcode.mtlr : Opcode.mflr; break;
                case 0x09: opcode = to ? Opcode.mtctr : Opcode.mfctr; break;
                default:
                    opcode = to ? Opcode.mtspr : Opcode.mfspr;
                    op2 = op1;
                    op1 = ImmediateOperand.UInt32(spr);
                    break;
                }
                return new PowerPcInstruction(opcode)
                {
                    iclass = InstrClass.Linear,
                    op1 = op1,
                    op2 = op2
                };
            }
        }

        public class CmpOpRec : DOpRec
        {
            public CmpOpRec(Opcode op, string format) : base(op, format)
            { }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var l = ((wInstr >> 21) & 1) != 0;
                var op = Opcode.illegal;
                switch (this.opcode)
                {
                default: throw new NotImplementedException();
                case Opcode.cmp: op = l ? Opcode.cmpl : Opcode.cmp; break;
                case Opcode.cmpi: op = l ? Opcode.cmpi : Opcode.cmpwi; break;
                case Opcode.cmpl: op = l ? Opcode.cmpl : Opcode.cmplw; break;
                case Opcode.cmpli: op = l ? Opcode.cmpli : Opcode.cmplwi; break;
                }
                return dasm.DecodeOperands(op, iclass, wInstr, opFmt);
            }
        }

        public class VXOpRec : Decoder
        {
            private Dictionary<uint, Decoder> vxOpRecs;
            private Dictionary<uint, Decoder> vaOpRecs;

            public VXOpRec(Dictionary<uint, Decoder> vxOpRecs, Dictionary<uint, Decoder> vaOpRecs)
            {
                this.vxOpRecs = vxOpRecs;
                this.vaOpRecs = vaOpRecs;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var xOp = wInstr & 0x7FFu;
                if (vxOpRecs.TryGetValue(xOp, out Decoder opRec))
                {
                    return opRec.Decode(dasm, wInstr);
                }
                else if (vaOpRecs.TryGetValue(wInstr & 0x3Fu, out opRec))
                {
                    return opRec.Decode(dasm, wInstr);
                }
                else
                {
                    Debug.Print("Unknown PowerPC VX instruction {0:X8} {1:X2}-{2:X3} ({2})", wInstr, wInstr >> 26, xOp);
                    return dasm.EmitUnknown(wInstr);
                }
            }
        }

        public class XSOpRec : DOpRec
        {
            public XSOpRec(Opcode opcode, string format) : base(opcode, format) { }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var instr = base.Decode(dasm, wInstr);
                var c = ((ImmediateOperand)instr.op3).Value.ToInt32();
                if ((wInstr & 2) != 0)
                    c += 32;
                instr.op3 = new ImmediateOperand(Constant.Byte((byte)c));
                return instr;

            }
        }

        public class XX3OpRec : Decoder
        {
            private Dictionary<uint, Decoder> decoders;

            public XX3OpRec(Dictionary<uint, Decoder> xoprecs)
            {
                this.decoders = xoprecs;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var subOp = (wInstr & 0xFFFF) >> 3;
                if (decoders.TryGetValue(subOp, out var decoder))
                {
                    return decoder.Decode(dasm, wInstr);
                }
                else
                {
                    Debug.Print("Unknown PowerPC XX3 instruction {0:X8} {1:X2}-{2:X3} ({2})", wInstr, wInstr >> 26, subOp);
                    return dasm.EmitUnknown(wInstr);
                }
            }
        }

        public class VMXDecoder : Decoder
        {
            private readonly uint mask;
            public Dictionary<uint, Decoder> decoders;

            public VMXDecoder(uint mask, Dictionary<uint, Decoder> decoders)
            {
                this.mask = mask;
                this.decoders = decoders;
            }

            public override PowerPcInstruction Decode(PowerPcDisassembler dasm, uint wInstr)
            {
                var key = (wInstr >> 0x4) & mask;
                if (decoders.TryGetValue(key, out Decoder decoder))
                {
                    return decoder.Decode(dasm, wInstr);
                }
                else
                {
                    Debug.Print("Unknown PowerPC VMX instruction {0:X8} {1:X2}-{2:X3} ({2})", wInstr, wInstr >> 26, key);
                    return dasm.EmitUnknown(wInstr);
                }
            }

            public static PowerPcInstruction DecodeVperm128(PowerPcDisassembler dasm, uint wInstr)
            {
                var instr = dasm.DecodeOperands(Opcode.vperm128, InstrClass.Linear, wInstr, "Wd,Wa,Wb");
                instr.op4 = dasm.VRegFromBits((wInstr >> 6) & 7);
                return instr;
            }
        }
    }
}
