using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuneLab.Extensions.Formats.VSQX.vsq4;
using VsqxFormat.vsq2.ini;

namespace VsqxFormat.vsq2
{
    internal class VsqMidiBPList
    {
        public readonly char BPType;
        public readonly string BPTypeComment;

        List<TuneLab.Extensions.Formats.VSQX.vsq4.cc> ctl = new List<TuneLab.Extensions.Formats.VSQX.vsq4.cc>();
        public VsqMidiBPList(char BPType, string BPTypeComment = "") { this.BPType = BPType; this.BPTypeComment = BPTypeComment.Length>0?BPTypeComment:""+BPType; }
        public VsqMidiBPList(IniDataParser parser, string SectionKey,char BPType, string BPTypeComment = "") {
            this.BPType = BPType; this.BPTypeComment = BPTypeComment.Length > 0 ? BPTypeComment : "" + BPType;
            this.LoadData(parser, SectionKey);
        }

        public List<cc> CtlCollection { get => ctl;}

        public void LoadData(IniDataParser parser, string SectionKey)
        {
            IEnumerable<IniSetting> t;
            lock (parser)
            {
                t = parser.GetSectionSettings(SectionKey);
            }
            if (t == null) return;
            Parallel.ForEach(t, (kvp) => {
                try
                {
                    TuneLab.Extensions.Formats.VSQX.vsq4.cc ct = new cc()
                    {
                        t = int.Parse(kvp.Name),
                        v = new typeParamAttr()
                        {
                            id = "" + this.BPType,
                            Value = int.Parse(kvp.Value)
                        }
                    };
                    lock (ctl) { ctl.Add(ct); }
                }
                catch {; }
            });
            ctl = ctl.OrderBy(p => p.t).ToList();//顺序排序
        }
    }
}
