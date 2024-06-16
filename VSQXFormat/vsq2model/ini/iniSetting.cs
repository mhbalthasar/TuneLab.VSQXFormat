using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsqxFormat.vsq2.ini
{
    /// <summary>
    /// Represents the name/value pair of an INI-file setting.
    /// </summary>
    public class IniSetting
    {
        /// <summary>
        /// The name of this INI setting.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The value of this INI setting.
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Converts this <see cref="IniSetting"></see> to a string.
        /// </summary>
        /// <remarks>
        /// This method is used to write each setting to the INI file and
        /// so the result must fit that format.
        /// </remarks>
        public override string ToString() => $"{Name ?? string.Empty}={Value ?? string.Empty}";
    }
}
