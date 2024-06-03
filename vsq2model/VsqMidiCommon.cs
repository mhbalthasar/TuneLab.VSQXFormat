using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsqxFormat.vsq2
{
    internal class VsqMidiCommon
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public Color ColorRGB { get; set; }
        public bool DynamicsMode { get; set; }
        public bool PlayMode { get; set; }
        public VsqMidiCommon() { Name = "";Version = ""; }

        public VsqMidiCommon(IniDataParser parser,string Key="Common")
        {
            bool ReadBool(string SettingKey, bool def)
            {
                int ret = 0;
                if (int.TryParse(parser.GetSetting(Key, SettingKey, def ? "1" : "0"), out ret))
                {
                    return ret == 1;
                }
                return def;
            }

            Name = parser.GetSetting(Key, "Name", "");
            Version = parser.GetSetting(Key, "Version", "");
            DynamicsMode = ReadBool("DynamicsMode", true);
            PlayMode = ReadBool("PlayMode", true);
            try
            {
                string[] RGB = parser.GetSetting(Key, "Color", "0,0,0").Split(',');
                ColorRGB = Color.FromArgb(int.Parse(RGB[0]), int.Parse(RGB[1]), int.Parse(RGB[2]));
            }
            catch {; }
        }
    }
}
