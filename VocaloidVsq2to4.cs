using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TuneLab.Extensions.Formats.VSQX.vsq4;
using VsqxFormat.vsq2;
using VsqxFormat.vsq2.smf;

namespace TuneLab.Extensions.Formats.VSQX
{
    public class Vsq4Adapter2
    {
        public static bool checkIsV2Project(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            var magicHead = new string(reader.ReadChars(4));
            stream.Position = 0;
            if (magicHead == "MThd") return true;
            return false;
        }
        enum VsqMetaType
        {
            DataBlock = 1,
            Name = 3,
            TempoItem = 0x51,
            TimeSigItem = 0x58,
        }
        public static vsq4.vsq4 loadFromStream(Stream stream)
        {
            vsq4.vsq4 ret=new vsq4.vsq4();
            //初始化VSQ4工程
            {
                ret.masterTrack = new vsq4.masterTrack() { comment = "",preMeasure=0,seqName="",resolution=480};
            }
            //初始化歌手标记
            {
                ret.vVoiceTable = new vVoice[1];
                ret.vVoiceTable[0] = new vVoice();
                ret.vVoiceTable[0].bs = 0;//JP
                ret.vVoiceTable[0].pc = 0;
                ret.vVoiceTable[0].id = "BYHKHFC6YWBDEN9C";
                ret.vVoiceTable[0].name = "Yuki";
                ret.vVoiceTable[0].vPrm = new vPrm();
                ret.vVoiceTable[0].vPrm.cle = 0;
                ret.vVoiceTable[0].vPrm.bre = 0;
                ret.vVoiceTable[0].vPrm.bri = 0;
                ret.vVoiceTable[0].vPrm.gen = 0;
                ret.vVoiceTable[0].vPrm.ope = 0;
                ret.vVoiceTable[0].id2 = ret.vVoiceTable[0].id;
                ret.vVoiceTable[0].vPrm2 = new vPrm2() { bre = 0, cle = 0, bri = 0, gen = 0, ope = 0, vol = 0 };
            }
            //读取V2
            SMF_Midi smf = SMF_Midi.Read(stream);
            
            //读取工程整体信息
            var projMsg = smf.Tracks[0];
            string projName;
            SortedDictionary<int, long> tempoList = new SortedDictionary<int, long>() { { 0,12000}};
            List<vsq4.timeSig> timeSigList = new List<timeSig>() { new vsq4.timeSig() { m = 0, nu = (byte)4, de = (byte)4 } };
            {
                int sig_dno = 4;int sig_num = 4;int sig_cnt = -1;int sig_lastTick = 0;
                foreach (MidiMessage msg in projMsg.Messages)
                {
                    switch (msg.Event.MetaType)
                    {
                        case (byte)VsqMetaType.Name:
                            projName = msg.Event.StringData;
                            break;
                        case (byte)VsqMetaType.TempoItem:
                            if (msg.Event.Data!=null && msg.Event.Data.Length >= 3)
                            {
                                long tempoVal = 6000000000 / (msg.Event.Data[0] << 16 | msg.Event.Data[1] << 8 | msg.Event.Data[2]);
                                int tempoTick = msg.DeltaTime * 100;
                                if (tempoList.ContainsKey(tempoTick)) tempoList[tempoTick] = tempoVal; else tempoList.Add(tempoTick, tempoVal);
                            }
                            break;
                        case (byte)VsqMetaType.TimeSigItem:
                            if (msg.Event.Data != null && msg.Event.Data.Length >= 5)
                            {
                                sig_cnt++;
                                sig_num = msg.Event.Data[0];
                                sig_dno = 1;
                                for (int i = 0; i < msg.Event.Data[1]; i++) sig_dno = sig_dno * 2;
                                sig_lastTick = msg.DeltaTime;
                                if (sig_cnt == 0)
                                {
                                    if (msg.DeltaTime == 0)
                                    {
                                        timeSigList[0].m = 0;
                                        timeSigList[0].nu = (byte)sig_num;
                                        timeSigList[0].de = (byte)sig_dno;
                                    }
                                    else
                                    {
                                        timeSigList.Add(new vsq4.timeSig() { m = 0, nu = (byte)4, de = (byte)4 });
                                        timeSigList.Add(new vsq4.timeSig() { m = msg.DeltaTime/(480*4), nu = (byte)sig_num, de = (byte)sig_dno });
                                    }
                                }else
                                {
                                    var lastSig = timeSigList.Last();
                                    int last_nu = lastSig.nu;
                                    int last_de = lastSig.de;
                                    int tick = sig_lastTick;
                                    int barCount = lastSig.m;
                                    int dif = 480 * 4 / last_de * last_nu;
                                    barCount += ((int)(msg.DeltaTime - tick)) / dif;
                                    timeSigList.Add(new timeSig() {m=barCount,nu=(byte)sig_num,de=(byte)sig_dno });
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            //读取轨道信息
            List<VsqMidiTrack> vTracks = new List<VsqMidiTrack>();
            VsqMidiMaster? master = null;
            VsqMidiMixer? mixer = null;
            for (int i = 1; i < smf.Tracks.Count; i++)
            {
                var track = smf.Tracks[i];
                string trackName = "";
                StringBuilder trackData = new StringBuilder();
                foreach (MidiMessage msg in track.Messages)
                {
                    switch (msg.Event.MetaType)
                    {
                        case (byte)VsqMetaType.Name:
                            trackName = msg.Event.StringData;
                            break;
                        case (byte)VsqMetaType.DataBlock:
                            var data = msg.Event.StringData;
                            if (data.Length > 0 && data.StartsWith("DM:"))
                            {
                                var rData = data.Substring(7);
                                while (rData[0] != ':') rData = rData.Substring(1);
                                trackData.Append(rData.Substring(1));
                            }
                            break;
                        default:
                            break;
                    }
                }
                VsqMidiTrack vmT = new VsqMidiTrack(trackName, trackData.ToString());
                vTracks.Add(vmT);
                if (mixer == null && vmT.Mixer != null) mixer = vmT.Mixer;
                if (master == null && vmT.Master != null) master = vmT.Master;
            }
            //填充到VSQ
            {
                if (mixer == null) mixer = new VsqMidiMixer();
                if (master == null) master = new VsqMidiMaster() ;
                ret.vsTrack = new vsq4.vsTrack[vTracks.Count];
                for(int i=0;i<vTracks.Count;i++)
                {
                    ret.vsTrack[i] = vTracks[i].ToVsq4Track(i);
                }
                ret.masterTrack.resolution = (ushort)smf.DeltaTimeSpec;
                ret.masterTrack.preMeasure = (byte)master.PreMeasure;
                ret.mixer = new vsq4.mixer()
                {
                    masterUnit = new vsq4.masterUnit()
                    {
                        oDev = 0,
                        rLvl = 0,
                        vol = 0
                    },
                    monoUnit = new vsq4.monoUnit() { },
                    stUnit = new vsq4.stUnit() { },
                    vsUnit = new vsq4.vsUnit[mixer.trackUnits.Count]
                };
                for(int i=0;i<mixer.trackUnits.Count;i++)
                {
                    vsq4.vsUnit vsu = new vsq4.vsUnit()
                    {
                        m = (byte)(mixer.trackUnits[i].Mute ? 1 : 0),
                        s = (byte)(mixer.trackUnits[i].Solo ? 1 : 0),
                        pan = mixer.trackUnits[i].PanpotV4,
                        iGin = mixer.trackUnits[i].GainV4,
                        tNo = (byte)i
                    };
                    ret.mixer.vsUnit[i] = vsu;

                }

                ret.masterTrack.timeSig = new vsq4.timeSig[timeSigList.Count];
                for(int i=0;i< timeSigList.Count; i++) { ret.masterTrack.timeSig[i] = timeSigList[i]; };

                List<vsq4.tempo> tmpo = new List<tempo>();
                foreach(var kvp in tempoList){ tmpo.Add(new vsq4.tempo() { t = kvp.Key, v = (int)kvp.Value }); };
                ret.masterTrack.tempo = tmpo.ToArray();
            }
            return ret;
        }
    }
}
