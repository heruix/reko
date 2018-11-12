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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Rtl;
using Reko.Core.Services;
using Reko.Core.Types;

namespace Reko.Scanning
{
    using block = ScanResults.block;
    using instr = ScanResults.instr;
    using link = ScanResults.link;

    public class ScannerInLinq
    {
        // change @binary_size to simulate a large executable
        //private const int binary_size = 10;
        private IServiceProvider services;
        private Program program;
        private IRewriterHost host;
        private DecompilerEventListener eventListener;
        private ScanResults sr;

        public ScannerInLinq(IServiceProvider services, Program program, IRewriterHost host, DecompilerEventListener eventListener)
        {
            this.services = services;
            this.program = program;
            this.host = host;
            this.eventListener = eventListener;
        }

        #region Simulate binary
        public void SimulateBinary(int binary_size)
        {
            sr.FlatInstructions = new SortedList<Address, instr>();
            // links betweeen instructions
            sr.FlatEdges = new List<link>();


            // Create a simulated program ----------------------------------------------------

            long @offset = 0x40000;
            long @end = @offset + @binary_size;
            while (@offset < @end)
            {
                AddInstr(1 + @offset, 1, 'l'); ;
                AddLink(1 + @offset, 2 + @offset);

                AddInstr(2 + @offset, 1, 'l'); ;
                AddLink(2 + @offset, 4 + @offset);

                AddInstr(3 + @offset, 1, 'C'); ; // Capital 'C' means this was called by someone.
                AddLink(3 + @offset, 4 + @offset);

                AddInstr(4 + @offset, 1, 'c');
                AddLink(4 + @offset, 5 + @offset);
                AddLink(4 + @offset, 6 + @offset);

                AddInstr(5 + @offset, 1, 'l');
                AddLink(5 + @offset, 6 + @offset);

                AddInstr(6 + @offset, 1, 'x');
                AddLink(6 + @offset, 2 + @offset);
                AddLink(6 + @offset, 7 + @offset);

                AddInstr(7 + @offset, 1, 'l');
                AddLink(7 + @offset, 8 + @offset);

                AddInstr(8 + @offset, 1, 'l');
                AddLink(8 + @offset, 9 + @offset);

                AddInstr(9 + @offset, 1, 'l');
                AddLink(9 + @offset, 10 + @offset);

                AddInstr(10 + @offset, 1, 'l');

                @offset = @offset + 20;

            }
        }
        #endregion

        public ScanResults ScanImage(ScanResults sr)
        {
            this.sr = sr;

            // sr.WatchedAddresses.Add(Address.Ptr32(0x00404F5C)); //$DEBUG

            // At this point, we have some entries in the image map
            // that are data, and unscanned ranges in betweeen. We
            // have hopefully a bunch of procedure addresses to
            // break up the unscanned ranges.

            if (ScanInstructions(sr) == null)
                return sr;

            var the_blocks = BuildBasicBlocks(sr);
            sr.BreakOnWatchedAddress(the_blocks.Select(q => q.Key));
            the_blocks = RemoveInvalidBlocks(sr, the_blocks);

            // Remove blocks that fall off the end of the segment
            // or into data.
            Probe(sr);
            sr.ICFG = BuildIcfg(sr, program.NamingPolicy, the_blocks);
            Probe(sr);
            sr.Dump("After shingle scan");

            // On processors with variable length instructions,
            // there may be many blocks that partially overlap the 
            // "real" blocks that would actually have been executed
            // by the processor. Starting with known "roots", try to
            // remove as many invalid blocks as possible.

            var hsc = new BlockConflictResolver(
                program,
                sr,
                program.SegmentMap.IsValidAddress,
                host);
            Probe(sr);
            hsc.ResolveBlockConflicts(sr.KnownProcedures.Concat(sr.DirectlyCalledAddresses.Keys));
            Probe(sr);
            sr.Dump("After block conflict resolution");

            // If we detect padding bytes between blocks, 
            // we remove them now.
            var ppf = new ProcedurePaddingFinder(sr);
            var pads = ppf.FindPaddingBlocks();
            ppf.Remove(pads);

            var pd = new ProcedureDetector(program, sr, this.eventListener);
            var procs = pd.DetectProcedures();
            sr.Procedures = procs;
            sr.RemovedPadding = pads;
            return sr;
        }

