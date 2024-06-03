using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuneLab.Extensions.Formats.VSQX.vsq4;
using VsqxFormat.vsq2.ini;
using VsqxFormat.xml;
using static TuneLab.Base.Properties.PropertyPath;

namespace VsqxFormat.vsq2
{
    internal class VsqMidiNote
    {
        private TuneLab.Extensions.Formats.VSQX.vsq4.note baseNote = new note() {
            v = 127,
            p = new typeCDataPhonemes(new typePhonemes()),
            nStyle = new nStyle()
            {
                v = new typeParamAttr[9]
                                {
                                    new typeParamAttr(){id="accent",Value=50},//0
                                    new typeParamAttr(){id="bendDep",Value=8},
                                    new typeParamAttr(){id="bendLen",Value=0},
                                    new typeParamAttr(){id="decay",Value=50},
                                    new typeParamAttr(){id="fallPort",Value=0},
                                    new typeParamAttr(){id="opening",Value=127},
                                    new typeParamAttr(){id="risePort",Value=0},
                                    new typeParamAttr(){id="vibLen",Value=0},
                                    new typeParamAttr(){id="vibType",Value=0},//8
                                }
            }
        };
        
        public int Pos { get => baseNote.t; set => baseNote.t = value; }
        public int Dur { get => baseNote.dur;set=> baseNote.dur = value; }
        public int NoteNum { get => baseNote.n; set => baseNote.n = (byte)value; }
        public int PMBendDepth { get => baseNote.nStyle.v[1].Value; set => baseNote.nStyle.v[1].Value = value; }
        public int PMBendLength { get => baseNote.nStyle.v[2].Value; set => baseNote.nStyle.v[2].Value = value; }
        public bool PMbPortamentoUse { get => (baseNote.nStyle.v[4].Value==1 || baseNote.nStyle.v[6].Value == 1); set { baseNote.nStyle.v[4].Value = value?1:0; baseNote.nStyle.v[6].Value = value ? 1 : 0; } }
        public int DEMdecGainRate { get => baseNote.nStyle.v[3].Value; set => baseNote.nStyle.v[3].Value = value; }
        public int DEMaccent { get => baseNote.nStyle.v[0].Value; set => baseNote.nStyle.v[0].Value = value; }
        public string Lyric { get => baseNote.y; set => baseNote.y = value; }
        public string Phoneme { get => baseNote.p.Value; set => baseNote.p.Value = value; }
        public int Dynamics { get; set; }
        public note BaseNote { get => baseNote;}

        public VsqMidiNote(){}

        public bool TryLoad(IniDataParser parser, string NoteKey)
        {
            IEnumerable<IniSetting>? t=null;
            lock (parser)
            {
                string NoteType = parser.GetSetting(NoteKey, "Type", "Unknown");
                if(NoteType=="Anote")
                {
                    t = parser.GetSectionSettings(NoteKey);
                }
            }
            if (t == null) return false;
            {
                int ReadNumber(string SettingKey, string defaultValue)
                {
                    int ret = 0;
                    if (int.TryParse(parser.GetSetting(NoteKey, SettingKey, defaultValue), out ret))
                    {
                        return ret;
                    }
                    return int.Parse(defaultValue);
                }
                bool ReadBool(string SettingKey, bool def)
                {
                    int ret = 0;
                    if (int.TryParse(parser.GetSetting(NoteKey, SettingKey, def ? "1" : "0"), out ret))
                    {
                        return ret == 1;
                    }
                    return def;
                }

                Dur = ReadNumber("Length", "0"); if (Dur == 0) return false;
                NoteNum = ReadNumber("Note#", "0"); if (NoteNum == 0) return false;
                //TryLoadLyric
                {
                    string L0 = "";
                    lock (parser)
                    {
                        string LyricKey = parser.GetSetting(NoteKey, "LyricHandle", ""); 
                        if(LyricKey!="") L0=parser.GetSetting(LyricKey,"L0",""); 
                    }
                    if(L0=="") return false;
                    string[] Lyric0 = L0.Split(',');
                    if (Lyric0.Length < 2) return false;
                    string lrc = Lyric0[0];string phn = Lyric0[1];
                    if (!(lrc.StartsWith('"') && lrc.EndsWith('"'))) return false;
                    if (!(phn.StartsWith('"') && phn.EndsWith('"'))) return false;
                    Lyric = lrc.Substring(1, lrc.Length - 2);
                    Phoneme=phn.Substring(1, phn.Length - 2);
                }
                //TryLoadStyle
                {
                    Dynamics = ReadNumber("Dynamics", "0");
                    PMBendDepth = ReadNumber("PMBendDepth", "8");
                    PMBendLength = ReadNumber("PMBendLength", "0");
                    PMbPortamentoUse = ReadBool("PMbPortamentoUse", false);
                    DEMdecGainRate = ReadNumber("DEMdecGainRate", "50");
                    DEMaccent = ReadNumber("DEMaccent", "50");
                }
                //TryLoadVibrate
                {
                    //没实现，不管了
                    /*
                    string VibKey = parser.GetSetting(NoteKey, "VibratoHandle", "");
                    int VibLen = ReadNumber("VibratoDelay", 0);
                    */
                }
            }
            return true;
        }

        public static List<VsqMidiNote> LoadFromParser(IniDataParser parser, string SectionKey = "EventList")
        {
            List<VsqMidiNote> ret = new List<VsqMidiNote>();

            IEnumerable<IniSetting> t;
            lock (parser)
            {
                t = parser.GetSectionSettings(SectionKey);
            }
            if (t == null) return ret;
            Parallel.ForEach(t, (kvp) => {
                try
                {
                    VsqMidiNote n = new VsqMidiNote();
                    n.Pos = int.Parse(kvp.Name);
                    if(n.TryLoad(parser, kvp.Value)) lock (ret) { ret.Add(n); }
                }
                catch {; }
            });


            return ret.OrderBy(p => p.Pos).ToList(); ;
        }
    }
}
