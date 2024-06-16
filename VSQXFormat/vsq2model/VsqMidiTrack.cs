using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TuneLab.Extensions.Formats.VSQX.vsq4;
using VsqxFormat.vsq2.ini;

namespace VsqxFormat.vsq2
{
    internal class VsqMidiTrack
    {
        public string TrackName { get; set; }
        public VsqMidiCommon? Common { get; set; }
        public VsqMidiMaster? Master { get; set; }
        public VsqMidiMixer? Mixer { get; set; }
        public Dictionary<string,VsqMidiBPList> BpCtlLists { get; set; }
        public List<VsqMidiNote>? NoteList { get; set; }
        public VsqMidiTrack() { 
            this.TrackName="";
            this.BpCtlLists = new Dictionary<string, VsqMidiBPList>();
        }
        public VsqMidiTrack(string trackName,string trackData)
        {
            this.TrackName= trackName;
            this.BpCtlLists = new Dictionary<string, VsqMidiBPList>();
            this.LoadData(trackData);
        }

        public bool LoadData(string trackData)
        {
            IniDataParser iparser = new IniDataParser();
            iparser.LoadFromString(trackData);
            List<Task> pTask = new List<Task>();
            foreach(string section in iparser.GetSections())
            {
                switch(section)
                {
                    case "Common":
                        //通用属性"
                        Common = new VsqMidiCommon(iparser);
                        break;
                    case "Master":
                        //主轨属性
                        Master = new VsqMidiMaster(iparser);
                        break;
                    case "Mixer":
                        //混音器属性
                        Mixer = new VsqMidiMixer(iparser);
                        break;
                    case "EventList":
                        pTask.Add(Task.Factory.StartNew(() => {
                            List<VsqMidiNote> nlist=VsqMidiNote.LoadFromParser(iparser, "EventList");
                            lock (this) { NoteList = nlist; };
                        }));
                        break;
                    case "PitchBendBPList":
                        pTask.Add(Task.Factory.StartNew(() => {
                            VsqMidiBPList p = new VsqMidiBPList('P', "PitchBendBPList");
                            p.LoadData(iparser, "PitchBendBPList");
                            lock (BpCtlLists) { BpCtlLists.Add("PIT", p); };
                        }));
                        //Pitch曲线
                        break;
                    case "PitchBendSensBPList":
                        pTask.Add(Task.Factory.StartNew(() => {
                            VsqMidiBPList p = new VsqMidiBPList('S', "PitchBendSensBPList");
                            p.LoadData(iparser, "PitchBendSensBPList");
                            lock (BpCtlLists) { BpCtlLists.Add("PBS", p); };
                        }));
                        //PBS曲线
                        break;
                    case "DynamicsBPList":
                        pTask.Add(Task.Factory.StartNew(() => {
                            VsqMidiBPList p = new VsqMidiBPList('D', "DynamicsBPList");
                            p.LoadData(iparser, "DynamicsBPList");
                            lock (BpCtlLists) { BpCtlLists.Add("DYN", p); };
                        }));
                        break;
                    case "EpRResidualBPList":
                        pTask.Add(Task.Factory.StartNew(() => {
                            VsqMidiBPList p = new VsqMidiBPList('E', "EpRResidualBPList");
                            p.LoadData(iparser, "EpRResidualBPList");
                            lock (BpCtlLists) { BpCtlLists.Add("EPR", p); };
                        }));
                        break;
                    case "EpRESlopeDepthBPList":
                        pTask.Add(Task.Factory.StartNew(() => {
                            VsqMidiBPList p = new VsqMidiBPList('L', "EpRESlopeDepthBPList");
                            p.LoadData(iparser, "EpRESlopeDepthBPList");
                            lock (BpCtlLists) { BpCtlLists.Add("ESL", p); };
                        }));
                        break;
                    case "GenderFactorBPList"://64
                        pTask.Add(Task.Factory.StartNew(() => {
                            VsqMidiBPList p = new VsqMidiBPList('G', "GenderFactorBPList");
                            p.LoadData(iparser, "GenderFactorBPList");
                            lock (BpCtlLists) { BpCtlLists.Add("GEN", p); };
                        }));
                        //GEN虚线
                        break;
                    case "PortamentoTimingBPList":
                        pTask.Add(Task.Factory.StartNew(() => {
                            VsqMidiBPList p = new VsqMidiBPList('T', "PortamentoTimingBPList");
                            p.LoadData(iparser, "PortamentoTimingBPList");
                            lock (BpCtlLists) { BpCtlLists.Add("POR", p); };
                        }));
                        //POR曲线
                        break;
                }
            }
            Task.WaitAll(pTask.ToArray());
            return true;
        }

        public TuneLab.Extensions.Formats.VSQX.vsq4.vsTrack ToVsq4Track(int TrackId)
        {
            TuneLab.Extensions.Formats.VSQX.vsq4.vsTrack ret = new TuneLab.Extensions.Formats.VSQX.vsq4.vsTrack();
            ret.tNo = (byte)TrackId;
            ret.name = this.TrackName;
            ret.comment = this.TrackName;
            vsPart pret = new vsPart();
            {
                pret.t = 0;
                pret.playTime = this.NoteList.Last().Pos + this.NoteList.Last().Dur + 480 * 4;
                pret.name = this.TrackName;
                pret.comment = this.TrackName;
                pret.sPlug = new sPlug()
                {
                    id = "ACA9C502-A04B-42b5-B2EB-5CEA36D16FCE",
                    name = "VOCALOID2 Compatible Style",
                    version = "3.0.0.1"
                };
                pret.pStyle = new typeParamAttr[7] {
                            new typeParamAttr(){id="accent",Value=50},
                            new typeParamAttr(){id="bendDep",Value=8},
                            new typeParamAttr(){id="bendLen",Value=0},
                            new typeParamAttr(){id="decay",Value=50},
                            new typeParamAttr(){id="fallPort",Value=0},
                            new typeParamAttr(){id="opening",Value=127},
                            new typeParamAttr(){id="risePort",Value=0},
                        };
                pret.singer = new singer[1] { new singer() { t = 0, bs = 0, pc = 0 } };


                pret.note = new TuneLab.Extensions.Formats.VSQX.vsq4.note[this.NoteList.Count];
                for(int i = 0; i < this.NoteList.Count; i++) { pret.note[i]=this.NoteList[i].BaseNote; }

                string[] FitAbleBP = ["POR","GEN","DYN","PIT","PBS"];
                List<cc> ccs = new List<cc>();
                foreach(string BPK in FitAbleBP)
                {
                    if (!BpCtlLists.ContainsKey(BPK)) continue;
                    Parallel.ForEach(BpCtlLists[BPK].CtlCollection, (bp) => {
                        lock(ccs)ccs.Add(bp);
                    });
                }
                pret.cc = ccs.OrderBy(p => p.t).ToArray();
                pret.plane = 0;
                pret.planeSpecified = false;
            }
            ret.vsPart = [pret];
            return ret;
        }
    }
}