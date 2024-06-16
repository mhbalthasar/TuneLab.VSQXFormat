using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TuneLab.Base.Science;
using TuneLab.Base.Structures;
using TuneLab.Extensions.Formats.DataInfo;

namespace TuneLab.Extensions.Formats.VSQX.partSegment
{
    internal class MidiSegmentPartInfo
    {
        public MidiPartInfo MidiPart { get; set; }
        public bool isPitchSnapMode { get; set; }
        public MidiSegmentPartInfo() {}
    }
    internal class MidiPartSegment
    {
        public static List<MidiSegmentPartInfo> Segment(MidiPartInfo part,List<TempoInfo> tempos)
        {
            if (part.Pitch.Count == 0) return new List<MidiSegmentPartInfo>() { new MidiSegmentPartInfo() { isPitchSnapMode=false, MidiPart = part } };

            TempoHelper th = new TempoHelper(tempos);
            double time480 = th.Get120Time(480);
            List<double> SegmentLine = new List<double>() { 0, part.Dur }; 
            {
                Parallel.For(1, part.Notes.Count, (i) =>
                {
                    NoteInfo pn, nn;
                    lock (part.Notes)
                    {
                        pn = part.Notes[i - 1];
                        nn = part.Notes[i];
                    }
                    double pt = th.GetTime(pn.EndPos());
                    double nt = th.GetTime(nn.StartPos());
                    if (nt - pt >= time480)
                    {
                        lock (SegmentLine) { double SegmentTick= ((pn.EndPos() + nn.StartPos()) / 2); if (!SegmentLine.Contains(SegmentTick)) SegmentLine.Add(SegmentTick); }
                    }
                });
                SegmentLine = SegmentLine.OrderBy(i => i).ToList();
            }
            //去除不必要的切分
            {
                int isSnap = -1;
                List<double> RemoveLine = new List<double>();
                for(int i = 1; i < SegmentLine.Count; i++)
                {
                    List<NoteInfo> areaNotes = part.Notes.Where(n => (n.StartPos() >= SegmentLine[(int)i - 1] && n.EndPos() <= SegmentLine[(int)i])).ToList();
                    if (areaNotes.Count > 0)
                    {
                        double[] posBound = new double[2] { areaNotes.First().StartPos(), areaNotes.Last().EndPos() };
                        bool Snap = false;
                        Parallel.ForEach(part.Pitch, (pl,lStatus1) =>
                        {
                            Parallel.ForEach(pl.Where(p => (p.X >= SegmentLine[(int)i - 1] && p.X <= SegmentLine[(int)i])), (p,lStatus2) =>
                            {
                                if (p.X >= posBound[0] && p.X <= posBound[1])
                                {
                                    lock (RemoveLine)Snap = true;
                                    //IsSnap
                                    lStatus1.Stop();
                                    lStatus2.Stop();
                                }
                            });
                        });
                        int newSnap = Snap ? 1 : 0;
                        if (isSnap == newSnap)
                        {
                            RemoveLine.Add(SegmentLine[i - 1]);
                        }
                        else
                        {
                            isSnap = newSnap;
                        }
                    }
                }
                foreach (double rL in RemoveLine) SegmentLine.Remove(rL);
            }
            List<MidiSegmentPartInfo> Parts = new List<MidiSegmentPartInfo>();
            {
                Parallel.For(1, SegmentLine.Count, (i) =>
                {
                    int StartTick = (int)SegmentLine[(int)i - 1];
                    int EndTick = (int)SegmentLine[(int)i];
                    int DurTick = EndTick - StartTick;

                    MidiSegmentPartInfo brk = new MidiSegmentPartInfo() { isPitchSnapMode = false };

                    MidiPartInfo newPart = new MidiPartInfo();
                    newPart.Pos = part.Pos + StartTick;
                    newPart.Dur = DurTick;
                    newPart.Voice = part.Voice;
                    newPart.Vibratos = part.Vibratos;
                    newPart.Name = part.Name + "_Seg" + i.ToString();
                    newPart.Properties = newPart.Properties;
                    newPart.Pitch = new List<List<Point>>();
                    List<NoteInfo> areaNotes = part.Notes.Where(n => (n.StartPos() >= StartTick && n.EndPos() <= EndTick)).ToList();
                    if (areaNotes.Count > 0)
                    {
                        double[] posBound = new double[2] { areaNotes.First().StartPos(), areaNotes.Last().EndPos() };
                        foreach (List<Point> pl in part.Pitch)
                        {
                            List<Point> pA = new List<Point>();
                            Parallel.ForEach(pl.Where(p => (p.X >= StartTick && p.X <= EndTick)), (p) => {
                                if (!brk.isPitchSnapMode && p.X >= posBound[0] && p.X <= posBound[1]) lock (brk) { brk.isPitchSnapMode = true; }//标识Snap模式
                                p.X -= StartTick; 
                                lock (pA) { pA.Add(p); } 
                            });
                            if (pA.Count() > 0)
                            {
                                newPart.Pitch.Add(pA.OrderBy(p => p.X).ToList());
                            }
                        }
                    }
                    newPart.Notes = new List<NoteInfo>();
                    for(int ni=0;ni<areaNotes.Count;ni++)// Parallel.ForEach(areaNotes, (n) =>
                    {
                        NoteInfo n = areaNotes[ni];
                        n.Pos -= StartTick;
                        lock (newPart.Notes) { newPart.Notes.Add(n); }
                    }//);
                    newPart.Notes = newPart.Notes.OrderBy(n => n.StartPos()).ToList();
                    newPart.Automations = new Map<string, AutomationInfo>();
                    foreach(var kv in part.Automations)
                    {
                        List<Point> pA = new List<Point>();
                        Parallel.ForEach(part.Automations[kv.Key].Points.Where(p=> (p.X >= StartTick && p.X <= EndTick)), (p) => { p.X -= StartTick; lock (pA) { pA.Add(p); } });
                        newPart.Automations.Add(kv.Key,new AutomationInfo()
                        {
                            DefaultValue = kv.Value.DefaultValue,
                            Points=pA.OrderBy(p => p.X).ToList()
                        });
                    }

                    brk.MidiPart = newPart;
                    lock (Parts) { Parts.Add(brk); };
                });
            }
            return Parts;
        }
    }
    internal class TempoHelper : ITempoCalculatorHelper
    {
        public class TpoHelper : ITempoHelper
        {
            public double Pos { get; set; }
            public double Bpm { get; set; }
            public double Time { get => (Pos / Coe); }//单位:秒
            public double Coe { get => Bpm / 60.0 * 480.0; }//MIDI文件中每分钟播放Coe个MIDI时钟刻度(Tick)
            public double Pos120 { get => (Pos * 120.0 / Bpm); }//单位:秒
        }
        
        List<ITempoHelper> _tpo;
        public IReadOnlyList<ITempoHelper> Tempos => _tpo;
        public TempoHelper(List<TempoInfo> tempos)
        {
            _tpo = new List<ITempoHelper>();
            foreach(TempoInfo tempo in tempos)
            {
                _tpo.Add(new TpoHelper() { Bpm = tempo.Bpm, Pos = tempo.Pos });
            }
        }
        public double Get120Time(int Tick)
        {
            TpoHelper tpoHelper = new TpoHelper() { Bpm = 120, Pos = Tick };
            return tpoHelper.Time;
        }
    }
}