        public ScanResults ScanInstructions(ScanResults sr)
        {
            var ranges = FindUnscannedRanges().ToList();
            DumpRanges(ranges);
            var binder = new StorageBinder();
            var shsc = new ShingledScanner(this.program, this.host, binder, sr, this.eventListener);
            bool unscanned = false;
            foreach (var range in ranges)
            {
                unscanned = true;
                try
                {
                    shsc.ScanRange(range.Item1,
                        range.Item2,
                        range.Item3,
                        range.Item3.ToLinear() - range.Item2.ToLinear());
                }
                catch (AddressCorrelatedException aex)
                {
                    host.Error(aex.Address, aex.Message);
                }
            }
            if (!unscanned)
            {
                // No unscanned blocks were found.
                return null;
            }
            shsc.Dump("After shingle scan graph built");
            return sr;
        }

        [Conditional("DEBUG")]
        private void DumpRanges(List<Tuple<MemoryArea, Address, Address>> ranges)
        {
            foreach (var range in ranges)
            {
                Debug.Print("{0} - {1}", range.Item2, range.Item3);
            }
        }

        [Conditional("DEBUG")]
        public void Probe(ScanResults sr)
        {
            sr.BreakOnWatchedAddress(sr.ICFG.Nodes.Select(n => n.Address));
        }

        /// <summary>
        /// Scans the Program object looking for address ranges that have not
        /// been identified as code/data yet.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Tuple<MemoryArea, Address, Address>> FindUnscannedRanges()
        {
            return this.program.ImageMap.Items
                .Where(de => de.Value.DataType is UnknownType)
                .Select(de => CreateUnscannedArea(de))
                .Where(tup => tup != null);
        }

        private Tuple<MemoryArea, Address, Address> CreateUnscannedArea(KeyValuePair<Address, ImageMapItem> de)
        {
            if (!this.program.SegmentMap.TryFindSegment(de.Key, out ImageSegment seg))
                return null;
            if (!seg.IsExecutable)
                return null;
            return Tuple.Create(
                seg.MemoryArea,
                de.Key,
                de.Key + de.Value.Size);
        }

        private void BuildWeaklyConnectedComponents(Dictionary<long, block> the_blocks)
        {
            while (true)
            {
                // Find all links that connect instructions that have
                // different components
                var components_to_merge_raw =
                    (from link in sr.FlatEdges
                     join t1 in the_blocks.Values on link.first equals t1.id
                     join t2 in the_blocks.Values on link.second equals t2.id
                     where t1.component_id != t2.component_id
                     select new link { first = t1.component_id, second = t2.component_id })
                    .Distinct();
                // Ensure symmetry (only for WCC, SCC should remove this)
                var components_to_merge =
                    components_to_merge_raw
                    .Concat(components_to_merge_raw.Select(c => new link { first = c.second, second = c.first }))
                    .Distinct()
                    .ToList();

                if (components_to_merge.Count == 0)
                    break;

                foreach (var bc in
                    (from bb in the_blocks.Values
                     join cc in (
                        from c in components_to_merge
                        group c by c.first into g
                        select new
                        {
                            source = g.Key,
                            target = g.Min(gg => gg.second)
                        }) on bb.component_id equals cc.source
                     select new { block = bb, cc.target }))
                {
                    bc.block.component_id = Address.Min(bc.block.component_id, bc.target);
                }
            }
            Debug.Print("comp: {0}",
                string.Join("\r\n      ",
                from b in the_blocks.Values
                group b by b.component_id into g
                orderby g.Key
                select string.Format("{0:X8}:{1}", g.Key, g.Count())));
        }

        /// <summary>
        /// From the "soup" of instructions and links, we construct
        /// basic blocks by finding those instructions that have 0 or more than
        /// 1 predecessor or successors. These instructions delimit the start and 
        /// end of the basic blocks.
        /// </summary>
        public static Dictionary<Address, block> BuildBasicBlocks(ScanResults sr)
        {
            // Count and save the # of successors for each instruction.
            foreach (var cSucc in
                    from link in sr.FlatEdges
                    group link by link.first into g
                    select new { addr = g.Key, Count = g.Count() })
            {
                if (sr.FlatInstructions.TryGetValue(cSucc.addr, out var instr))
                    instr.succ = cSucc.Count;
            }
            // Count and save the # of predecessors for each instruction.
            foreach (var cPred in
                    from link in sr.FlatEdges
                    group link by link.second into g
                    select new { addr = g.Key, Count = g.Count() })
            {
                if (sr.FlatInstructions.TryGetValue(cPred.addr, out var instr))
                    instr.pred = cPred.Count;
            }

            var the_excluded_edges = new HashSet<link>();
            foreach (var instr in sr.FlatInstructions.Values)
            {
                // All blocks must start with a linear instruction.
                if ((instr.type & (ushort)InstrClass.Linear) == 0)
                    continue;
                // Find the instruction that is located directly after instr.
                if (!sr.FlatInstructions.TryGetValue(instr.addr + instr.size, out instr succ))
                    continue;
                // If the first instruction was padding the next one must also be padding, 
                // otherwise we start a new block.
                if (((instr.type ^ succ.type) & (ushort)InstrClass.Padding) != 0)
                    continue;
                // If succ follows instr and it's not the entry of a known procedure
                // or a called address, we don't need the edge between them since they're inside
                // a basic block. We also mark succ as belonging to the same block as instr.
                // Since we're iterating through FlatInstructions in ascending address order,
                // the block_id's will propagate from the first instruction in each block 
                // to the next.
                if (instr.succ == 1 && succ.pred == 1 &&
                    !sr.KnownProcedures.Contains(succ.addr) &&
                    !sr.DirectlyCalledAddresses.ContainsKey(succ.addr))
                {
                    succ.block_id = instr.block_id;
                    the_excluded_edges.Add(new link { first = instr.addr, second = succ.addr });
                }
            }

            // Build the blocks by grouping the instructions.
            var the_blocks =
                (from i in sr.FlatInstructions.Values
                 group i by i.block_id into g
                 select new block
                 {
                     id = g.Key,
                     instrs = g.OrderBy(ii => ii.addr).ToArray()
                 })
                .ToDictionary(b => b.id);
            // Exclude the now useless edges.
            sr.FlatEdges = 
                (from link in sr.FlatEdges
                join f in sr.FlatInstructions.Values on link.first equals f.addr
                where !the_excluded_edges.Contains(link)
                select new link { first = f.block_id, second = link.second })
                .Distinct()
                .ToList();
            return the_blocks;
        }

