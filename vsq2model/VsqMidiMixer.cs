using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuneLab.Extensions.Formats.VSQX.vsq4;

namespace VsqxFormat.vsq2
{
    internal class VsqMidiMixerUnit
    {
        public int Feder { get; set; }
        public int Panpot { get; set; }
        public bool Mute { get; set; }
        public bool Solo { get; set; }
        public VsqMidiMixerUnit()
        {
            Feder = 0; Panpot = 0; Solo = false; Mute = false;
        }

        public int PanpotV4 { get => (int)RangeMapper(this.Panpot, -10, 10, 0, 128); }
        public int GainV4 { get => (int)(RangeMapper(this.Feder, -100, 10, -89.8, 6.0) * 10); }
        private static double RangeMapper(double x, double minIn, double maxIn, double minOut, double maxOut)
        {
            return (x - minIn) * (maxOut - minOut) / (maxIn - minIn) + minOut;
        }
    }
    internal class VsqMidiMixer
    {
        public VsqMidiMixerUnit MasterUnit { get; set; }
        public int OutputMode { get; set; }

        public Dictionary<int,VsqMidiMixerUnit> trackUnits { get; set; }
        public VsqMidiMixer() { MasterUnit = new VsqMidiMixerUnit();trackUnits = new Dictionary<int, VsqMidiMixerUnit>(); }

        public VsqMidiMixer(IniDataParser parser, string Key = "Mixer")
        {
            MasterUnit = new VsqMidiMixerUnit(); trackUnits = new Dictionary<int, VsqMidiMixerUnit>();

            int ReadNumber(string SettingKey,string defaultValue)
            {
                int ret = 0;
                if(int.TryParse(parser.GetSetting(Key, SettingKey, defaultValue), out ret))
                {
                    return ret;
                }
                return int.Parse(defaultValue);
            }
            bool ReadBool(string SettingKey, bool def)
            {
                int ret = 0;
                if (int.TryParse(parser.GetSetting(Key, SettingKey, def?"1":"0"), out ret))
                {
                    return ret==1;
                }
                return def;
            }

            MasterUnit.Feder = ReadNumber("MasterFeder", "0");
            MasterUnit.Panpot = ReadNumber("MasterPanpot", "0");
            MasterUnit.Mute = ReadBool("MasterMute", false);
            OutputMode = ReadNumber("OutputMode", "0");

            int TrackCount = ReadNumber("Tracks", "0");
            for(int i=0; i < TrackCount; i++)
            {
                var t = new VsqMidiMixerUnit();
                t.Feder = ReadNumber(String.Format("Feder{0}", i), "0");
                t.Panpot = ReadNumber(String.Format("Panpot{0}", i), "0");
                t.Mute = ReadBool(String.Format("Mute{0}", i), false);
                t.Solo = ReadBool(String.Format("Solo{0}", i), false);
                trackUnits.Add(i, t);
            }
        }
    }
}
