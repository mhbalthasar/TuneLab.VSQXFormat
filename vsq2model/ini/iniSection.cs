using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsqxFormat.vsq2.ini
{
    /// <summary>
    /// Represents an INI file section.
    /// </summary>
    internal class IniSection : Dictionary<string, IniSetting>
    {
        /// <summary>
        /// The name of this INI section.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Constructs a new <see cref="IniSection"></see> instance.
        /// </summary>
        /// <param name="name">Name of this INI section.</param>
        /// <param name="comparer"><see cref="StringComparer"></see> used to
        /// look up setting names.</param>
        public IniSection(string name, StringComparer comparer)
            : base(comparer)
        {
            Name = name;
        }
    }
}
