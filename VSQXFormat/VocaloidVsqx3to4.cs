using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuneLab.Base.Structures;

namespace TuneLab.Extensions.Formats.VSQX
{
    class Vsq4Adapter3
    {
        public static vsq4.vsq4 trans(vsq3.vsq3 src)
        {
            vsq4.vsq4 ret = new vsq4.vsq4();
            if(src.masterTrack!=null) ret.masterTrack = trans_masterTrack(src.masterTrack);
            if (src.vsTrack != null)
            {
                ret.vsTrack = new vsq4.vsTrack[src.vsTrack.Length];
                for (int i = 0; i < src.vsTrack.Length; i++)
                {
                    ret.vsTrack[i] = trans_vsTrack(src.vsTrack[i]);
                }
            }
            if(src.mixer!=null && src.mixer.vsUnit != null)
            {
                ret.mixer = new vsq4.mixer();
                ret.mixer.vsUnit = new vsq4.vsUnit[src.mixer.vsUnit.Length];
                for(int i = 0; i < src.mixer.vsUnit.Length; i++)
                {
                    ret.mixer.vsUnit[i] = new vsq4.vsUnit();
                    ret.mixer.vsUnit[i].m = src.mixer.vsUnit[i].mute;
                    ret.mixer.vsUnit[i].s = src.mixer.vsUnit[i].solo;
                    ret.mixer.vsUnit[i].pan = src.mixer.vsUnit[i].pan;
                    ret.mixer.vsUnit[i].tNo = src.mixer.vsUnit[i].vsTrackNo;
                    ret.mixer.vsUnit[i].vol = src.mixer.vsUnit[i].vol;
                    ret.mixer.vsUnit[i].iGin = src.mixer.vsUnit[i].inGain;
                    ret.mixer.vsUnit[i].sLvl = src.mixer.vsUnit[i].sendLevel;
                    ret.mixer.vsUnit[i].sEnable = src.mixer.vsUnit[i].sendEnable;
                }
            }
            if (src.vVoiceTable != null)
            {
                ret.vVoiceTable = new vsq4.vVoice[src.vVoiceTable.Length];
                for (int i = 0; i < ret.vVoiceTable.Length; i++)
                {
                    ret.vVoiceTable[i] = new vsq4.vVoice();
                    ret.vVoiceTable[i].pc = src.vVoiceTable[i].vPC;
                    ret.vVoiceTable[i].bs = src.vVoiceTable[i].vBS;
                    ret.vVoiceTable[i].id = src.vVoiceTable[i].compID;
                    ret.vVoiceTable[i].name = src.vVoiceTable[i].vVoiceName;
                    ret.vVoiceTable[i].vPrm = new vsq4.vPrm();
                    ret.vVoiceTable[i].vPrm.cle = src.vVoiceTable[i].vVoiceParam.cle;
                    ret.vVoiceTable[i].vPrm.bri = src.vVoiceTable[i].vVoiceParam.bri;
                    ret.vVoiceTable[i].vPrm.bre = src.vVoiceTable[i].vVoiceParam.bre;
                    ret.vVoiceTable[i].vPrm.gen = src.vVoiceTable[i].vVoiceParam.gen;
                    ret.vVoiceTable[i].vPrm.ope = src.vVoiceTable[i].vVoiceParam.ope;
                }
            }
            else ret.vVoiceTable = new vsq4.vVoice[0];
            return ret;
        }
        private static vsq4.masterTrack trans_masterTrack(vsq3.masterTrack src)
        {
            //转义MasterTrack：包括TEMPO等全局参数
            vsq4.masterTrack ret = new vsq4.masterTrack();
            ret.resolution = src.resolution;
            ret.preMeasure = src.preMeasure;
            ret.comment = src.comment;
            ret.seqName = src.seqName;
            if (src.timeSig != null)
            {
                ret.timeSig = new vsq4.timeSig[src.timeSig.Length];
                for (int i = 0; i < src.timeSig.Length; i++)
                {
                    ret.timeSig[i] = new vsq4.timeSig();
                    ret.timeSig[i].m = src.timeSig[i].posMes;
                    ret.timeSig[i].nu = src.timeSig[i].nume;
                    ret.timeSig[i].de = src.timeSig[i].denomi;
                }
            }
            if (src.tempo != null)
            {
                ret.tempo = new vsq4.tempo[src.tempo.Length];
                for (int i = 0; i < src.tempo.Length; i++)
                {
                    ret.tempo[i] = new vsq4.tempo();
                    ret.tempo[i].t = src.tempo[i].posTick;
                    ret.tempo[i].v = src.tempo[i].bpm;
                }
            }
            return ret;
        }