        public static Dictionary<Address, block> RemoveInvalidBlocks(ScanResults sr, Dictionary<Address, block> blocks)
        {
            // Find transitive closure of bad instructions 

            var bad_blocks =
                (from i in sr.FlatInstructions.Values
                 where i.type == (ushort)InstrClass.Invalid
                 select i.block_id).ToHashSet();
            var new_bad = bad_blocks;
            var preds = sr.FlatEdges.ToLookup(e => e.second);
            //Debug.Print("Bad {0}",
            //    string.Join(
            //        "\r\n      ",
            //        bad_blocks
            //            .OrderBy(x => x)
            //            .Select(x => string.Format("{0:X8}", x))));
            for (;;)
            {
                // Find all blocks that are reachable from blocks
                // that already are known to be "bad", but that don't
                // end in a call.
                //$TODO: delay slots. @#$#@
                new_bad = new_bad
                    .SelectMany(bad => preds[bad])
                    .Where(l => 
                        !bad_blocks.Contains(l.first)
                        &&
                        !BlockEndsWithCall(blocks[l.first]))
                    .Select(l => l.first)
                    .ToHashSet();

                if (new_bad.Count == 0)
                    break;

                //Debug.Print("new {0}",
                //    string.Join(
                //        "\r\n      ",
                //        bad_blocks
                //            .OrderBy(x => x)
                //            .Select(x => string.Format("{0:X8}", x))));

                bad_blocks.UnionWith(new_bad);
            }
            Debug.Print("Bad blocks: {0} of {1}", bad_blocks.Count, blocks.Count);
            //DumpBadBlocks(sr, blocks, sr.FlatEdges, bad_blocks);

            // Remove edges to bad blocks and bad blocks.
            sr.FlatEdges = sr.FlatEdges
                .Where(e => !bad_blocks.Contains(e.second))
                .ToList();
            blocks = blocks.Values
                .Where(b => !bad_blocks.Contains(b.id))
                .ToDictionary(k => k.id);

            return blocks;
        }

        private static bool BlockEndsWithCall(block block)
        {
            int len = block.instrs.Length;
            if (len < 1)
                return false;
            if (block.instrs[len - 1].type == (uint)(InstrClass.Call | InstrClass.Transfer))
                return true;
            return false;
        }

        public static DiGraph<RtlBlock> BuildIcfg(
            ScanResults sr,
            NamingPolicy namingPolicy,
            Dictionary<Address, block> blocks)
        {
            var icfg = new DiGraph<RtlBlock>();
            var map = new Dictionary<Address, RtlBlock>();
            var rtlBlocks = 
                from b in blocks.Values
                join i in sr.FlatInstructions.Values on b.id equals i.block_id into instrs
                orderby b.id
                select new RtlBlock(b.id, namingPolicy.BlockName(b.id))
                {
                    Instructions = instrs.Select(x => x.rtl).ToList()
                };

            foreach (var rtlBlock in rtlBlocks)
            {
                map[rtlBlock.Address] = rtlBlock;
                icfg.AddNode(rtlBlock);
            }
            foreach (var edge in sr.FlatEdges)
            {
                if (!map.TryGetValue(edge.first, out var from) ||
                    !map.TryGetValue(edge.second, out var to))
                    continue;
                icfg.AddEdge(from, to);
            }
            return icfg;
        }

