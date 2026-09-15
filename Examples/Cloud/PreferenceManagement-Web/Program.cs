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

namespace FiftyOne.Examples.Cloud.PreferenceManagementWeb
{
    /// <summary>
    /// A website whose pages carry the 51Degrees Preference Management
    /// Platform and the 51Degrees client script in every arrangement the
    /// shared browser tests check.
    /// <para>
    /// Everything the tests rely on is in plain files under wwwroot, being
    /// three scripts in wwwroot/js and one HTML template per page in
    /// wwwroot/templates. This program does only two things, which are
    /// serving wwwroot as it is and answering /cloud/{page} with
    /// wwwroot/templates/cloud/{page}.html once its placeholders are
    /// filled. Another language's copy of this demo copies wwwroot and does
    /// the same two things. See README.md for the rules.
    /// </para>
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // Start the server and then wait for the task to finish.
            Run(args).Wait();
        }

        /// <summary>
        /// Used by tests to run the example in the same way a developer
        /// does. Returns the task the web server runs in, so a test can
        /// cancel the token and wait for the server to stop.
        /// </summary>
        public static Task Run(
            string[] args,
            CancellationToken stopToken = default)
        {
            // Read before anything starts, so a missing resource key stops
            // the demo with a message rather than serving broken pages.
            var settings = Settings.FromEnvironment();

            // The content root is the build output, where wwwroot is copied,
            // so the pages are found whichever directory the demo is started
            // from. The address to listen on comes from ASPNETCORE_URLS, the
            // usual way of starting any ASP.NET Core application.
            var builder = WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    Args = args,
                    ContentRootPath = AppContext.BaseDirectory
                });
            var app = builder.Build();
            var files = app.Environment.WebRootFileProvider;

            // Every host name is answered, because the tests open the same
            // page as two different sites on one port. AllowedHosts in
            // appsettings.json is "*" for the same reason.
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                    Pages.NoStore(context.Context.Response)
            });

            // The pages that load both the platform and the client script
            // straight from the cloud.
            var cloud = settings.CloudPlaceholders();
            app.MapGet(
                "/cloud/{**page}",
                (HttpContext context, string page) =>
                    Pages.Serve(context, files, "cloud", page, cloud));

            // Names only, never the values.
            Console.WriteLine(
                $"The resource key is read from " +
                $"'{settings.ResourceKeyVariable}'. The cloud is " +
                $"{settings.CloudEndpointVariable}.");

            return app.RunAsync(stopToken);
        }
    }
}
