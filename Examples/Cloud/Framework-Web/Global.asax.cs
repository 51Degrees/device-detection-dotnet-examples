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

using FiftyOne.DeviceDetection.Cloud.FlowElements;
using FiftyOne.Pipeline.CloudRequestEngine.FlowElements;
using FiftyOne.Pipeline.Engines.FiftyOne.FlowElements;
using FiftyOne.Pipeline.JavaScriptBuilder.FlowElement;
using FiftyOne.Pipeline.JsonBuilder.FlowElement;
using System;
using System.Web;
using FiftyOne.DeviceDetection.Uach;

namespace Framework_Web
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Make sure the assemblies that are needed by the pipeline are loaded into the
            // app domain.
            // This is needed in order from BuildFromConfiguration to be able to find the
            // relevant builder types when using reflection.
            AppDomain.CurrentDomain.Load(typeof(UachJsConversionElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(CloudRequestEngine).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(DeviceDetectionCloudEngine).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(JavaScriptBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(JsonBuilderElement).Assembly.GetName());
            AppDomain.CurrentDomain.Load(typeof(SequenceElementBuilder).Assembly.GetName());
        }
    }
}