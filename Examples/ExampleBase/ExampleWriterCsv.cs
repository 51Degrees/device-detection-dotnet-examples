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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiftyOne.DeviceDetection.Examples
{
    /// <summary>
    /// Simple writer to a text stream in CSV format.
    /// </summary>
    /// <remarks>
    /// Must be disposed of to write the records. As CSV requires the headings
    /// to be known in advance this can not be done until all the records have
    /// been passed to the writer.
    /// </remarks>
    public class ExampleWriterCsv : IExampleWriter
    {
        private readonly TextWriter _output;

        private Queue<Dictionary<string,string>> _outputQueue = 
            new Queue<Dictionary<string, string>>();

        public ExampleWriterCsv(TextWriter output)
        {
            _output = output;
        }

        public void Dispose()
        {
            // write the headings
            var headings = _outputQueue.SelectMany(i => i.Keys)
                .Distinct().OrderBy(i => i).ToArray();
            _output.WriteLine(String.Join(",", headings));

            // write the records in the same order they were provided
            while (_outputQueue.Count > 0)
            {
                var record = _outputQueue.Dequeue();

                // ensure that the field values are in the same order as
                // the headings
                _output.WriteLine(String.Join(",", headings.Select(i =>
                {
                    if (record.TryGetValue(i, out var value))
                    {
                        return GetCsvEscaped(value);
                    }
                    return "";
                })));
            }
        }

        /// <summary>
        /// If the string value contains , or " then surround the string in 
        /// quotes and change the quote character to two characters.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public string GetCsvEscaped(string value)
        {
            var quote = false;
            for(var i = 0; i < value.Length; i++)
            {
                if (value[i] == ',' || value[i] == '\"')
                { 
                    quote = true;
                    break;
                }
            }
            if (quote) {
                value = "\""+ value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        public void Write(Dictionary<string, string> data)
        {
            _outputQueue.Enqueue(data);
        }
    }
}
