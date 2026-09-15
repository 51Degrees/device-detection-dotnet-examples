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

using Microsoft.Extensions.FileProviders;
using System.Net;
using System.Text.RegularExpressions;

namespace FiftyOne.Examples.Cloud.PmpWeb
{
    /// <summary>
    /// Fills a page template's placeholders and serves the result.
    /// </summary>
    public static class Pages
    {
        /// <summary>
        /// What a page name may be, which is lower case words joined by
        /// hyphens, one or more deep. Nothing else reaches the file system.
        /// </summary>
        private static readonly Regex PageName =
            new Regex("^[a-z0-9-]+(/[a-z0-9-]+)*$");

        /// <summary>A placeholder, being an upper case name in double
        /// braces.</summary>
        private static readonly Regex Placeholder =
            new Regex(@"\{\{([A-Z_]+)\}\}");

        /// <summary>
        /// Answers with wwwroot/templates/{mode}/{page}.html once its
        /// placeholders are filled, or 404 where there is no such page.
        /// </summary>
        public static async Task Serve(
            HttpContext context,
            IFileProvider files,
            string mode,
            string page,
            IReadOnlyDictionary<string, string> values)
        {
            var file = PageName.IsMatch(page)
                ? files.GetFileInfo($"templates/{mode}/{page}.html")
                : null;
            if (file == null || file.Exists == false || file.IsDirectory)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            string template;
            using (var reader = new StreamReader(file.CreateReadStream()))
            {
                template = await reader.ReadToEndAsync();
            }

            var html = Fill(template, values);
            NoStore(context.Response);
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html);
        }

        /// <summary>
        /// Replaces every placeholder with its value, encoded for an HTML
        /// attribute.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The template uses a placeholder there is no value for, or still
        /// holds double braces once every known placeholder is filled. A
        /// page is never served half filled, because a test reading it
        /// would fail somewhere far from the cause.
        /// </exception>
        public static string Fill(
            string template,
            IReadOnlyDictionary<string, string> values)
        {
            var html = Placeholder.Replace(template, match =>
                values.TryGetValue(match.Groups[1].Value, out var value)
                    ? WebUtility.HtmlEncode(value)
                    : throw new InvalidOperationException(
                        $"The template uses {match.Value}, which is not " +
                        "a placeholder this demo fills."));
            if (html.Contains("{{"))
            {
                throw new InvalidOperationException(
                    "The template still holds '{{' once every " +
                    "placeholder is filled, so one is misspelt.");
            }
            return html;
        }

        /// <summary>
        /// Stops the browser keeping a copy, so a changed page or script is
        /// what the next test loads.
        /// </summary>
        public static void NoStore(HttpResponse response)
        {
            response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";
        }
    }
}
