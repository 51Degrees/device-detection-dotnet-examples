using System;
using System.Collections.Generic;

namespace FiftyOne.DeviceDetection.Examples
{
    /// <summary>
    /// Interface for writing example output. Used to easilly vary between
    /// YAML, CSV, and other types.
    /// </summary>
    public interface IExampleWriter : IDisposable
    {
        /// <summary>
        /// Writes the dictionary to the output.
        /// </summary>
        /// <param name="data"></param>
        void Write(Dictionary<string, string> data);
    }
}
