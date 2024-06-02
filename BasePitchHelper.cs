using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TuneLab.Extensions.Formats.VSQX
{
    public class BezierCurve
    {
        private PointF[] controlPoints;
        private class tRange
        {
            readonly double tMin;
            readonly double tMax;
            readonly double tDur;
            public tRange(PointF p1, PointF p2)
            {
                tMin = Math.Min(p1.X, p2.X);
                tMax = Math.Max(p1.X, p2.X);
                tDur = tMax - tMin;
            }
            public double GetT(double V)
            {
                if (V < tMin) return 0;
                if (V > tMax) return 1;
                return (V - tMin) / tDur;
            }
        }
        private tRange ranger;
        public BezierCurve(PointF[] controlPoints)
        {
            if (controlPoints.Length != 4)
            {
                throw new ArgumentException("Invalid number of control points. Expected 4, but got " + controlPoints.Length);
            }

            this.controlPoints = controlPoints;
            this.ranger = new tRange(controlPoints[0], controlPoints[3]);
        }

        public PointF GetPointAt(double v)
        {
            double t = this.ranger.GetT(v);

            double t2 = t * t;
            double t3 = t * t2;

            PointF p0 = controlPoints[0];
            PointF p1 = controlPoints[1];
            PointF p2 = controlPoints[2];
            PointF p3 = controlPoints[3];

            PointF point = new PointF
            {
                X = (float)((1 - 3 * t + 3 * t2 - t3) * p0.X + (3 * t - 6 * t2 + 3 * t3) * p1.X + (3 * t2 - 3 * t3) * p2.X + t3 * p3.X),
                Y = (float)((1 - 3 * t + 3 * t2 - t3) * p0.Y + (3 * t - 6 * t2 + 3 * t3) * p1.Y + (3 * t2 - 3 * t3) * p2.Y + t3 * p3.Y)
            };

            return point;
        }
    }
    public class BasePitchHelper
    {

        private double _noteTempo;
        public BasePitchHelper(double NoteTempo = 120.0d)
        {
            this._noteTempo = NoteTempo;
        }
        private static int TickFromT120(int t120, double Tempo)
        {
            if (Tempo == 120.0) return t120;
            return (int)Math.Round((t120 * Tempo) / 120.0d);
        }
        private static int TickToT120(int tReal, double Tempo)
        {
            if (Tempo == 120.0) return tReal;
            return (int)Math.Round((tReal * 120.0d) / Tempo);
        }
        private int AttrackDuration(int NoteTickDuration)
        {
            int T120 = TickFromT120(NoteTickDuration,_noteTempo);
            return TickToT120(Math.Min(T120 / 4, 48),_noteTempo);//实测，120BPM下通常在40-50之间，但不高于1/4音符
        }
        private int ReleaseDuration(int NoteTickDuration)
        {
            int T120 = TickFromT120(NoteTickDuration, _noteTempo);
            return TickToT120(Math.Min(T120*2 / 3, 100), _noteTempo);//实测，120BPM下通常在90-110之间，但不高于2/3音符
        }
        private PointF[] midctlpoint(PointF[] cp) {

            var p1 = cp[0];
            var cp1 = cp[1];
            var cp2 = cp[2];
            var p2 = cp[3];
            PointF c1 = new PointF(cp1.X/2 + cp2.X/2, 3 * p1.Y / 4 + p2.Y / 4);
            PointF c2 = new PointF(cp1.X/2 + cp2.X/2, 3 * p2.Y / 4 + p1.Y / 4);
            return new PointF[] {p1,c1,c2,p2};
        }
        public BezierCurve GetSmoothCurve(int noteA_Pos, int noteA_Dur, int noteA_Pitch, int noteB_Pos, int noteB_Dur, int noteB_Pitch)
        {
            if (noteA_Pos > noteB_Pos)
            {  
                int t = noteA_Pos; noteA_Pos = noteB_Pos; noteB_Pos = t;
                t = noteA_Dur; noteA_Dur = noteB_Dur; noteB_Dur = t;
                t = noteA_Pitch; noteA_Pitch = noteB_Pitch; noteB_Pitch = t;
            }
            PointF[] controlPoints = [
                new PointF(noteA_Pos + noteA_Dur - ReleaseDuration(noteA_Dur), noteA_Pitch), //StartPoint
                new PointF(noteA_Pos + noteA_Dur, noteA_Pitch),  //ControlPoint
                new PointF(noteB_Pos, noteB_Pitch), //ControlPoint
                new PointF(noteB_Pos + AttrackDuration(noteB_Dur), noteB_Pitch) //EndPoint
            ];
            return new BezierCurve(midctlpoint(controlPoints));
        }
        public SortedDictionary<int,double> GetSmoothPitch(int noteA_Pos, int noteA_Dur, int noteA_Pitch, int noteB_Pos, int noteB_Dur, int noteB_Pitch,SortedDictionary<int,double>? target=null)
        {
            int tickStep = 4;
            SortedDictionary<int, double> ret = target==null?new SortedDictionary<int, double>():target;
            BezierCurve curve=GetSmoothCurve(noteA_Pos, noteA_Dur, noteA_Pitch, noteB_Pos, noteB_Dur, noteB_Pitch);
            for(int i= noteA_Pos + noteA_Dur - ReleaseDuration(noteA_Dur); i< noteB_Pos + AttrackDuration(noteB_Dur) + tickStep;i=i+tickStep)
            {
                if (ret.ContainsKey((int)curve.GetPointAt(i).X)) ret[(int)curve.GetPointAt(i).X] = curve.GetPointAt(i).Y;else ret.Add((int)curve.GetPointAt(i).X, curve.GetPointAt(i).Y);
            }
            return ret;
        }

        public static double calc_Mid(KeyValuePair<int,double> p1, KeyValuePair<int, double> p2,int k)
        {
            KeyValuePair<int, double> a1 = p1.Key < p2.Key ? p1 : p2;
            KeyValuePair<int, double> a2 = p1.Key < p2.Key ? p2 : p1;
            double slope = (a2.Value - a1.Value) / ((double)a2.Key - a1.Key);
            return a1.Value + slope * ((double)k - a1.Key);
        }
    }
}
