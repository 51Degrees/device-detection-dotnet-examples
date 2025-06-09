/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2025 51 Degrees Mobile Experts Limited, Davidson House,
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
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FiftyOne.DeviceDetection.Examples.OnPremise.Performance
{
    /// <summary>
    /// Preprocesses evidence by converting string values to UTF-8 byte arrays
    /// to eliminate UTF-16 to UTF-8 conversion overhead during detection.
    /// </summary>
    public static class Utf8EvidencePreprocessor
    {
        /// <summary>
        /// Converts a dictionary of evidence with string values to UTF-8 encoded strings.
        /// This is done to eliminate the UTF-16 to UTF-8 conversion that happens during
        /// the SWIG marshaling process.
        /// </summary>
        public static List<Dictionary<string, object>> ConvertToUtf8(List<Dictionary<string, object>> evidenceList)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new List<Dictionary<string, object>>(evidenceList.Count);
            
            foreach (var evidence in evidenceList)
            {
                var utf8Evidence = new Dictionary<string, object>(evidence.Count);
                
                foreach (var kvp in evidence)
                {
                    // Convert string values to UTF-8 encoded strings
                    if (kvp.Value is string strValue)
                    {
                        // Pre-encode to UTF-8 bytes and back to string
                        // This ensures the string is already in UTF-8 format
                        var utf8Bytes = Encoding.UTF8.GetBytes(strValue);
                        utf8Evidence[kvp.Key] = Encoding.UTF8.GetString(utf8Bytes);
                    }
                    else
                    {
                        utf8Evidence[kvp.Key] = kvp.Value;
                    }
                }
                
                result.Add(utf8Evidence);
            }
            
            stopwatch.Stop();
            Console.WriteLine($"UTF-8 preprocessing took {stopwatch.ElapsedMilliseconds}ms for {evidenceList.Count} records");
            
            return result;
        }
        
        /// <summary>
        /// Alternative approach that stores evidence as UTF-8 byte arrays.
        /// This would require API changes to accept byte[] evidence.
        /// </summary>
        public static List<Dictionary<byte[], byte[]>> ConvertToUtf8Bytes(List<Dictionary<string, object>> evidenceList)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new List<Dictionary<byte[], byte[]>>(evidenceList.Count);
            
            foreach (var evidence in evidenceList)
            {
                var utf8Evidence = new Dictionary<byte[], byte[]>(evidence.Count);
                
                foreach (var kvp in evidence)
                {
                    var keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                    byte[] valueBytes;
                    
                    switch (kvp.Value)
                    {
                        case string str:
                            valueBytes = Encoding.UTF8.GetBytes(str);
                            break;
                        case byte[] bytes:
                            valueBytes = bytes;
                            break;
                        default:
                            valueBytes = Encoding.UTF8.GetBytes(kvp.Value?.ToString() ?? string.Empty);
                            break;
                    }
                    
                    utf8Evidence[keyBytes] = valueBytes;
                }
                
                result.Add(utf8Evidence);
            }
            
            stopwatch.Stop();
            Console.WriteLine($"UTF-8 byte array preprocessing took {stopwatch.ElapsedMilliseconds}ms for {evidenceList.Count} records");
            
            return result;
        }
        
    }
}