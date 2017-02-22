using System;
using System.Text;

static readonly string _callbackUrl = Environment.GetEnvironmentVariable("WF_CallbackUrl");

public static void Run(string publishLocatorMsg, TraceWriter log)
{
    log.Info($"ProcessPublishingMessage: {publishLocatorMsg}");

    if(!string.IsNullOrWhiteSpace(_callbackUrl)) {

        // Post to the Callback Url
        var httpClient = new HttpClient();
        var content = new StringContent(publishLocatorMsg, Encoding.UTF8, "application/json");
        var result = httpClient.PostAsync(_callbackUrl, content).Result;

        if(result.IsSuccessStatusCode)
        {
            log.Info($"ProcessPublishMessage Callback to {_callbackUrl} successful: {result.StatusCode.ToString()} {result.ReasonPhrase}");
        }
        else {
            log.Error($"ProcessPublishMessage Callback to {_callbackUrl} failed: {result.StatusCode.ToString()} {result.ReasonPhrase}");
        }
    }
    else {
        log.Warning($"Callback URL is empty.");
    }
}