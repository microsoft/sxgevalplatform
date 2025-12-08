namespace SxG.EvalPlatform.Plugins.Helpers
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Services;
    using Newtonsoft.Json;

    /// <summary>
    /// Helper class for integrating with Power Automate flows for file operations
    /// </summary>
    public static class PowerAutomateFlowHelper
    {
        /// <summary>
        /// Model for Power Automate flow trigger payload
        /// </summary>
        public class FlowTriggerPayload
        {
            public string EvalRunId { get; set; }
            public string FileName { get; set; }
            public string FileContent { get; set; } // Base64 encoded
            public string FileAttributeName { get; set; }
            public string EntityLogicalName { get; set; }
        }

        /// <summary>
        /// Triggers a Power Automate flow to handle file upload to Dataverse file column
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="fileName">File name</param>
        /// <param name="fileContent">File content as byte array</param>
        /// <param name="fileAttributeName">Dataverse file column name</param>
        /// <param name="entityLogicalName">Entity logical name</param>
        /// <param name="flowUrl">Power Automate flow trigger URL</param>
        /// <param name="loggingService">Logging service</param>
        /// <returns>True if flow triggered successfully</returns>
        public static bool TriggerFileUploadFlow(
            string evalRunId,
            string fileName,
            byte[] fileContent,
            string fileAttributeName,
            string entityLogicalName,
            string flowUrl,
            IPluginLoggingService loggingService)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                loggingService.Trace($"PowerAutomateFlowHelper: Starting file upload flow trigger");
                loggingService.Trace($"PowerAutomateFlowHelper: Flow URL: {flowUrl}");
                loggingService.Trace($"PowerAutomateFlowHelper: File name: {fileName}, File size: {fileContent?.Length ?? 0} bytes");

                var payload = new FlowTriggerPayload
                {
                    EvalRunId = evalRunId,
                    FileName = fileName,
                    FileContent = Convert.ToBase64String(fileContent),
                    FileAttributeName = fileAttributeName,
                    EntityLogicalName = entityLogicalName
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);
                
                // Log the complete payload (truncate FileContent for readability)
                var payloadForLogging = new FlowTriggerPayload
                {
                    EvalRunId = payload.EvalRunId,
                    FileName = payload.FileName,
                    FileContent = payload.FileContent?.Length > 100 
                        ? $"{payload.FileContent.Substring(0, 100)}... [truncated, full length: {payload.FileContent.Length} chars]"
                        : payload.FileContent,
                    FileAttributeName = payload.FileAttributeName,
                    EntityLogicalName = payload.EntityLogicalName
                };
                string payloadForLoggingJson = JsonConvert.SerializeObject(payloadForLogging, Formatting.Indented);
                loggingService.Trace($"PowerAutomateFlowHelper: Request payload (FileContent truncated for logging):\n{payloadForLoggingJson}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(flowUrl);
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = 30000; // 30 seconds for flow trigger

                byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
                httpWebRequest.ContentLength = data.Length;

                loggingService.Trace($"PowerAutomateFlowHelper: Sending POST request to flow, payload size: {data.Length} bytes");

                using (Stream requestStream = httpWebRequest.GetRequestStream())
                {
                    requestStream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    var duration = DateTimeOffset.UtcNow - startTime;
                    bool success = httpWebResponse.StatusCode == HttpStatusCode.OK || 
                                   httpWebResponse.StatusCode == HttpStatusCode.Accepted;

                    loggingService.LogDependency("PowerAutomate", flowUrl, startTime, duration, success);

                    if (success)
                    {
                        loggingService.Trace($"PowerAutomateFlowHelper: Successfully triggered flow for file upload, Response status: {httpWebResponse.StatusCode}");
                        return true;
                    }
                    else
                    {
                        loggingService.Trace($"PowerAutomateFlowHelper: Flow trigger returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("PowerAutomate", "FileUploadFlow", startTime, duration, false);
                loggingService.LogException(webEx, $"PowerAutomateFlowHelper: WebException triggering flow. Flow URL: {flowUrl}");
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("PowerAutomate", "FileUploadFlow", startTime, duration, false);
                loggingService.LogException(ex, $"PowerAutomateFlowHelper: Exception triggering flow. Flow URL: {flowUrl}");
                return false;
            }
        }
    }
}