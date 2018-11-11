using Reko.Core;
using Reko.WindowsItp.Decoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Reko.WindowsItp
{
    public partial class DecoderPerformanceDialog : Form
    {
        public DecoderPerformanceDialog()
        {
            InitializeComponent();
        }

        private async void btnDoit_Click(object sender, EventArgs e)
        {
            lblTest.Text = "Running...";
            try
            {
                byte[] buf = ReadBytes();
                if (buf == null)
                    return;
                Func<long> test = SelectTest(buf);
                long msec = await Task.Run(test);
                double instrs_msec = msec / (double) (buf.Length / 4000);
                lblTest.Text = $"Done in {msec}ms; {instrs_msec,3} usec/instr";
            }
            catch
            {
                lblTest.Text = "Threw an exception innit?";
            }
        }

        private Func<long> SelectTest(byte[] buf)
        {
            var rewriter = chkRewriter.Checked;
            Func<long> test;
            if (rdbRealDasm.Checked)
            {
                if (rewriter)
                {
                    test = () => PerformanceTest_A32Rewriter(buf);
                }
                else
                {
                    test = () => PerformanceTest_A32Dasm(buf);
                }
            }
            else
            {
                DecoderBuilder m;
                if (rdbInterpretedDasm.Checked)
                {
                    m = new Decoders.FormatDecoderBuilder();
                }
                else
                {
                    m = new Decoders.ThreadedDecoderBuilder();
                }
                var root = m.Mask(29, 3,
                    m.Instr(Opcode.add, "r8,r4,r0"),
                    m.Instr(Opcode.sub, "r8,r4,r0"),
                    m.Instr(Opcode.mul, "r8,r4,r0"),
                    m.Instr(Opcode.div, "r8,r4,r0"),

                    m.Instr(Opcode.and, "r8,r4,r0"),
                    m.Instr(Opcode.or, "r8,r4,r0"),
                    m.Instr(Opcode.not, "r8,r4"),
                    m.Instr(Opcode.xor, "r8,r4,r0"));

                if (rewriter)
                {
                    test = () => PerformanceTest_SimulatedRewriter(buf, root);
                }
                else
                {
                    test = () => PerformanceTest_Simulated(buf, root);
                }
            }
            return test;
        }

        private byte[] ReadBytes()
        {
            if (rdbLoadFile.Checked)
            {
                string filename = txtFilename.Text;
                if (string.IsNullOrEmpty(filename))
                    return null;
                var buf = System.IO.File.ReadAllBytes(filename);
                return buf;
            }
            else
            {
                if (!int.TryParse(txtRandomSize.Text, out var bufsize))
                    return null;
                var rnd = new Random(4711);
                var buf = new byte[bufsize];
                rnd.NextBytes(buf);
                return buf;
            }
        }

        private long PerformanceTest_Simulated(byte[] buf, Decoder root)
        {
            var rdr = new BeImageReader(buf);
            var dasm = new Disassembler(rdr, root);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var instr in dasm)
            {
            }
            sw.Stop();
            var time = sw.ElapsedMilliseconds;
            return time;
        }

        private long PerformanceTest_SimulatedRewriter(byte[] buf, Decoder root)
        {
#if NYI
            var rdr = new BeImageReader(buf);
            var dasm = new Disassembler(rdr, root);
            var rewriter = new Rewriter(dasm);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var instr in rewriter)
            {
            }
            sw.Stop();
            var time = sw.ElapsedMilliseconds;
            return time;
#endif
            return 1;
        }

        private long PerformanceTest_A32Dasm(byte[] buf)
        {
            var arch = new Reko.Arch.Arm.Arm32Architecture("arm32");
            var mem = new MemoryArea(Address.Ptr32(0x00100000), buf);
            var rdr = arch.CreateImageReader(mem, mem.BaseAddress);
            var dasm = arch.CreateDisassembler(rdr);
            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var instr in dasm)
            {
            }
            sw.Stop();
            var time = sw.ElapsedMilliseconds;
            return time;

        }

        private long PerformanceTest_A32Rewriter(byte[] buf)
        {
            var arch = new Reko.Arch.Arm.Arm32Architecture("arm32");
            var mem = new MemoryArea(Address.Ptr32(0x00100000), buf);
            var rdr = arch.CreateImageReader(mem, mem.BaseAddress);
            var dasm = arch.CreateRewriter(rdr, arch.CreateProcessorState(), new StorageBinder(),
                new RewriterPerformanceDialog.RewriterHost(new Dictionary<Address, ImportReference>()));
            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var instr in dasm)
            {
            }
            sw.Stop();
            var time = sw.ElapsedMilliseconds;
            return time;

        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}