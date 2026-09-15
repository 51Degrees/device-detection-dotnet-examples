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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace FiftyOne.DeviceDetection.Example.Tests.Shared
{
    /// <summary>
    /// Stops any browser driver, and the browsers it started, that is still
    /// running when a Selenium test assembly finishes. On Windows a process
    /// left behind keeps the output of 'dotnet test' open, so the command
    /// never returns and the CI job runs until it times out. The drivers
    /// should already have been quit by the tests; this makes sure a leak in
    /// one assembly cannot hang the run.
    /// </summary>
    [TestClass]
    public class SeleniumProcessCleanup
    {
        private static readonly string[] Drivers =
        {
            "chromedriver",
            "msedgedriver",
            "geckodriver",
        };

        private static readonly string[] Browsers =
        {
            "chrome",
            "msedge",
            "firefox",
        };

        [AssemblyCleanup]
        public static void StopLeftoverBrowsers()
        {
            // Only processes started after this test host are touched, so a
            // developer's own browser is never closed. Browsers themselves are
            // only stopped on CI, where nobody else is using one.
            var since = Process.GetCurrentProcess().StartTime;
            Stop(Drivers, since);
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
            {
                Stop(Browsers, since);
            }
        }

        private static void Stop(string[] names, DateTime since)
        {
            foreach (var name in names)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        try
                        {
                            if (process.StartTime >= since)
                            {
                                process.Kill(entireProcessTree: true);
                            }
                        }
                        catch (Exception exception) when (
                            exception is InvalidOperationException ||
                            exception is Win32Exception ||
                            exception is NotSupportedException)
                        {
                            // The process exited already, or belongs to
                            // another user. Neither is ours to stop.
                        }
                    }
                }
            }
        }
    }
}
