#load "../Shared/EncodingJobMessage.csx"
#load "../Shared/PublishedLocatorMessage.csx"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/storageHelpers.csx"

using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.MediaServices.Client.DynamicEncryption;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

// Read settings from Environment Variables, which are defined in the Application Settings
static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("WF_MediaServicesAccountName");
static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("WF_MediaServicesAccountKey");
static readonly string _storageAccountName = Environment.GetEnvironmentVariable("WF_StorageAccountName");
static readonly string _storageAccountKey = Environment.GetEnvironmentVariable("WF_StorageAccountKey");
static readonly string _notificationQueueName = "mwfpublishqueue";

// Media Services Credentials and Cloud Media Context fields
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;

// Queue to send publish messages to
private static CloudQueue _queue = null;

public static void Run(EncodingJobMessage encodingJobMsg, out PublishedLocatorMessage publishLocatorMsg, TraceWriter log)
{
    log.Info($"EventType: {encodingJobMsg.EventType}");
    log.Info($"MessageVersion: {encodingJobMsg.MessageVersion}");
    log.Info($"ETag: {encodingJobMsg.ETag}");
    log.Info($"TimeStamp: {encodingJobMsg.TimeStamp}");

    // Message
    publishLocatorMsg = null;

    // We are only interested in messages where EventType is "JobStateChange"
    if (encodingJobMsg.EventType == "JobStateChange")
    {
        string jobId = (string) encodingJobMsg.Properties.Where(j => j.Key == "JobId").FirstOrDefault().Value;
        string newJobStateStr = (string) encodingJobMsg.Properties.Where(j => j.Key == "NewState").FirstOrDefault().Value;
        JobState jobState = (JobState) Enum.Parse(typeof(JobState), newJobStateStr);

        log.Info($"Job {jobId} state {jobState}");
        

        if (jobState == JobState.Finished) {
            log.Info($"Job {jobId} is complete.");

            // Create and cache the Media Services credentials in a static class variable
            _cachedCredentials = new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey);
            
            // Used the chached credentials to create CloudMediaContext
            _context = new CloudMediaContext(_cachedCredentials);
            
            // Create the queue that will be receiving the notification messages
            _queue = CreateQueue(_storageAccountName, _storageAccountKey, _notificationQueueName);

            // Get the Asset related to the job
            var job = _context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();
            var outputAsset = job.OutputMediaAssets[0];
            
            // Create a 10 year read only streaming policy
            var policy = _context.AccessPolicies.Create("Streaming policy", TimeSpan.FromDays(36500), AccessPermissions.Read);

            // Create a locator to the streaming content on an origin.
            var originLocator = _context.Locators.CreateLocator(LocatorType.OnDemandOrigin, outputAsset, policy, DateTime.UtcNow.AddMinutes(-5));
    
            // Get a valid on-demand URL using the helper
            var publishurl = GetValidOnDemandURI(outputAsset);            
            log.Info($"Publish URL: {publishurl.ToString()}");


            if (originLocator != null && publishurl != null)
            {
                string smoothUrl = publishurl.ToString();

                // Put on the queue
                publishLocatorMsg= new PublishedLocatorMessage {
                    AssetId = outputAsset.Id,
                    Smooth = smoothUrl,
                    HLSv3 = $"{smoothUrl}(format=m3u8-aapl-v3)",
                    HLSv4 = $"{smoothUrl}(format=m3u8-aapl)",
                    DASH = $"{smoothUrl}(format=mpd-time-csf)" 
                }; 

                log.Info($"Set queue output {publishLocatorMsg}");
            }
        }
        else if (jobState == JobState.Error)
        {
            log.Info($"Job {jobId} failed with an error.");
        }
    }
}