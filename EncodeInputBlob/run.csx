#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
#load "../Shared/storageHelpers.csx"
#load "../Shared/mediaServicesHelpers.csx"
using Microsoft.WindowsAzure.MediaServices.Client; 
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

// Read settings from Environment Variables, which are defined in the Application Settings
static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("WF_MediaServicesAccountName");
static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("WF_MediaServicesAccountKey");
static readonly string _storageAccountName = Environment.GetEnvironmentVariable("WF_StorageAccountName");
static readonly string _storageAccountKey = Environment.GetEnvironmentVariable("WF_StorageAccountKey");
static readonly string _encodingPreset = Environment.GetEnvironmentVariable("WF_PresetName");
static readonly string _notificationQueueName = "mwfprogressqueue";

// Media Services Credentials and Cloud Media Context fields
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;

// Queue and notification end point for tracking job progress
private static CloudQueue _queue = null;
private static INotificationEndPoint _notificationEndPoint = null;

public static void Run(CloudBlockBlob inputBlob, string fileName, string fileExtension, TraceWriter log)
{
    log.Info($"EncodeInputBlob function triggered: {fileName}.{fileExtension}");
    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");
    try
    {
        // Create and cache the Media Services credentials in a static class variable
        _cachedCredentials = new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey);
        
        // Used the chached credentials to create CloudMediaContext
        _context = new CloudMediaContext(_cachedCredentials);

        // Create the queue that will be receiving the notification messages
         _queue = CreateQueue(_storageAccountName, _storageAccountKey, _notificationQueueName);

        // Check for existing Notification Endpoint with the name "FunctionWebHook"
        var existingEndpoint = _context.NotificationEndPoints.Where(e=>e.Name == _notificationQueueName).FirstOrDefault();

        if (existingEndpoint != null){
            log.Info ($"Notification endpoint {_notificationQueueName} already exists");
            _notificationEndPoint = (INotificationEndPoint) existingEndpoint;
        }
        else {
            // Create the notification point that is mapped to the queue.
            _notificationEndPoint = _context.NotificationEndPoints.Create(_notificationQueueName, NotificationEndPointType.AzureQueue, _notificationQueueName);
            log.Info($"Notification Endpoint {_notificationQueueName} created");
        }

        // Step 1: Create asset from the input blob
        IAsset newAsset = CreateAssetFromBlobDeleteOriginal(inputBlob, $"{fileName} input", log).GetAwaiter().GetResult();

        // Step 2: Create the encoding job
        // Declare a new encoding job with the Standard encoder
        IJob job = _context.Jobs.Create($"{fileName} Azure Function workflow job");

        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

        // Create a task with the encoding details, using a string preset defined in the settings
        ITask task = job.Tasks.AddNew($"{fileName} encoding task - {_encodingPreset}", processor, _encodingPreset, TaskOptions.None);

        // Specify the input asset to be encoded
        task.InputAssets.Add(newAsset);

        // Add an output asset to contain the results of the job
        // This output is specified as AssetCreationOptions.None, which means the output asset is not encrypted
        task.OutputAssets.AddNew($"{fileName} output - {_encodingPreset}", AssetCreationOptions.None);

        // Add a notification point to the job. You can add multiple notification points 
        job.JobNotificationSubscriptions.AddNew(NotificationJobState.FinalStatesOnly, _notificationEndPoint);

        job.Submit();
        log.Info("Job Submitted");
    }
    catch (Exception ex)
    {
        log.Error($"EncodeInputBlob function failed: {fileName}.{fileExtension}");
        log.Info($"StackTrace : {ex.StackTrace}");
        throw ex;
    }
}