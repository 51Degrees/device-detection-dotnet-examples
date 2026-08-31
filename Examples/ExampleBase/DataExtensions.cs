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

using FiftyOne.Pipeline.Core.Data;
using FiftyOne.Pipeline.Engines;
using FiftyOne.Pipeline.Engines.Data;
using System;
using System.Collections.Generic;

namespace FiftyOne.DeviceDetection.Examples
{
    public static class DataExtensions
    {
        /// <summary>
        /// Execute the specified function on the supplied <see cref="IElementData"/> instance.
        /// If a <see cref="PropertyMissingException"/> occurs then the resulting string will
        /// contain 'Unknown' + the message from the exception.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        public static string TryGetValue<T>(this T data, Func<T, string> function)
            where T : IElementData
        {
            string result;
            try
            {
                result = function(data);
            }
            catch (PropertyMissingException pex)
            {
                result = pex.Message;
            }
            return result;
        }

        /// <summary>
        /// Get a human-readable version of the specified <see cref="IAspectPropertyValue"/>.
        /// If no value has be set, the result will be 'Unknown' + the 
        /// <see cref="IAspectPropertyValue.NoValueMessage"/>.
        /// </summary>
        /// <param name="apv"></param>
        /// <returns></returns>
        public static string GetHumanReadable(this IAspectPropertyValue<string> apv)
        {
            return apv != null && apv.HasValue ? apv.Value : NoValue(apv);
        }
        public static string GetHumanReadable(this IAspectPropertyValue<IReadOnlyList<string>> apv)
        {
            return apv != null && apv.HasValue ? string.Join(", ", apv.Value) : NoValue(apv);
        }
        public static string GetHumanReadable(this IAspectPropertyValue<int> apv)
        {
            return apv != null && apv.HasValue ? apv.Value.ToString() : NoValue(apv);
        }

        /// <summary>
        /// Get a human-readable version of a property looked up by name rather than
        /// through the strongly typed <see cref="IDeviceData"/> interface. A property
        /// is present in the data file and in the cloud response before the generated
        /// interface is rebuilt to include it, so this is the only way for an example
        /// to display a newly added property. Behaves like
        /// <see cref="GetHumanReadable(IAspectPropertyValue{string})"/>, returning
        /// 'No value' and a reason when the property is absent or has no value.
        /// </summary>
        /// <param name="data">
        /// The element data to read the property from.
        /// </param>
        /// <param name="propertyName">
        /// Name of the property, for example 'IsHeadless'. Matching is case
        /// insensitive because the dictionary keys differ in case between the
        /// on-premise and cloud engines.
        /// </param>
        public static string GetHumanReadableByName<T>(
            this T data,
            string propertyName)
            where T : IElementData
        {
            object raw = null;
            var found = false;
            foreach (var entry in data.AsDictionary())
            {
                if (string.Equals(entry.Key, propertyName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    raw = entry.Value;
                    found = true;
                    break;
                }
            }
            if (found == false || raw == null)
            {
                return "No value (property missing from the resource key or " +
                    "data file)";
            }
            if (raw is IAspectPropertyValue apv)
            {
                return apv.HasValue ? apv.Value.ToString() :
                    $"No value ({apv.NoValueMessage})";
            }
            return raw.ToString();
        }

        /// <summary>
        /// Build the 'Unknown' message for a property that has no value. Handles the
        /// case where the property is entirely absent from the response (a null
        /// <see cref="IAspectPropertyValue{T}"/>), which happens when the resource key
        /// or data file does not include the property.
        /// </summary>
        private static string NoValue<T>(IAspectPropertyValue<T> apv)
        {
            return $"No value ({(apv == null ? "property missing from the resource key or data file" : apv.NoValueMessage)})";
        }
    }
}
