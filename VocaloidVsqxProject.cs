using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TuneLab.Base.Properties;
using TuneLab.Base.Structures;
using TuneLab.Extensions.Formats.DataInfo;
using TuneLab.Extensions.Formats.VSQX.vsq4;
using Point = TuneLab.Base.Structures.Point;

namespace TuneLab.Extensions.Formats.VSQX
{
    [ImportFormat("vsqx")]
    [ExportFormat("vsqx")]
    internal class VsqxWithExtension : IImportFormat,IExportFormat
    {
        public ProjectInfo Deserialize(Stream stream)
        {
            VsqxDecoder decoder = new VsqxDecoder();
            ProjectInfo ret = decoder.Deserialize(stream);
            return ret;
        }

        public Stream Serialize(ProjectInfo info)
        {
            MemoryStream ms = new MemoryStream();
            VsqxEncoder encoder = new VsqxEncoder();
            encoder.Serialize(info, ms);
            return ms;
        }
    }
    [ImportFormat("vsq")]
    internal class VsqWithExtension : IImportFormat
    {
        public ProjectInfo Deserialize(Stream stream)
        {
            VsqxDecoder decoder = new VsqxDecoder();
            ProjectInfo ret = decoder.Deserialize(stream);
            return ret;
        }
    }
    
    public class VsqxDecoder
    {
        private enum DecodeModeType
        {
            VOCALOID_BasePitch=0,
            Besizer_BasePitch=1
        }

        DecodeModeType mMode = DecodeModeType.Besizer_BasePitch;

