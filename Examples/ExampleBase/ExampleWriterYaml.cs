/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

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
