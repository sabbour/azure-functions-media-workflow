using System;
using System.Text;

static readonly string _callbackUrl = Environment.GetEnvironmentVariable("WF_CallbackUrl");

public static void Run(string publishLocatorMsg, TraceWriter log)
{
    log.Info($"ProcessPublishingMessage: {publishLocatorMsg}");

    // Post to the Callback Url
    var httpClient = new HttpClient();
    var content = new StringContent(publishLocatorMsg, Encoding.UTF8, "application/json");
    var result = httpClient.PostAsync(_callbackUrl, content).Result;
}