        int mVsqVersion = 0;
        public VsqxDecoder() {}
        private int loadVsqVersion(Stream stream)
        {
            StreamReader streamReader = new StreamReader(stream);
            //判断非V2版本
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(streamReader.ReadToEnd());
            stream.Position = 0;
            if (xmlDoc.DocumentElement == null) return 0;
            return (xmlDoc.DocumentElement.Name == "vsq3") ? 3 : (xmlDoc.DocumentElement.Name == "vsq4") ? 4 : 0;

        }
        private vsq4.vsq4 loadVsq4Stream(Stream stream)
        {
            vsq4.vsq4 vsqxDoc = null; ;
            if (mVsqVersion == 3)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(vsq3.vsq3));
                vsqxDoc = Vsq4Adapter3.trans((vsq3.vsq3)serializer.Deserialize(stream));
            }
            else if (mVsqVersion == 4)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(vsq4.vsq4));
                vsqxDoc = (vsq4.vsq4)serializer.Deserialize(stream);
            }
            return vsqxDoc;
        }
        public static double RangeMapper(double x, double minIn, double maxIn, double minOut, double maxOut)
        {
            return (x - minIn) * (maxOut - minOut) / (maxIn - minIn) + minOut;
        }
        private delegate double SyncAutomationPoint(double input);
        public ProjectInfo Deserialize(Stream stream)
        {
            mVsqVersion = loadVsqVersion(stream);
            if (mVsqVersion == 0) return null;
            vsq4.vsq4 vsqxDoc = loadVsq4Stream(stream);

            ProjectInfo proj = new ProjectInfo();
            //处理Tempo
            foreach (vsq4.tempo t in vsqxDoc.masterTrack.tempo) proj.Tempos.Add(new TempoInfo() { Bpm = t.v / 100d, Pos = t.t / 100d });
            //处理TimeSig
            foreach (vsq4.timeSig t in vsqxDoc.masterTrack.timeSig) proj.TimeSignatures.Add(new TimeSignatureInfo() { BarIndex = t.m, Numerator = t.nu, Denominator = t.de });
            //获取音量配置
            Dictionary<int, vsq4.vsUnit> vsMixer = new Dictionary<int, vsUnit>();
            foreach (vsq4.vsUnit v in vsqxDoc.mixer.vsUnit) { vsMixer.Add(v.tNo, v); }
            //获取歌手配置
            Dictionary<string, vsq4.vVoice> vsVoice = new Dictionary<string, vVoice>();
            foreach (vsq4.vVoice v in vsqxDoc.vVoiceTable) { vsVoice.Add(String.Format("{0}_{1}", v.bs, v.pc), v); }
            //处理Tracks
            foreach (vsq4.vsTrack t in vsqxDoc.vsTrack)
            {
                var trackInfo = new TrackInfo()
                {
                    Name = t.name,
                    Gain = RangeMapper((vsMixer.ContainsKey(t.tNo) ? vsMixer[t.tNo].iGin : 0) / 10d, -89.8, 6.0, -24.0, 24.0),
                    Pan = RangeMapper((vsMixer.ContainsKey(t.tNo) ? vsMixer[t.tNo].pan : 0), 0, 128.0, -1.0, 1.0),
                    Mute = vsMixer.ContainsKey(t.tNo) ? vsMixer[t.tNo].m != 0 : false,
                    Solo = vsMixer.ContainsKey(t.tNo) ? vsMixer[t.tNo].s != 0 : false
                };
                int pIdx = 0;
                if (t.vsPart == null) continue;
                foreach (vsPart p in t.vsPart)
                {
                    pIdx++;
                    var midiPartInfo = new MidiPartInfo()
                    {
                        Properties = PropertyObject.Empty,
                        Voice = new VoiceInfo(),
                        Pos = p.t,
                        Dur = p.playTime,
                        Name = t.name + "_Part_" + pIdx.ToString(),
                    };
                    //区块引擎基础信息
                    midiPartInfo.Voice.Type = "VOCALOID5";
                    midiPartInfo.Voice.ID = "";
                    //歌手基础信息
                    if(p.singer!=null && p.singer.Length>0)
                    {
                        string k = String.Format("{0}_{1}", p.singer[0].bs, p.singer[0].pc);
                        if (vsVoice.ContainsKey(k)) midiPartInfo.Voice.ID = vsVoice[k].id;
                    }
                    //参数分组
                    Dictionary<string, SortedDictionary<int, int>> VsqxControllers = new Dictionary<string, SortedDictionary<int, int>>()
                    {
                        {"D",new SortedDictionary<int, int>(){ { 0, 64 } } },//DYN
                        {"B",new SortedDictionary<int, int>(){ { 0, 0 } } },//BRE
                        {"R",new SortedDictionary<int, int>(){ { 0, 64 } } },//BRI
                        {"C",new SortedDictionary<int, int>(){ { 0, 0 } } },//CLE
                        {"G",new SortedDictionary<int, int>(){ { 0, 64 } } },//GEN
                        {"T",new SortedDictionary<int, int>(){ { 0, 64 } } },//POR
                        {"S",new SortedDictionary<int, int>(){ { 0, 2 } } },//PBS
                        {"P",new SortedDictionary<int, int>(){ { 0, 0 } } },//PIT
                        {"X",new SortedDictionary<int, int>(){ { 0, 0 } } },//XSY
                        {"W",new SortedDictionary<int, int>(){ { 0, 0 } } },//GWL
                    };
                    //复制参数
                    if(p.cc!=null) Parallel.ForEach(p.cc, c =>
                    {
                        lock (VsqxControllers)
                        {
                            if (VsqxControllers[c.v.id].ContainsKey(c.t)) VsqxControllers[c.v.id][c.t] = c.v.Value;
                            else VsqxControllers[c.v.id].Add(c.t, c.v.Value);
                        }
                    });
                    //定义参数同步函数
                    void SyncAutomation(string srcKey, string targetKey, double defaultValue, SyncAutomationPoint callback)
                    {
                        var retCtl = new AutomationInfo() { DefaultValue = callback((double)defaultValue) };
                        if (VsqxControllers.ContainsKey(srcKey))
                        {
                            SortedDictionary<int, int> ctl = VsqxControllers[srcKey];
                            List<int> keyList = ctl.Keys.ToList();
                            for (int i = 0; i < keyList.Count; i++)
                            {
                                int Key = keyList[i];
                                int Value = ctl[Key];
                                if (i > 0 && keyList[i - 1] != Key - 1)
                                {
                                    retCtl.Points.Add(new Point((double)Key - 1, callback((double)ctl[keyList[i - 1]])));
                                }
                                retCtl.Points.Add(new Point((double)Key, callback((double)Value)));

                            }
                        }
                        lock (midiPartInfo)
                        {
                            midiPartInfo.Automations.Add(targetKey, retCtl);
                        }

                    }
                    Task copyNotes = Task.Factory.StartNew(() =>
                    {
                        SortedDictionary<int, double> basePitch = new SortedDictionary<int, double>();
                        //复制音符
                        for (int ni = 0; ni < p.note.Length; ni++)
                        {
                            note n = p.note[ni];
                            NoteInfo noteInfo = new NoteInfo()
                            {
                                Pos = n.t,
                                Dur = n.dur,
                                Pitch = n.n,
                                Lyric = (n.p.lockSpecified) ? ("." + n.p.Value) : n.y,
                                Properties = new PropertyObject(new Map<string, PropertyValue>() { { "Phoneme", n.p.Value.ToString() } })
                            };
                            lock (midiPartInfo)
                            {
                                midiPartInfo.Notes.Add(noteInfo);
                            }

                            bool NoPitch = (!VsqxControllers.ContainsKey("P") || VsqxControllers["P"].Where(key => key.Value != 0).Count() == 0) && p.plane == 1;
                            //生成音高(仅在贝塞尔模式运行）
                            if (NoPitch)
                            {
                                ;//当音高捕捉模式没有有效音高控制点时，不准备基础音高了。
                            }
                            else if (mMode == DecodeModeType.Besizer_BasePitch)//只有不是黄条模式，且贝塞尔基础曲线时消耗时间才有意义，因此判断是否执行
                            {
                                double nextDert = 0;
                                //计算基础音高，自动补完滑音
                                {
                                    if (ni == 0) { basePitch.Add(0, n.n); if (n.t > 0) basePitch.Add(n.t, n.n); }//第一个音符，加头部0和开始0
                                    if (ni + 1 < p.note.Length)
                                    {
                                        note n1 = p.note[ni + 1];
                                        nextDert = n1.t - n.t - n.dur;
                                        if (nextDert <= 480)//不分片,前后两端都有音高
                                        {
                                            if (p.plane != 1)
                                            {//音高拟合模式，使用贝塞尔曲线
                                                TempoInfo bpm = proj.Tempos.Where(t => t.Pos <= n.t).OrderBy(t => t.Pos).LastOrDefault();
                                                BasePitchHelper besizerHelper = new BasePitchHelper(bpm.Bpm);
                                                basePitch = besizerHelper.GetSmoothPitch(n.t, n.dur, n.n, n1.t, n1.dur, n1.n, basePitch); //贝塞尔曲线，从当前音符末尾到下一音符开头
                                            }
                                            else
                                            {//音高捕捉(黄条）模式，对齐音符折线
                                                if (n.t + n.dur == n1.t)
                                                {
                                                    basePitch.Add(n.t + n.dur - 1, n.n);
                                                }
                                                else
                                                {
                                                    basePitch.Add(n.t + n.dur, n.n);//加尾部0
                                                    if (n.t + n.dur + 1 < n1.t) basePitch.Add(n.t + n.dur + 1, n1.n);//加区域0
                                                }
                                                basePitch.Add(n1.t, n1.n);//加头部0
                                            }
                                        }
                                        else//分片，两端不连续，中间区域归属后一个音符
                                        {
                                            basePitch.Add(n.t + n.dur, n.n);//加尾部0
                                            basePitch.Add(n.t + n.dur + 4, n1.n);//加区域0
                                            basePitch.Add(n1.t, n1.n);//加头部0
                                        }
                                    }
                                    else
                                    {
                                        //最后一个音符
                                        basePitch.Add(n.t + n.dur, n.n);//加尾部0
                                    }
                                }
                                //基础音高算完，绘制拟合音高
                                {
                                    //获取音符边界
                                    int sp = ((ni == 0) || (midiPartInfo.Pitch[midiPartInfo.Pitch.Count - 1].Count == 0)) ? n.t : p.note[ni - 1].t + p.note[ni - 1].dur + 1;
                                    int ep = n.t + n.dur;
                                    //初始化PIT属性
                                    SortedDictionary<int, int> ctl = VsqxControllers.ContainsKey("P") ? VsqxControllers["P"] : new SortedDictionary<int, int>() { { 0, 0 } };
                                    SortedDictionary<int, int> pbs = VsqxControllers.ContainsKey("S") ? VsqxControllers["S"] : new SortedDictionary<int, int>() { { 0, 2 } };
                                    //获取所有可能的控制点
                                    List<int> PitchKeys = basePitch.Keys.Where(key => key >= sp && key <= ep).ToList();
                                    List<int> PitKeys = ctl.Keys.Where(key => key >= sp && key <= ep).ToList();
                                    List<int> AllKeys = PitchKeys.Concat(PitKeys).ToList().Distinct().ToList();
                                    AllKeys.Sort();
                                    //计算控制点绝对音高
                                    List<Point> lpItems = new List<Point>();
                                    Parallel.ForEach(AllKeys, (tickKey) =>
                                    {
                                        double bPitch = basePitch.ContainsKey(tickKey) ? basePitch[tickKey] : BasePitchHelper.calc_Mid(basePitch.Where(key => key.Key < tickKey).Last(), basePitch.Where(key => key.Key > tickKey).First(), tickKey);
                                        double bPit = ctl.Where(key => key.Key <= tickKey).Last().Value;
                                        double bPbs = pbs.Where(key => key.Key <= tickKey).Last().Value;
                                        Point vP = new Point(tickKey, bPitch + bPbs * bPit / (bPit > 0 ? 8191.0d : 8192.0d));
                                        lock (lpItems)
                                        {
                                            lpItems.Add(vP);
                                        }
                                    });
                                    lock (midiPartInfo)
                                    {
                                        if (midiPartInfo.Pitch.Count == 0)
                                            midiPartInfo.Pitch.Add(lpItems.OrderBy(p => p.X).ToList());
                                        else
                                            midiPartInfo.Pitch[midiPartInfo.Pitch.Count - 1].AddRange(lpItems.OrderBy(p => p.X).ToList());
                                        if (nextDert > 480) midiPartInfo.Pitch.Add(new List<Point>());
                                    }
                                }
                            }
                            //添加非贝塞尔音高捕捉模式
                            else if (p.plane == 1)
                            {
                                int sp = n.t;
                                int ep = n.t + n.dur;
                                SortedDictionary<int, int> ctl = VsqxControllers.ContainsKey("P") ? VsqxControllers["P"] : new SortedDictionary<int, int>() { { 0, 0 } };
                                SortedDictionary<int, int> pbs = VsqxControllers.ContainsKey("S") ? VsqxControllers["S"] : new SortedDictionary<int, int>() { { 0, 2 } };
                                int KeyStart = ctl.Keys.LastOrDefault(key => key <= sp);
                                var KeyArea = ctl.Keys.Where(key => key > sp && key <= ep);
                                int PBS = pbs[pbs.Keys.LastOrDefault(key => key <= KeyStart)];
                                List<Point> lpItems = new List<Point>() { new Point(sp, n.n + PBS * ctl[KeyStart] / (ctl[KeyStart] > 0 ? 8191.0d : 8192.0d)) };
                                foreach (int aKey in KeyArea)
                                {
                                    PBS = pbs[pbs.Keys.LastOrDefault(key => key <= aKey)];
                                    lpItems.Add(new Point(aKey, n.n + PBS * ctl[aKey] / (ctl[aKey] > 0 ? 8191.0d : 8192.0d)));
                                }
                                lock (midiPartInfo)
                                {
                                    midiPartInfo.Pitch.Add(lpItems.OrderBy(p => p.X).ToList());
                                }
                            }
                        }
                    });
                    //添加PIT参数,如果是Besizer，那么自己加初始滑音
                    Task copyPIT= (p.plane==1 || mMode==DecodeModeType.Besizer_BasePitch) ? Task.Factory.StartNew(() => {; }):Task.Factory.StartNew(() => { SyncAutomation("P", "PitchBend", 0, new SyncAutomationPoint((inp) => { return inp > 0 ? inp / 8191.0d : inp / 8192.0d; })); });
                    //添加PBS参数
                    Task copyPBS = Task.Factory.StartNew(() => { SyncAutomation("S", "PitchBendSensitive", 2, new SyncAutomationPoint((inp) => { return inp; })); });
                    //添加DYN参数
                    Task copyDYN = Task.Factory.StartNew(() => { SyncAutomation("D", "Dynamics", 64, new SyncAutomationPoint((inp) => { return RangeMapper((int)inp, 0, 128, -1.0, 1.0); })); });
                    //添加BRI参数
                    Task copyBRI = Task.Factory.StartNew(() => { SyncAutomation("R", "Brightness", 64, new SyncAutomationPoint((inp) => { return RangeMapper((int)inp, 0, 128, -1.0, 1.0); })); });
                    //添加GEN参数
                    Task copyGEN = Task.Factory.StartNew(() => { SyncAutomation("G", "Gender", 64, new SyncAutomationPoint((inp) => { return RangeMapper((int)inp, 0, 128, -1.0, 1.0); })); });
                    //添加GWL参数
                    Task copyGWL = Task.Factory.StartNew(() => { SyncAutomation("W", "Growl", 0, new SyncAutomationPoint((inp) => { return RangeMapper((int)inp, 0, 127, 0, 1.0); })); });
                    //添加CLE参数
                    Task copyCLE = Task.Factory.StartNew(() => { SyncAutomation("C", "Clearness", 0, new SyncAutomationPoint((inp) => { return RangeMapper((int)inp, 0, 127, 0, 1.0); })); });
                    //添加BRE
                    Task copyBRE = Task.Factory.StartNew(() => { SyncAutomation("B", "Breathiness", 0, new SyncAutomationPoint((inp) => { return RangeMapper((int)inp, 0, 127, 0, 1.0); })); });

                    Task.WaitAll([copyNotes, copyPIT, copyPBS, copyDYN, copyBRI, copyGEN, copyGWL, copyCLE, copyBRE]);

                    trackInfo.Parts.Add(midiPartInfo);
                }
                proj.Tracks.Add(trackInfo);
            }
            return proj;
        }
    }

    public class VsqxEncoder
    {
        public VsqxEncoder() { }

        private delegate double SyncAutomationPoint(double input);
        public static double RangeMapper(double x, double minIn, double maxIn, double minOut, double maxOut)
        {
            return (x - minIn) * (maxOut - minOut) / (maxIn - minIn) + minOut;
        }

        public void Serialize(ProjectInfo info, Stream stream)
        {
            if (info == null) return;
            vsq4.vsq4 vsq = new vsq4.vsq4();
            vsq.vender = "Yamaha corporation";
            vsq.version = "4.0.0.3";
            //初始化歌手声音，由于Tunelab为通用型，所以预置MIKU
            {
                vsq.vVoiceTable = new vVoice[1];
                vsq.vVoiceTable[0] = new vVoice();
                vsq.vVoiceTable[0].bs = 4;//CHN
                vsq.vVoiceTable[0].pc = 0;
                vsq.vVoiceTable[0].id = "BNGE7CP7EMTRSNC3";//Miku_V4C
                vsq.vVoiceTable[0].name = "Miku_Chinese";
                vsq.vVoiceTable[0].vPrm = new vPrm();
                vsq.vVoiceTable[0].vPrm.cle = 0;
                vsq.vVoiceTable[0].vPrm.bre = 0;
                vsq.vVoiceTable[0].vPrm.bri = 0;
                vsq.vVoiceTable[0].vPrm.gen = 0;
                vsq.vVoiceTable[0].vPrm.ope = 0;
                vsq.vVoiceTable[0].id2 = vsq.vVoiceTable[0].id;
                vsq.vVoiceTable[0].vPrm2 = new vPrm2() { bre = 0, cle = 0, bri = 0, gen = 0, ope = 0, vol = 0 };
            }
            //音量混合器初始化
            {
                vsq.mixer = new mixer();
                vsq.mixer.masterUnit = new masterUnit() { oDev = 0, rLvl = 0, vol = 0 };
                vsq.mixer.vsUnit = new vsq4.vsUnit[info.Tracks.Count];
                for (int i = 0; i < info.Tracks.Count; i++)
                {
                    vsq.mixer.vsUnit[i] = new vsUnit()
                    {
                        tNo = (byte)i,
                        iGin = (int)(RangeMapper(info.Tracks[i].Gain, -24.0, 24.0, -89.8, 6.0) * 10d),
                        pan = (int)(RangeMapper(info.Tracks[i].Pan, -1.0, 1.0, 0, 128.0)),
                        m = (byte)(info.Tracks[i].Mute ? 1 : 0),
                        s = (byte)(info.Tracks[i].Solo ? 1 : 0)
                    };
                }
                vsq.mixer.monoUnit = new monoUnit() { iGin = 0, sLvl = 0, sEnable = 0, m = 0, s = 0, pan = 64, vol = 0 };
                vsq.mixer.stUnit = new stUnit() { iGin = 0, m = 0, s = 0, vol = -129 };
            }
            //主声轨属性初始化
            {
                vsq.masterTrack = new masterTrack()
                {
                    seqName = "TunelabExport",
                    comment = "VSQ4Mode",
                    resolution = 480,
                    preMeasure = 0//前置空多少小结
                };
                vsq.masterTrack.timeSig = new timeSig[info.TimeSignatures.Count];
                for (int i = 0; i < info.TimeSignatures.Count; i++)
                {
                    vsq.masterTrack.timeSig[i] = new timeSig();
                    vsq.masterTrack.timeSig[i].m = info.TimeSignatures[i].BarIndex;
                    vsq.masterTrack.timeSig[i].nu = (byte)info.TimeSignatures[i].Numerator;
                    vsq.masterTrack.timeSig[i].de = (byte)info.TimeSignatures[i].Denominator;
                }
                vsq.masterTrack.tempo = new tempo[info.Tempos.Count];
                for(int i=0;i<info.Tempos.Count;i++)
                {
                    vsq.masterTrack.tempo[i] = new tempo();
                    vsq.masterTrack.tempo[i].t = (int)(info.Tempos[i].Pos * 100);
                    vsq.masterTrack.tempo[i].v = (int)(info.Tempos[i].Bpm * 100);
                }
            }
            //媒体轨道初始化
            {
                vsq.vsTrack = new vsTrack[info.Tracks.Count];
                Parallel.For(0, info.Tracks.Count, (ti) => {
                    TrackInfo t;
                    lock (info.Tracks){t = info.Tracks[ti];}
                    vsTrack ret = new vsTrack();
                    ret.tNo = (byte)ti;
                    ret.name = t.Name;
                    ret.comment = t.Name;
                    ret.vsPart = new vsPart[t.Parts.Count];
                    for(int pi=0;pi<t.Parts.Count;pi++)
                    {
                        if (t.Parts[pi].GetType() != typeof(MidiPartInfo)) continue;
                        MidiPartInfo p = (MidiPartInfo)t.Parts[pi];
                        vsPart vp = new vsPart();
                        vp.t = (int)p.Pos;
                        vp.playTime = (int)p.Dur;
                        vp.name = p.Name;
                        vp.comment = p.Name;
                        vp.sPlug = new sPlug()
                        {
                            id = "ACA9C502-A04B-42b5-B2EB-5CEA36D16FCE",
                            name = "VOCALOID2 Compatible Style",
                            version = "3.0.0.1"
                        };
                        vp.pStyle = new typeParamAttr[7] {
                            new typeParamAttr(){id="accent",Value=50},
                            new typeParamAttr(){id="bendDep",Value=8},
                            new typeParamAttr(){id="bendLen",Value=0},
                            new typeParamAttr(){id="decay",Value=50},
                            new typeParamAttr(){id="fallPort",Value=0},
                            new typeParamAttr(){id="opening",Value=127},
                            new typeParamAttr(){id="risePort",Value=0},
                        };
                        vp.singer = new singer[1] { new singer(){ t = 0, bs = 4, pc = 0 } };//和头部vVoiceTable对应
                        List<Task> pallTasks = new List<Task>();
                        //复制Note
                        pallTasks.Add(Task.Factory.StartNew(() =>
                        {
                            List<vsq4.note> noteList = new List<vsq4.note>();
                            for (int i = 0; i < p.Notes.Count; i++)
                            {
                                note vsnote = new note();
                                vsnote.t = (int)p.Notes[i].Pos;
                                vsnote.dur = (int)p.Notes[i].Dur;
                                vsnote.n = (byte)p.Notes[i].Pitch;
                                vsnote.v = 64;
                                vsnote.y = p.Notes[i].Lyric;
                                vsnote.p = new typePhonemes() { Value = p.Notes[i].Properties.GetValue<string>("Phoneme", "a") };
                                vsnote.nStyle = new nStyle();
                                vsnote.nStyle.v = new typeParamAttr[9]
                                {
                                    new typeParamAttr(){id="accent",Value=50},
                                    new typeParamAttr(){id="bendDep",Value=8},
                                    new typeParamAttr(){id="bendLen",Value=0},
                                    new typeParamAttr(){id="decay",Value=50},
                                    new typeParamAttr(){id="fallPort",Value=0},
                                    new typeParamAttr(){id="opening",Value=127},
                                    new typeParamAttr(){id="risePort",Value=0},
                                    new typeParamAttr(){id="vibLen",Value=0},
                                    new typeParamAttr(){id="vibType",Value=0},
                                };
                                noteList.Add(vsnote);
                            }
                            lock (vp) { vp.note = noteList.ToArray(); }
                        }));
                        //复制常规参数
                        List<vsq4.cc> ccList = new List<cc>();
                        void SyncAutomation2(string srcKey, string targetKey, double defaultValue, SyncAutomationPoint callback)
                        {
                            if (!p.Automations.ContainsKey(targetKey)) return;
                            List<vsq4.cc> tmpList = new List<cc>();
                            foreach (Point pp in p.Automations[targetKey].Points)
                            {
                                tmpList.Add(new cc() { t = (int)pp.X, v = new typeParamAttr() { id = srcKey, Value = (int)callback(pp.Y) } });
                            }
                            lock (ccList) { ccList.AddRange(tmpList); };
                        }
                        pallTasks.Add(Task.Factory.StartNew(() => { SyncAutomation2("D", "Dynamics", 64, new SyncAutomationPoint((inp) => { return Math.Min(127,RangeMapper(inp,-1.0,1.0,0,128)); })) ; }));
                        pallTasks.Add(Task.Factory.StartNew(() => { SyncAutomation2("R", "Brightness", 64, new SyncAutomationPoint((inp) => { return Math.Min(127, RangeMapper(inp, -1.0, 1.0, 0, 128)); })); }));
                        pallTasks.Add(Task.Factory.StartNew(() => { SyncAutomation2("G", "Gender", 64, new SyncAutomationPoint((inp) => { return Math.Min(127, RangeMapper(inp, -1.0, 1.0, 0, 128)); })); }));
                        pallTasks.Add(Task.Factory.StartNew(() => { SyncAutomation2("W", "Growl", 0, new SyncAutomationPoint((inp) => { return RangeMapper(inp, 0, 1.0, 0, 127); })); }));
                        pallTasks.Add(Task.Factory.StartNew(() => { SyncAutomation2("C", "Clearness", 0, new SyncAutomationPoint((inp) => { return RangeMapper(inp, 0, 1.0, 0, 127); })); }));
                        pallTasks.Add(Task.Factory.StartNew(() => { SyncAutomation2("B", "Breathiness", 0, new SyncAutomationPoint((inp) => { return RangeMapper(inp, 0, 1.0, 0, 127); })); }));

                        //处理PBS
                        pallTasks.Add(Task.Factory.StartNew(() => {
                            //计算基础音高
                            SortedDictionary<int, double> basePitch = new SortedDictionary<int, double>();
                            {
                                for (int i = 0; i < p.Notes.Count; i++)
                                {
                                    basePitch.Add((int)p.Notes[i].EndPos(), p.Notes[i].Pitch);//因为Pit与音符向后关联，所以取音符最后一个点即可。
                                   /* if (basePitch.ContainsKey((int)p.Notes[i].StartPos()))
                                        basePitch[(int)p.Notes[i].StartPos()] = p.Notes[i].Pitch;
                                    else
                                        basePitch.Add((int)p.Notes[i].StartPos(), p.Notes[i].Pitch);//但是为了避免动态音高导致串音，前点也加上*/
                                }
                            }
                            //计算绝对音高
                            SortedDictionary<int, double> absPitch = new SortedDictionary<int, double>(basePitch);
                            //从PIT获取绝对音高
                            if(p.Automations.ContainsKey("P") && p.Automations["P"].Points.Count>0){
                                SortedDictionary<int, int> pbs = new SortedDictionary<int, int>();
                                if (p.Automations.ContainsKey("S") && p.Automations["S"].Points.Count > 0) for (int pt = 0; pt < p.Automations["S"].Points.Count; pt++) pbs.Add((int)p.Automations["S"].Points[pt].X, (int)p.Automations["S"].Points[pt].Y);
                                else pbs.Add(0, 2);
                                Parallel.ForEach<Point>(p.Automations["P"].Points, (pT) => {
                                    var pitT = pT.X;
                                    var pitV = pT.Y;
                                    int PBS = pbs[pbs.Keys.LastOrDefault(key => key <= pitT)];
                                    var absPitV = (double)PBS * (pitV / (pitV > 0 ? 8191.0d : 8192.0d));
                                    var absPitB = (double)basePitch[basePitch.Keys.FirstOrDefault(key => key >= pitT)];
                                    var absPit = absPitB + absPitV;
                                    lock (absPitch) { if (absPitch.ContainsKey((int)pitT)) absPitch[(int)pitT] = absPit; else absPitch.Add((int)pitT, absPit); }
                                });
                            }
                            //从PithLine更新绝对音高
                            for(int pli =0;pli<p.Pitch.Count;pli++)
                            {
                                List<Point> pLine = p.Pitch[pli];
                                Parallel.ForEach<Point>(p.Pitch[pli], (pp) =>
                                {
                                    lock (absPitch)
                                    {
                                        if (absPitch.ContainsKey((int)pp.X))
                                            absPitch[(int)pp.X] = pp.Y;
                                        else
                                            absPitch.Add((int)pp.X, pp.Y);
                                    }
                                });
                            }
                            //从绝对音高取回PIT
                            SortedDictionary<int,double> relPit= new SortedDictionary<int,double>();//单独存是为了让PBS小一点，减少毛刺。
                            Parallel.ForEach(absPitch, (kv => {
                                var pitT = kv.Key;
                                var absPitV = kv.Value;
                                var absPitB = (double)basePitch[basePitch.Keys.FirstOrDefault(key => key >= pitT)];
                                double pitV = (absPitV - absPitB);
                                lock (relPit) { relPit.Add(pitT, pitV); }
                            }));
                            //输出PIT
                            int outputPBS=Math.Min(24,Math.Max(2,(int)relPit.Values.Max()+1));
                            List<vsq4.cc> tmpList = new List<cc>();
                            Parallel.ForEach(relPit, (kv => {
                                var pitT=kv.Key;
                                double pitV = kv.Value/(double)outputPBS;
                                double pitS = pitV>0?pitV*8191.0d:pitV * 8192.0d;
                                pitS = Math.Max(-8192.0, Math.Min(8191.0,pitS));
                                lock (tmpList)
                                {
                                    tmpList.Add(new cc() { t=pitT,v=new typeParamAttr() { id="P",Value=(int)pitS} });
                                }
                            }));
                            lock (ccList) {
                                ccList.Add(new cc() { t = 0, v = new typeParamAttr() { id = "S", Value = outputPBS } });
                                ccList.AddRange(tmpList); 
                            };
                        }));

                        Task.WaitAll(pallTasks.ToArray());
                        vp.cc = ccList.ToArray();
                        vp.plane = 1;//PITCHMODE
                        vp.planeSpecified = true;
                        ret.vsPart[pi]= vp;
                    }
                    lock (vsq.vsTrack) { vsq.vsTrack[ti] = ret; }
                });
            }
            //伴奏
            {
                vsq.monoTrack = new wavPart[0];
                vsq.stTrack = new wavPart[0];
                vsq.aux = new aux[1];
                vsq.aux[0] = new aux()
                {
                    id = "AUX_VST_HOST_CHUNK_INFO",
                    content = "VlNDSwAAAAADAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                };
            }
            //Ser
            if(stream.CanSeek)stream.Position = 0;
            XmlSerializer serializer = new XmlSerializer(typeof(vsq4.vsq4));
            serializer.Serialize(stream, vsq);
            if (stream.CanSeek) stream.Position = 0;
        }
    }
}
