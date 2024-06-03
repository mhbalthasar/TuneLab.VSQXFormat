using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsqxFormat.vsq2
{
    internal class VsqMidiMaster
    {
        public int PreMeasure { get; set; }

        public VsqMidiMaster() { PreMeasure=0; }

        public VsqMidiMaster(IniDataParser parser, string Key = "Master")
        {
            int i = 0;
            if(int.TryParse(parser.GetSetting(Key, "PreMeasure", "0"),out i))
            {
                PreMeasure = i;
            };
        }
    }
}
