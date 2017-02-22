using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json;

public class IngestSource
{
    public string SourceContainerName { get; set; }
}

public class AssetFile
{
    public string FileName { get; set; }
    public bool IsPrimary { get; set; }
}

public class IngestAsset
{
    public string AssetName { get; set; }
    public List<AssetFile> AssetFiles { get; set; }
    public string AssetCreationOption { get; set; }
    public AssetCreationOptions CreationOption = AssetCreationOptions.None;
    
    public void setAssetCreationOption()
    {
        switch (this.AssetCreationOption)
        {
            case "CommonEncryptionProtected":
                this.CreationOption = AssetCreationOptions.CommonEncryptionProtected;
                break;
            case "EnvelopeEncryptionProtected":
                this.CreationOption = AssetCreationOptions.EnvelopeEncryptionProtected;
                break;
            case "None":
            default:
                this.CreationOption = AssetCreationOptions.None;
                break;
        }
    }
}

public class IngestAssetEncoding
{
    public bool Encoding { get; set; }
    public string Encoder { get; set; }
    public string EncodingPreset { get; set; }
    public string EncodingConfiguration { get; set; }
    public string PremiumWorkflowAssetId { get; set; }
}

public class IngestAssetPublish
{
    public string StartDate { get; set; }
    public string EndDate { get; set; }
    public DateTime PublishStartDateTime = DateTime.Now;
    public TimeSpan PublishSpan = TimeSpan.FromDays(365);
    public void set()
    {
        if (this.StartDate != null && this.StartDate != "")
        {
            this.PublishStartDateTime = Convert.ToDateTime(this.StartDate);
        }

        if (this.EndDate != null && this.EndDate != "")
        {
            DateTime end = Convert.ToDateTime(this.EndDate);
            this.PublishSpan = end.Subtract(PublishStartDateTime);
        }
    }
}

public class IngestAssetConfig
{
    public IngestSource IngestSource { get; set; }
    public IngestAsset IngestAsset { get; set; }
    public IngestAssetEncoding IngestAssetEncoding { get; set; }
    public IngestAssetPublish IngestAssetPublish { get; set; }

}


public static IngestAssetConfig ParseIngestAssetConfig(string jsonIngestConfig)
{
    IngestAssetConfig c = JsonConvert.DeserializeObject<IngestAssetConfig>(jsonIngestConfig);
    //c.IngestAsset.setAssetCreationOption();
    return c;
}

public static bool ValidateIngestAssetConfig(IngestAssetConfig config)
{
    bool result = false;

    // IngestAsset
    if (config.IngestAsset == null) return result;
    if (config.IngestAsset.AssetName == null) return result;
    if (config.IngestAsset.AssetFiles == null) return result;
    config.IngestAsset.setAssetCreationOption();

    // IngestSource
    if (config.IngestSource == null) return result;
    if (config.IngestSource.SourceContainerName == null) return result;

    // IngestPublish
    if (config.IngestAssetPublish != null)
    {
        if (config.IngestAssetPublish.StartDate != null)
        {
            if (config.IngestAssetPublish.EndDate == null) return result;
            try
            {
                DateTime start = Convert.ToDateTime(config.IngestAssetPublish.StartDate);
                if (DateTime.Compare(DateTime.Now, start) >= 0) return result;
            }
            catch (Exception)
            {
                return result;
            }
        }
        if (config.IngestAssetPublish.EndDate != null)
        {
            try
            {
                DateTime end = Convert.ToDateTime(config.IngestAssetPublish.EndDate);
                if (DateTime.Compare(DateTime.Now, end) >= 0) return result;
            }
            catch (Exception)
            {
                return result;
            }
        }
        config.IngestAssetPublish.set();
    }
    else
    {
        config.IngestAssetPublish = new IngestAssetPublish();
        config.IngestAssetPublish.StartDate = null;
        config.IngestAssetPublish.EndDate = null;
    }

    return true;
}

public static string[] GetAssetFilesFromIngestAssetConfig(IngestAssetConfig config)
{
    List<AssetFile> assetFiles = config.IngestAsset.AssetFiles;
    List<string> assetFileNames = new List<string>();
    foreach (AssetFile srcAssetFile in assetFiles)
    {
        if (srcAssetFile.IsPrimary)
        {
            assetFileNames.Insert(0, srcAssetFile.FileName);
        }
        else
        {
            assetFileNames.Add(srcAssetFile.FileName);
        }
    }
    return assetFileNames.ToArray();
}