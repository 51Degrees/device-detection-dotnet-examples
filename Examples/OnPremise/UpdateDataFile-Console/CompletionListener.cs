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

using FiftyOne.Pipeline.Engines.Services;
using System;
using System.Threading.Tasks;

namespace FiftyOne.DeviceDetection.Examples.OnPremise.UpdateDataFile
{
    /// <summary>
    /// Helper class used to wait for data updates to complete and return the status of the update.
    /// </summary>
    public class CompletionListener
    {
        public DataUpdateCompleteArgs Result { get; private set; }
        public bool Complete { get; private set; }

        public CompletionListener(IDataUpdateService dataUpdateService)
        {
            dataUpdateService.CheckForUpdateComplete += DataUpdateService_CheckForUpdateComplete;
        }

        private void DataUpdateService_CheckForUpdateComplete(object sender, DataUpdateCompleteArgs e)
        {
            Result = e;
            Complete = true;
        }

        /// <summary>
        /// Blocks the calling thread until the data update is complete.
        /// Note - an update may have occurred before calling this method. Call <see cref="Reset"/>
        /// to wait for the *next* update.
        /// </summary>
        /// <seealso cref="Reset"/>
        /// <param name="timeout">
        /// The maximum time to wait.
        /// </param>
        /// <exception cref="TimeoutException">
        /// Thrown if the update does not complete before the timeout expires.
        /// </exception>
        public void WaitForComplete(TimeSpan timeout)
        {
            DateTime start = DateTime.UtcNow;
            while(Complete == false &&
                start.Add(timeout) > DateTime.UtcNow)
            {
                Task.Delay(100).Wait();
            }
            if(Complete == false)
            {
                throw new TimeoutException("Timed out waiting for data update to complete");
            }
        }

        /// <summary>
        /// Clear the 'complete' flag. This will cause the <see cref="WaitForComplete(TimeSpan)"/>
        /// method to block until a future data update completes.
        /// </summary>
        /// <seealso cref="WaitForComplete(TimeSpan)"/>
        public void Reset()
        {
            Complete = false;
            Result = null;
        }
    }
}