        void DumpInstructions(ScanResults sr)
        {
            Debug.WriteLine(
                string.Join("\r\n",
                    from instr in sr.FlatInstructions.Values
                    join e in sr.FlatEdges on instr.addr equals e.first into es
                    from e in new[] { string.Join(", ", es.Select(ee => string.Format("{0:X8}", ee.second))) }
                    orderby instr.addr
                    select string.Format(
                        "{0:X8} {1} {2} {3}",
                            instr.addr,
                            instr.size,
                            (char)(instr.type + 'A'),
                            e)));
        }

        private void DumpBlocks(ScanResults sr, Dictionary<Address, block> blocks)
        {
            DumpBlocks(sr, blocks, s => Debug.WriteLine(s));
        }

        // Writes the start and end addresses, size, and successor edges of each block, 
        public void DumpBlocks(ScanResults sr, Dictionary<Address, block> blocks, Action<string> writeLine)
        {
            writeLine(
               string.Join(Environment.NewLine,
               from b in blocks.Values
               join i in (
                    from ii in sr.FlatInstructions.Values
                    group ii by ii.block_id into g
                    select new { block_id = g.Key, max = g.Max(iii => iii.addr.ToLinear() + (uint) iii.size ) })
                    on b.id equals i.block_id
               join l in sr.FlatInstructions.Values on b.id equals l.addr
               join e in sr.FlatEdges on b.id equals e.first into es
               from e in new[] { string.Join(", ", es.Select(ee => string.Format("{0:X8}", ee.second))) }
               orderby b.id
               select string.Format(
                   "{0:X8}-{1:X8} ({2}): {3}{4}",
                       b.id,
                       b.id + (i.max - b.id.ToLinear()),
                       i.max - b.id.ToLinear(),
                       RenderType(b.instrs.Last().type),
                       e)));

            string RenderType(ushort type)
            {
                var t = (InstrClass)type;
                if ((t & InstrClass.Padding) != 0)
                    return "Pad ";
                if ((t & InstrClass.Call) != 0)
                    return "Cal ";
                if ((t & InstrClass.ConditionalTransfer) == InstrClass.ConditionalTransfer)
                    return "Bra ";
                if ((t & InstrClass.Transfer) != 0)
                    return "End";
                return "Lin ";
            }
        }



        private void DumpBadBlocks(ScanResults sr, Dictionary<long, block> blocks, IEnumerable<link> edges, HashSet<Address> bad_blocks)
        {
            Debug.Print(
                "{0}",
                string.Join(Environment.NewLine,
                from b in blocks.Values
                join i in (
                     from ii in sr.FlatInstructions.Values
                     group ii by ii.block_id into g
                     select new { block_id = g.Key, max = g.Max(iii => iii.addr.ToLinear()  + (uint) iii.size) })
                     on b.id equals i.block_id
                join e in edges on b.id equals e.first into es
                from e in new[] { string.Join(", ", es.Select(ee => string.Format("{0:X8}", ee.second))) }
                orderby b.id
                select string.Format(
                    "{0:X8}-{1:X8} {2} ({3}): {4}",
                        b.id,
                        b.id + (i.max  - b.id.ToLinear()),
                        bad_blocks.Contains(b.id) ? "*" : " ",
                        i.max - b.id.ToLinear(),
                        e)));
        }

        [Conditional("DEBUG")]
        private void Dump(Dictionary<long, block> the_blocks)
        {
            foreach (var block in the_blocks.Values)
            {
                Debug.Print("{0:X8} - component {1:X8}", block.id, block.component_id);
            }
        }

        [Conditional("DEBUG")]
        private void Dump(IEnumerable<link> edges)
        {
            foreach (var link in edges)
            {
                Debug.Print("[{0:X8} -> {1:X8}]", link.first, link.second);
            }
        }

        [Conditional("DEBUG")]
        private void Dump(IEnumerable<instr> blocks)
        {
            foreach (var i in blocks)
            {
                Debug.Print("{0:X8} {1} {2} {3:X8} {4,2} {5,2}]",
                    i.addr, i.size, i.type, i.block_id, i.pred, i.succ);
            }
        }

        [Conditional("DEBUG")]
        private void AddInstr(long uAddr, int size, char type)
        {
            var addr = Address.Ptr32((uint)uAddr);
            sr.FlatInstructions.Add(addr, new instr
            {
                addr = addr,
                size = size,
                type = type,
                block_id = addr,
            });
        }

        [Conditional("DEBUG")]
        private void AddLink(long from, long to)
        {
            var f = Address.Ptr32((uint)from);
            var t = Address.Ptr32((uint)to);

            sr.FlatEdges.Add(new link { first = f, second = t });
        }

    }
}