        private static vsq4.vsTrack trans_vsTrack(vsq3.vsTrack src)
        {
            vsq4.vsTrack ret = new vsq4.vsTrack();
            ret.tNo = src.vsTrackNo;
            ret.name = src.trackName;
            ret.comment = src.comment;
            if (src.Items != null)
            {
                ret.vsPart = new vsq4.vsPart[src.Items.Length];
                for (int i = 0; i < src.Items.Length; i++)
                {
                    ret.vsPart[i] = trans_vsPart((vsq3.musicalPart)src.Items[i]);
                }
            }
            return ret;
        }
        private static string Ctrl3to4(string v3str)
        {
            if (v3str.Equals("DYN")) return "D";
            if (v3str.Equals("BRE")) return "B";
            if (v3str.Equals("BRI")) return "R";
            if (v3str.Equals("CLE")) return "C";
            if (v3str.Equals("GEN")) return "G";
            if (v3str.Equals("POR")) return "T";
            if (v3str.Equals("PBS")) return "S";
            if (v3str.Equals("PIT")) return "P";
            if (v3str.Equals("XSY")) return "X";
            if (v3str.Equals("GWL")) return "W";
            return "Z";
        }
        private static vsq4.vsPart trans_vsPart(vsq3.musicalPart src)
        {
            vsq4.vsPart ret = new vsq4.vsPart();
            ret.t = src.posTick;
            ret.playTime = src.playTime;
            ret.name = src.partName;
            ret.comment = src.comment;
            if (src.stylePlugin != null)
            {
                ret.sPlug = new vsq4.sPlug();
                ret.sPlug.id = src.stylePlugin.stylePluginID;
                ret.sPlug.name = src.stylePlugin.stylePluginName;
                ret.sPlug.version = src.stylePlugin.version;
            }
            if (src.partStyle != null)
            {
                ret.pStyle = new vsq4.typeParamAttr[src.partStyle.Length];
                for (int i = 0; i < src.partStyle.Length; i++)
                {
                    ret.pStyle[i] = new vsq4.typeParamAttr();
                    ret.pStyle[i].id = src.partStyle[i].id;
                    ret.pStyle[i].Value = src.partStyle[i].Value;
                }
            }
            if(src.singer!=null)
            {
                ret.singer = new vsq4.singer[1];
                ret.singer[0] = new vsq4.singer() { t = 0, bs = src.singer[0].vBS, pc = src.singer[0].vPC };
            }
            if (src.mCtrl != null)
            {
                ret.cc = new vsq4.cc[src.mCtrl.Length];
                Parallel.For(0, src.mCtrl.Length, (i) =>
                {
                    vsq3.mCtrl ctl;
                    lock (src.mCtrl)
                    {
                        ctl = src.mCtrl[i];
                    }
                    vsq4.cc cc = new vsq4.cc();
                    cc.t = ctl.posTick;
                    cc.v = new vsq4.typeParamAttr();
                    cc.v.id = Ctrl3to4(ctl.attr.id);
                    cc.v.Value = ctl.attr.Value;
                    lock (ret)
                    {
                        ret.cc[i] = cc;
                    }
                });
            }
            if (src.note != null)
            {
                ret.note = new vsq4.note[src.note.Length];
                for (int i = 0; i < src.note.Length; i++)
                {
                    ret.note[i] = trans_vsNote(src.note[i]);
                }
            }
            return ret;
        }
        private static vsq4.note trans_vsNote(vsq3.note src)
        {
            vsq4.note ret = new vsq4.note();
            ret.t = src.posTick;
            ret.dur = src.durTick;
            ret.n = src.noteNum;
            ret.v = src.velocity;
            ret.y = src.lyric;
            if (src.phnms != null)
            {
                ret.p = new vsq4.typePhonemes();
                ret.p.Value = src.phnms.Value;
                ret.p.lockSpecified = src.phnms.lockSpecified;
            }
            ret.nStyle = new vsq4.nStyle();
            if (src.noteStyle!=null && src.noteStyle.attr != null)
            {
                ret.nStyle.v = new vsq4.typeParamAttr[src.noteStyle.attr.Length];
                for (int i = 0; i < src.noteStyle.attr.Length; i++)
                {
                    ret.nStyle.v[i] = new vsq4.typeParamAttr();
                    ret.nStyle.v[i].Value = src.noteStyle.attr[i].Value;
                    ret.nStyle.v[i].id = src.noteStyle.attr[i].id;
                }
            }
            if (src.noteStyle != null && src.noteStyle.seqAttr != null)
            {
                ret.nStyle.seq = new vsq4.seq[src.noteStyle.seqAttr.Length];
                for (int i = 0; i < src.noteStyle.seqAttr.Length; i++)
                {
                    ret.nStyle.seq[i] = new vsq4.seq();
                    ret.nStyle.seq[i].id = src.noteStyle.seqAttr[i].id;
                    ret.nStyle.seq[i].cc = new vsq4.seqCC[src.noteStyle.seqAttr[i].elem.Length];
                    for (int j = 0; j < src.noteStyle.seqAttr[i].elem.Length; j++)
                    {
                        ret.nStyle.seq[i].cc[j] = new vsq4.seqCC();
                        ret.nStyle.seq[i].cc[j].v = src.noteStyle.seqAttr[i].elem[j].elv;
                        ret.nStyle.seq[i].cc[j].p = src.noteStyle.seqAttr[i].elem[j].posNrm;
                    }
                }
            }
            return ret;
        }
    }
}
