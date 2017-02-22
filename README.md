<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fsabbour%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

# Azure Functions Media Services Workflow
This project contains examples of using Azure Functions with Azure Media Services. 
The project includes several folders of sample Azure Functions for use with Azure Media Services that show workflows related
to ingesting content directly from blob storage, encoding, and writing content back to blob storage. It also includes examples of
how to monitor job notifications via WebHooks and Azure Queues. 

## Deploying to Azure
It is recommended that you first fork the project and update the "sourceCodeRepositoryURL" in the [azuredeploy.json](azuredeploy.json) template parameters
when deploying to your own Azure account.  That way you can more easily update, experiment and edit the code and see changes
reflected quickly in your own Functions deployment.  

## Questions & Help

If you have questions about Azure Media Services and Functions, we encourage you to reach out and participate in our community. 
The Media Services engineering and product management team monitors the following community sites and is available to help.

 - For all questions and technical help, [our MSDN forums](https://social.msdn.microsoft.com/forums/azure/en-US/home?forum=MediaServices) are an easy place to have a conversation with our product team.
 - For questions which fit the Stack Overflow format ("*how* does this work?"), we monitor the [azure-media-services](http://stackoverflow.com/questions/tagged/azure%20media%20service) tag.
 - You can also tweet/follow [@MSFTAzureMedia](https://twitter.com/MSFTAzureMedia).
 
While we do our best to help out in a timely basis, we don't have any promise around the above resources. If you need an SLA on support from us, it's recommended you invest in an [Azure Support plan](https://azure.microsoft.com/en-us/support/options/).

## How to run the sample

To run the samples, first fork this project into your own repository, and then deploy the Functions with the [azuredeploy.json](azuredeploy.json) template
Make sure to update the path to point to your github fork.  

The deploymnet template will automatically create the following resources:
* Azure Media Services Account.
* Storage account attached to your media account.
* This Azure Functions application with your source code configured for continuous integration.
* The required function's application settings will be updated to point to the new resources automatically. You can modify any of these settings after deployment.

### Function Application Settings 
The following applications settings are created upon deployment and are automatically linked to the resources
deployed with the azuredeploy.json template.

* **WF_MediaServicesAccountName** - your Media Services Account name. 
* **WF_MediaServicesAccountKey** - your Media Services key. 
* **WF_StorageAccountName** - the storage account name tied to your Media Services account. 
* **WF_StorageAccountKey** - the storage account key tied to your Media Services account. 
* **WF_StorageConnection** -  the connection string to your Azure Storage account. This is used by the function triggers for Blob and Queue.
* **WF_CallbackUrl** - a URL that will be called once the publishing queue message is processed.
* **WF_PresetName** - a built-in encoding preset name. The default value is "H264 Multiple Bitrate 720p". Select your preset name from https://docs.microsoft.com/en-us/azure/media-services/media-services-mes-presets-overview
 
  ### Connection Strings:
  If you are adjusting the deployment settings to use an existing Media Services account or storage account, 
  you can find the connection string for your storage account in the Azure portal. Go to Access Keys in Settings. In the Access Keys blade
  go to Key1, or Key2, click the "..." menu and select "view connection string". Copy the connection string.

## NuGets
The EncodeBlobInput and ProcessEncodingStatusMessage functions both use some NuGets. Each one specifies those NuGets in its respective project.json file as below.

    {
        "frameworks": {
            "net46":{
                "dependencies": {
                    "windowsazure.mediaservices": "3.8.0.5",
                    "windowsazure.mediaservices.extensions": "3.8.0.3"
                }
            }
        }
    }

## EncodeBlobInput Function
The EncodeBlobInput Function has a Blob Storage input binding trigger to an Azure Storage container.

In the function.json, you will notice that we use a binding direction of "In" and also set the name to "inputBlob".
The path is also updated to point to a specific input container, and a pattern is provided for naming the input file. 

    {
      "name": "inputBlob",
      "type": "blobTrigger",
      "direction": "in",
      "path": "input/{fileName}.{fileExtension}",
      "connection": "WF_StorageConnection"
    }

In the run.csx file, we then bind this inputBlob to the Run method signature as a CloudBlockBlob. 

    public static void Run(CloudBlockBlob inputBlob, string fileName, string fileExtension, TraceWriter log)

What we do then is create the Azure Storage Queue that will hold the notifications of progress and the CloudMediaContext, to access the Media Services SDK functionality.

Once we have those in place, we create a new Input Asset from an existing blob, delete the uploaded copy and launch the encoding process.

We also instruct the platform to publish progress change methods to the queue created earlier.

## ProcessEncodingStatusMessage Function
The ProcessEncodingStatusMessage Function has a Queue Storage input/output binding trigger to an Azure Storage container.

    "bindings": [
        {
        "name": "encodingJobMsg",
        "type": "queueTrigger",
        "direction": "in",
        "queueName": "mwfprogressqueue",
        "connection": "WF_StorageConnection"
        },
        {
        "name": "publishLocatorMsg",
        "type": "queue",
        "direction": "out"
        "queueName": "mwfpublishqueue",
        "connection": "WF_StorageConnection",
        }
    ]


In the run.csx file, we then bind the encodingJobMsg and the publishLocatorMsg. 

    public static void Run(EncodingJobMessage encodingJobMsg, out PublishedLocatorMessage publishLocatorMsg, TraceWriter log)

Once this function receives a notification message (EncodingJobMessage) on the queue from Azure Media Services, it will inspect it, and if the message states that the job has been encoded
it will create a Streaming Locator for the Output Asset, publish it with a 10-year streaming policy then put a message on another queue with the asset details.


## ProcessPublishingMessage Function

This function also binds to a queue, which receives a message once the Output Asset is published. In this example, all what the function does is
initate an HTTP POST request to the call back Url in **WF_CallbackUrl** in your function's Application Settings with the Json serialization of the object below.

    public class PublishedLocatorMessage
    {
        public string AssetId { get; set; }
        public string Smooth { get; set; }
        public string HLSv3 { get; set; }
        public string HLSv4 { get; set; }
        public string DASH { get; set; }
    }

You may probably need to create different logic here depending on what you want to do.

### License
This sample project is licensed under [the MIT License](LICENSE) and is provided with no warranty.