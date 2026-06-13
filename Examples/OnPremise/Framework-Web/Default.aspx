<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Framework_Web.Default" %>

<%@ Import Namespace="FiftyOne.DeviceDetection" %>
<%@ Import Namespace="FiftyOne.DeviceDetection.Examples" %>
<%@ Import Namespace="FiftyOne.DeviceDetection.Hash.Engine.OnPremise.FlowElements" %>
<%@ Import Namespace="FiftyOne.Pipeline.Web.Framework.Providers" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>51Degrees Example</title>

    <link rel="stylesheet" href="Content/examples-main.min.css" />

    <%-- This JavaScript is dynamically generated based on the details of the detected device.
        Including a reference to it allows us to collect client side evidence
        (used for things such as Apple model detection) and access device detection
        results in client side code.
        Note that there doesn't need to be a physical file. The 51Degrees Pipeline will
        intercept the request and serve it automatically.
        --%>
    <script async src='51Degrees.core.js' type='text/javascript'></script>
</head>
<body>
    <div class="c-eg-page">
        <h2 class="c-eg-page__title">Web integration example</h2>

        <p class="c-eg-page__lead">
            This example demonstrates the use of the Pipeline API to perform device detection within a
            simple ASP.NET web project. In particular, it highlights:
        </p>
        <ol>
            <li>
                Automatic handling of the 'Accept-CH' header, which is used to request User-Agent
                Client Hints from the browser
            </li>
            <li>
                Client-side evidence collection in order to identify Apple device models and properties
                such as screen size.
            </li>
        </ol>

        <h3 class="c-eg-page__heading">Client Hints</h3>
        <p class="c-eg-page__lead">
            When the first request is made, browsers that support client hints will typically send a subset
            of client hints values along with the User-Agent header.
            If device detection determines that the browser does support client hints then it will request
            that additional client hints headers are sent with future requests by sending the Accept-CH
            header with the response.
        </p>
        <p class="c-eg-page__lead">
            Note that if you have visited this page previously, the value of Accept-CH will have been
            cached so all requested client hints headers will be sent on the first request. Using features
            such as 'private browsing' or 'incognito mode' will allow you to see the true first request
            experience as the previous Accept-CH value will not be used.
        </p>

        <noscript>
            <div class="c-eg-alert">
                WARNING: JavaScript is disabled in your browser. This means that the callback discussed
                further down this page will not fire and UACH headers will not be sent.
            </div>
        </noscript>

        <div id="content">
            <h3 class="c-eg-page__heading">Detection results</h3>
            <p class="c-eg-page__lead">
                The following values are determined by server-side device detection
                on the first request.
            </p>
            <p class="c-eg-page__lead">
                Note that all values below are retrieved using the strongly typed approach,
                which is new for version 4. In order to provide easier migration for sites using
                version 3 of this API, you can also access some properties from the
                HttpBrowserCapabilities object. For example, is this site being accessed with
                a mobile device? <strong><%= Request.Browser.IsMobileDevice ? "Yes" : "No" %></strong></p>
            <table class="c-eg-table">
                <thead class="c-eg-table__head">
                    <tr class="c-eg-table__row">
                        <th class="c-eg-table__cell">Key</th>
                        <th class="c-eg-table__cell">Value</th>
                    </tr>
                </thead>
                <tbody>
                <%
                    // Put the flow data and device data instances into local variables so we don't
                    // have to keep grabbing them.
                    var flowData = ((PipelineCapabilities)Request.Browser).FlowData;
                    var deviceData = flowData.Get<IDeviceData>();
                    // Get the engine that is used to make requests to the cloud service.
                    var engine = flowData.Pipeline.GetElement<DeviceDetectionHashEngine>();
                    // Note that below we are using some helper methods from the
                    // FiftyOne.DeviceDeteciton.Examples project (TryGetValue and GetHumanReadable)
                    // These are mostly intended to handle scenarios where device detection does
                    // not have an answer.
                    // In a production scenario, you will probably want to handle these scenarios
                    // differently. Feel free to copy these helpers if they are useful though.
                %>
                <tr class="c-eg-table__row c-eg-table__row--alt"><td class="c-eg-table__cell c-eg-table__cell--key">Hardware Vendor:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.HardwareVendor.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row"><td class="c-eg-table__cell c-eg-table__cell--key">Hardware Name:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.HardwareName.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row c-eg-table__row--alt"><td class="c-eg-table__cell c-eg-table__cell--key">Device Type:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.DeviceType.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row"><td class="c-eg-table__cell c-eg-table__cell--key">Platform Vendor:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.PlatformVendor.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row c-eg-table__row--alt"><td class="c-eg-table__cell c-eg-table__cell--key">Platform Name:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.PlatformName.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row"><td class="c-eg-table__cell c-eg-table__cell--key">Platform Version:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.PlatformVersion.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row c-eg-table__row--alt"><td class="c-eg-table__cell c-eg-table__cell--key">Browser Vendor:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.BrowserVendor.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row"><td class="c-eg-table__cell c-eg-table__cell--key">Browser Name:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.BrowserName.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row c-eg-table__row--alt"><td class="c-eg-table__cell c-eg-table__cell--key">Browser Version:</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.BrowserVersion.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row"><td class="c-eg-table__cell c-eg-table__cell--key">Screen width (pixels):</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.ScreenPixelsWidth.GetHumanReadable()) %></td></tr>
                <tr class="c-eg-table__row c-eg-table__row--alt"><td class="c-eg-table__cell c-eg-table__cell--key">Screen height (pixels):</td><td class="c-eg-table__cell"> <%= deviceData.TryGetValue(d => d.ScreenPixelsHeight.GetHumanReadable()) %></td></tr>
                </tbody>
            </table>

            <div id="evidence">
                <h3 class="c-eg-page__heading">Evidence used</h3>
                <p class="c-eg-legend">
                    Evidence was
                    <span class="c-eg-legend__swatch c-eg-legend__swatch--used">used</span>
                    /
                    <span class="c-eg-legend__swatch c-eg-legend__swatch--present">present</span>
                    for detection
                </p>
                <table class="c-eg-table">
                    <thead class="c-eg-table__head">
                        <tr class="c-eg-table__row">
                            <th class="c-eg-table__cell">Key</th>
                            <th class="c-eg-table__cell">Value</th>
                        </tr>
                    </thead>
                    <tbody>
                    <% foreach (var evidence in flowData.GetEvidence().AsDictionary()) { %>
                        <tr class="c-eg-table__row <%= engine.EvidenceKeyFilter.Include(evidence.Key) ? "c-eg-table__row--used" : "c-eg-table__row--present" %>">
                            <td class="c-eg-table__cell c-eg-table__cell--key"><%= evidence.Key %></td>
                            <td class="c-eg-table__cell"><%= evidence.Value %></td>
                        </tr>
                    <% } %>
                    </tbody>
                </table>
            </div>

            <div id="response-headers">
                <h3 class="c-eg-page__heading">Response headers</h3>
                <table class="c-eg-table">
                    <thead class="c-eg-table__head">
                        <tr class="c-eg-table__row">
                            <th class="c-eg-table__cell">Key</th>
                            <th class="c-eg-table__cell">Value</th>
                        </tr>
                    </thead>
                    <tbody>
                    <% foreach (var key in Response.Headers.AllKeys) { %>
                        <tr class="c-eg-table__row c-eg-table__row--present">
                            <td class="c-eg-table__cell c-eg-table__cell--key"><%= key %></td>
                            <td class="c-eg-table__cell"><%= string.Join(", ", Response.Headers.GetValues(key)) %></td>
                        </tr>
                    <% } %>
                    </tbody>
                </table>
            </div>

            <% if (Response.Headers.AllKeys.Contains("Accept-CH") == false) { %>
                <div class="c-eg-alert">
                    WARNING: There is no Accept-CH header in the response. This may indicate that your
                    browser does not support User-Agent Client Hints. This is not necessarily a problem,
                    but if you are wanting to try out detection using User-Agent Client Hints, then make
                    sure that your browser
                    <a href="https://developer.mozilla.org/en-US/docs/Web/API/User-Agent_Client_Hints_API#browser_compatibility">supports them</a>.
                </div>
            <% } %>

            <h3 class="c-eg-page__heading">Client-side evidence and Apple models</h3>
            <p class="c-eg-page__lead">
                The information shown below is determined after a callback is made to the server with
                additional evidence that is gathered by JavaScript running on the client-side.
                The callback will also include any additional client hints headers that have been requested.
            </p>
            <p class="c-eg-page__lead">
                When an Apple device is used, the results from
                the first request above will show all Apple models because the server cannot tell the
                exact model of the device. In contrast, the results from the callback below will show
                a smaller set of possible models.
                This can be tested to some extent using most emulators, such as those in the
                'developer tools' menu in Google Chrome. However, these are not the identical to real
                devices so this can cause some unusual results. Using real devices will generally be more
                successful.
            </p>
            <p class="c-eg-page__lead">
                If you want to work with Apple Model or other client-side information, such as screen
                width/height on the server, it will be available on the next request.
                This is achieved by storing the additional client-side evidence as cookies on the client.
                When a future page is requested, these cookies will be included with the request and the
                device detection API will include them when working out the details of the device.
                Refreshing this page can be used to show this in action. Any values that are unique to the
                client-side values below will appear in the evidence values used and server-side results
                after the refresh.
            </p>
            <% if (engine.DataSourceTier == "Lite") { %>
                <div class="c-eg-alert">
                    WARNING: You are using the free 'Lite' data file. This does not include the client-side
                    evidence capabilities of the paid-for data file, so you will not see any additional
                    data below. Find out about the Enterprise data file on our
                    <a href="https://51degrees.com/pricing">pricing page</a>.
                </div>
            <% } %>
        </div>

        <% if (engine.DataSourceTier == "Lite") { %>
            <div class="c-eg-message">
              <p class="c-eg-message__text">Need more on-premise properties and features? <a href="https://51degrees.com/contact-us">Contact us</a> to explore the options.</p>
              <a class="b-btn c-eg-message__cta" href="https://51degrees.com/contact-us">Contact us</a>
            </div>
        <% } %>
    </div>

    <%-- The shared examples.js helper subscribes to the 51Degrees.core.js 'complete'
        event and appends a results table into #content using the pattern-library classes. --%>
    <script src="Content/examples.min.js"></script>
    <script>
        window.onload = function () {
            fodExamples.bindDeviceCallback({ targetId: "content" });
        }
    </script>
</body>
</html>
