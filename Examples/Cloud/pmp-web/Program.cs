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

using FiftyOne.DeviceDetection.Cloud.FlowElements;
using FiftyOne.Did.Cloud.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;

namespace FiftyOne.Examples.Cloud.PmpWeb
{
    /// <summary>
    /// A website whose pages carry the 51Degrees Preference Management
    /// Platform and the 51Degrees client script in every arrangement the
    /// shared browser tests check.
    /// <para>
    /// Everything the tests rely on is in plain files under wwwroot, being
    /// three scripts in wwwroot/js and one HTML template per page in
    /// wwwroot/templates. This program serves wwwroot as it is, answers
    /// /cloud/{page} and /pipeline/{page} with
    /// wwwroot/templates/{mode}/{page}.html once its placeholders are
    /// filled, and runs the 51Degrees Pipeline for the pipeline pages, so
    /// that the pipeline serves their client script. Another language's
    /// copy of this demo copies wwwroot and does the same. See README.md
    /// for the rules.
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

            // The 51Degrees Pipeline for the pipeline pages. The elements
            // are listed under PipelineOptions in appsettings.json, and the
            // resource key and the cloud come from the same environment
            // variables the pages are filled from. The web integration adds
            // the JSON and JavaScript builders that serve the client script.
            builder.Configuration.AddInMemoryCollection(
                settings.PipelineConfiguration());
            builder.Services.AddSingleton<CloudRequestEngineBuilder>();
            builder.Services.AddSingleton<DeviceDetectionCloudEngineBuilder>();
            builder.Services.AddSingleton<DidCloudEngineBuilder>();
            builder.Services.AddFiftyOne(builder.Configuration);

            var app = builder.Build();
            var files = app.Environment.WebRootFileProvider;

            // The pipeline runs only for the pipeline pages and the two
            // requests their client script makes, being the script itself
            // and the JSON it posts evidence to, so a page under /cloud/
            // never makes this server call the cloud as well.
            app.UseWhen(
                context => IsForThePipeline(context.Request.Path),
                pipeline => pipeline.UseFiftyOne());

            // Every host name is answered, because the tests open the same
            // page as two different sites on one port. AllowedHosts in
            // appsettings.json is "*" for the same reason.
            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                    Pages.NoStore(context.Context.Response)
            });

            // The pages that load both the PMP and the client script
            // straight from the cloud.
            var cloud = settings.CloudPlaceholders();
            app.MapGet(
                "/cloud/{**page}",
                (HttpContext context, string page) =>
                    Pages.Serve(context, files, "cloud", page, cloud));

            // The same pages with the client script served by this demo's
            // own pipeline rather than by the cloud. The PMP still loads
            // from the cloud.
            var pipelinePages = settings.PipelinePlaceholders();
            app.MapGet(
                "/pipeline/{**page}",
                (HttpContext context, string page) =>
                    Pages.Serve(
                        context, files, "pipeline", page, pipelinePages));

            // Names only, never the values.
            Console.WriteLine(
                $"The resource key is read from " +
                $"'{settings.ResourceKeyVariable}'. The cloud is " +
                $"{settings.CloudEndpointVariable}.");

            return app.RunAsync(stopToken);
        }

        /// <summary>
        /// Whether a request is one the 51Degrees Pipeline handles, being
        /// a pipeline page, the client script those pages load, or the
        /// JSON that script posts its evidence to.
        /// </summary>
        private static bool IsForThePipeline(PathString path) =>
            path.StartsWithSegments("/pipeline") ||
            path.Equals(
                Settings.PIPELINE_CLIENT_SCRIPT_PATH,
                StringComparison.OrdinalIgnoreCase) ||
            path.Equals(
                FiftyOne.Pipeline.Engines.Constants.DEFAULT_JSON_ENDPOINT,
                StringComparison.OrdinalIgnoreCase);
    }
}
