// Ignore Spelling: Yaml yaml

using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace FiftyOne.DeviceDetection.Examples
{
    /// <summary>
    /// Simple writer to a text stream in YAML format using YamlDotNet.
    /// </summary>
    /// <remarks>
    /// Must be disposed of to write the end of documents marker.
    /// </remarks>
    public class ExampleWriterYaml : IExampleWriter
    {
        private static readonly Serializer _serializer = new Serializer();

        private readonly TextWriter _output;

        public ExampleWriterYaml(TextWriter output)
        {
            _output = output;
        }

        public void Dispose()
        {
            // write the yaml document end marker
            _output.WriteLine("...");
        }

        public void Write(Dictionary<string, string> data)
        {
            // write the yaml document separator
            _output.WriteLine("---");

            // write the data as a yaml document
            _serializer.Serialize(_output, data);
        }
    }
}
