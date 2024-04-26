using System;
using System.Collections.Generic;
using System.Reflection;
using FrostyEditor;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler.Agents;

/// <summary>
/// Enumerates down a Bundle Call Stack chain and generates call stacks
/// </summary>
public interface ICallStackAgent
{
    void GenerateCallStack(BundleEntry bundleEntry, List<BundleEntry> children);
}

public class SharedCallStackAgent : ICallStackAgent
{
    public void GenerateCallStack(BundleEntry bundleEntry, List<BundleEntry> children)
    {
        // First we enumerate over the ebx to catch obvious things
        foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx(bundleEntry))
        {
            switch (assetEntry.Type)
            {
                case "BlueprintBundleCollection":
                {
                    EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
                    dynamic root = asset.RootObject;
                    
                    foreach (dynamic bundle in root.Bundles)
                    {
                        int bunId = App.AssetManager.GetBundleId($"win32/{bundle.Name.ToString().ToLower()}");
                        if (bunId == App.AssetManager.GetBundleId(bundleEntry) || bunId == -1)
                            continue;

                        BundleEntry entry = App.AssetManager.GetBundleEntry(bunId);
                        if (children.Contains(entry))
                            continue;
                        
                        children.Add(entry);
                    }
                } break;
            }
        }
        
        // TODO: Find a way to figure out the call stack of shared bundles
        // We should be able to judge based on the Super Bundles
    }
}

public abstract class BlueprintCallStackAgent : ICallStackAgent
{
    public static Dictionary<string, BlueprintCallStackAgent> Agents = new();

    static BlueprintCallStackAgent()
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsSubclassOf(typeof(BlueprintCallStackAgent)))
            {
                BlueprintCallStackAgent agent = (BlueprintCallStackAgent)Activator.CreateInstance(type);
                Agents.Add(agent.AssetType, agent);
            }
        }
    }

    public abstract string AssetType { get; }
    public abstract void GenerateCallStack(BundleEntry bundleEntry, List<BundleEntry> children);
}

public class LevelCallStack : BlueprintCallStackAgent
{
    public override string AssetType => "LevelData";
    public override void GenerateCallStack(BundleEntry bundleEntry, List<BundleEntry> children)
    {
        EbxAssetEntry assetEntry = bundleEntry.Blueprint;
        EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
        
        // Things the description loads go first, since those are going to be loaded before everything else
        EbxAssetEntry descriptionEntry = App.AssetManager.GetEbxEntry($"{assetEntry.Name}/Description");
        if (descriptionEntry == null)
        {
            App.Logger.LogError("Could not find level description for {0}", assetEntry.Filename);
            return;
        }

        EbxAsset descriptionAsset = App.AssetManager.GetEbx(descriptionEntry);
        dynamic root = descriptionAsset.RootObject;
        int bunId = App.AssetManager.GetBundleId(bundleEntry);
        foreach (dynamic bundle in root.Bundles)
        {
            int childId = App.AssetManager.GetBundleId($"win32/{bundle.Name.ToString().ToLower()}");
            if (childId == bunId)
                continue;
            
            children.Add(App.AssetManager.GetBundleEntry(childId));
        }

        foreach (object assetObject in asset.Objects)
        {
            if (assetObject.GetType().Name == "SubWorldReferenceObjectData")
            {
                EbxAssetEntry subworldEntry = App.AssetManager.GetEbxEntry(((dynamic)assetObject).BundleName.ToString());
                if (subworldEntry == null)
                {
                    App.Logger.LogError("Subworld {0} referenced in {1} does not exist.", ((dynamic)assetObject).BundleName.ToString(), assetEntry.Filename);
                    continue;
                }

                BundleEntry child = App.AssetManager.GetBundleEntry(subworldEntry.Bundles[0]);
                if (children.Contains(child))
                    continue;
                    
                children.Add(child);
            }
        }
    }
}

public class SubworldCallStack : BlueprintCallStackAgent
{
    public override string AssetType => "SubWorldData";
    public override void GenerateCallStack(BundleEntry bundleEntry, List<BundleEntry> children)
    {
        EbxAssetEntry assetEntry = bundleEntry.Blueprint;
        EbxAsset asset = App.AssetManager.GetEbx(assetEntry);

        foreach (object assetObject in asset.Objects)
        {
            if (assetObject.GetType().Name == "SubWorldReferenceObjectData")
            {
                EbxAssetEntry subworldEntry = App.AssetManager.GetEbxEntry(((dynamic)assetObject).BundleName.ToString());
                if (subworldEntry == null)
                {
                    App.Logger.LogError("Subworld {0} referenced in {1} does not exist.", ((dynamic)assetObject).BundleName.ToString(), assetEntry.Filename);
                    continue;
                }

                BundleEntry child = App.AssetManager.GetBundleEntry(subworldEntry.Bundles[0]);
                if (children.Contains(child))
                    continue;
                    
                children.Add(child);
            }
        }
    }
}
