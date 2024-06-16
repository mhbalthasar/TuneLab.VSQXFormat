using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using TuneLab.Base.Science;
using TuneLab.Base.Structures;
using TuneLab.Extensions.Formats.DataInfo;

namespace TuneLab.Extensions.Formats.VSQX.partSegment
{
    internal class SnapModeCurve
    {
        TempoHelper tUtils;
        public SnapModeCurve(List<TempoInfo> tempos) { this.tUtils = new TempoHelper(tempos); }
        public static double calc_Mid(KeyValuePair<int, double> p1, KeyValuePair<int, double> p2, int k)
        {
            KeyValuePair<int, double> a1 = p1.Key < p2.Key ? p1 : p2;
            KeyValuePair<int, double> a2 = p1.Key < p2.Key ? p2 : p1;
            double slope = (a2.Value - a1.Value) / ((double)a2.Key - a1.Key);
            return a1.Value + slope * ((double)k - a1.Key);
        }

        public List<List<Point>> GenerateSmooth(List<NoteInfo> notes)
        {
            List<List<Point>> ret = new List<List<Point>>();
            const double transitionTime = 120;
            const double transitionSpace = 0;
            double duration = notes.Last().EndPos();
            Parallel.For(1, notes.Count, (ni) => {
                List<Point> lines = new List<Point>();
                NoteInfo pn, nn;
                lock (notes) { pn = notes[ni-1]; nn = notes[ni];}
                if (pn.EndPos() - nn.StartPos() <= transitionSpace)
                {
                    double midTick = Math.Min(pn.EndPos(), nn.StartPos());
                    double pitchGap = nn.Pitch - pn.Pitch;
                    double midTime = tUtils.GetTime(midTick) * 1000.0d;
                    double transitionStartTime = midTime - transitionTime / 2;
                    double transitionEndTime = midTime + transitionTime / 2;
                    int transitionStartTick = (int)tUtils.GetTick(transitionStartTime / 1000.0d).Limit(0, duration);
                    int transitionEndTick = (int)tUtils.GetTick(transitionEndTime / 1000.0d).Limit(0, duration);
                    int transitionTicks = transitionEndTick - transitionStartTick;
                    for (int t = transitionStartTick; t < transitionEndTick; t++)
                    {
                        double value = nn.Pitch;
                        if (t < midTick)
                        {
                            value = pn.Pitch + MathUtility.CubicInterpolation((double)(t - transitionStartTick) / transitionTicks) * pitchGap;
                        }
                        else
                        {
                            value = nn.Pitch - (1 - MathUtility.CubicInterpolation((double)(t - transitionStartTick) / transitionTicks)) * pitchGap;
                        }
                        lines.Add(new Point(t, value));
                    }
                    lines.Add(new Point(transitionEndTick, nn.Pitch));//最后一个结尾Point
                }
                if (lines.Count > 0)
                {
                    lines = lines.OrderBy(p => p.X).ToList();
                    lock (ret) { ret.Add(lines); }
                }
            });
            ret = ret.OrderBy(p => p[0].X).ToList();
            return ret;
        }
        public List<List<Point>> KeepSmooth(List<List<Point>> pitch)
        {
            List<List<Point>> ret = new List<List<Point>>();
            for (int i = 0; i < pitch.Count; i++)
            {
                List<Point> points = new List<Point>() { pitch[i][0] };
                Parallel.For(1, pitch[i].Count, (pi) => {
                    Point aPoint = pitch[i][pi - 1];
                    Point bPoint = pitch[i][pi];
                    if ((int)bPoint.X - (int)aPoint.X > 1)
                    {
                        for (int j = (int)aPoint.X + 1; j < (int)bPoint.X; j++)
                        {
                            lock (points) points.Add(new Point((int)j, aPoint.Y));
                        }
                    }
                    lock (points) points.Add(new Point((int)bPoint.X, bPoint.Y));
                });
                ret.Add(points);
            }
            return ret;
        }
        public List<Point> GenerateAutomationPit(Map<string, AutomationInfo>? automn = null)
        {
            if (automn == null) return new List<Point>();
            if (!automn.ContainsKey("PitchBend")) return new List<Point>(); ;
            double getPBS(double tick)
            {
                return (!automn.ContainsKey("PitchBendSensitive")) ? 2 : ((int)automn["PitchBendSensitive"].Points.Where(p => p.X <= tick).OrderBy(p => p.X).LastOrDefault(new Point() { X = 0, Y = 2 }).Y);
            }
            List<Point> a_PIT = automn["PitchBend"].Points.OrderBy(p=>p.X).ToList();
            List<Point> ret = new List<Point>();
            Parallel.For(0, a_PIT.Count, pi => {
                double pbs = getPBS(a_PIT[pi].X);
                if(pi==0)
                {
                    double tick = a_PIT[pi].X;
                    double val = a_PIT[pi].Y;
                    if (val != 0)
                    {
                        lock (ret) ret.Add(new Point() {X=tick,Y=pbs*val });
                    }
                }else
                {
                    double tick1 = a_PIT[pi-1].X;
                    double val1 = a_PIT[pi-1].Y;
                    double tick2 = a_PIT[pi].X;
                    double val2 = a_PIT[pi].Y;
                    double pbs1 = getPBS(a_PIT[pi-1].X);
                    if(val1!=0)
                    {
                        Parallel.For((int)tick1 + 1, (int)tick2, t =>
                        {
                            lock (ret) ret.Add(new Point() { X = t, Y = pbs1 * val1 });
                        });
                    }
                    lock (ret) ret.Add(new Point() { X = tick2, Y = pbs * val2 });
                }
            });
            return ret.OrderBy(p => p.X).ToList(); ;
        }
        public List<List<Point>> TransformRel(List<List<Point>> lines ,List<NoteInfo> notes)
        {
            List<List<Point>> ret = new List<List<Point>>();
            Parallel.ForEach(lines, (l) => {
                List<Point> lp = new List<Point>();
                Parallel.ForEach(l, (p) => {
                    lock (notes)
                    {
                        int notePitch = notes.Where(n => n.EndPos() >= p.X).First().Pitch;
                        p.Y -= notePitch;
                    }
                    lock(lp) lp.Add(p);
                });
                lock(ret) ret.Add(lp.OrderBy(p=>p.X).ToList());
                if (lp.Last().Y != 0) { lp.Add(new Point(lp.Last().X + 1, 0)); }
            });
            return ret.OrderBy(lp => lp[0].X).ToList();
        }
        public SortedDictionary<int, double> CombineLines(List<List<Point>>[] lineGroup, List<Point>[]? overlayGroup=null)
        {
            SortedDictionary<int, double> rel = new SortedDictionary<int, double>();
            for (int w = 0; w < lineGroup.Length; w++)
            {
                var lines = lineGroup[w];
                for (int i = 0; i < lines.Count; i++)
                {
                    Parallel.ForEach(lines[i], (p) =>
                    {
                        lock (rel)
                        {
                            if (rel.ContainsKey((int)p.X)) rel[(int)p.X] = p.Y; else rel.Add((int)p.X, p.Y);
                        }
                    });
                }
                if (overlayGroup != null && w < overlayGroup.Length)
                {
                    List<Point> overlay = overlayGroup[w];
                    if (overlay != null)
                    {
                        Parallel.ForEach(overlay, p =>
                        {
                            lock (rel)
                            {
                                if (rel.ContainsKey((int)p.X)) { rel[(int)p.X] += p.Y; }
                                else { rel.Add((int)p.X, p.Y); }
                            }
                        });
                    }
                }
            }
            return rel;
        }
    }
}
