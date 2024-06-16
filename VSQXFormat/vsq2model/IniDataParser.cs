using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using VsqxFormat.vsq2.ini;

namespace VsqxFormat.vsq2
{
    public class IniDataParser:IniFile
    {
        public void LoadFromString(string Data,string LineSpliter="\n")
        {
            string[] sArray = Data.Split(LineSpliter);

            IniSection? section = null;
            Clear();
            for(int i=0;i<sArray.Length;i++)
            {
                base.ParseLine(sArray[i], ref section);
            }
        }
    }
}
