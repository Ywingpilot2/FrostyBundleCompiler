using System;
using System.Collections.Generic;
using System.Linq;
using BundleCompiler.Caching;
using Frosty.Core;
using FrostySdk.Managers;

namespace BundleCompiler.Agents;

/// <summary>
/// This simple agent crawls down a stack and conducts user specified actions
/// </summary>
public class StackCrawlerAgent
{
    private Dictionary<int, List<string>> _checkedAssets = new();
    private List<int> _loadedBundles = new();

    /// <summary>
    /// Crawls down the 
    /// </summary>
    /// <param name="rootCall"></param>
    /// <param name="action"></param>
    public void CrawlThroughBundles(BundleCallStack rootCall, Action<BundleCallStack> action)
    {
        action.Invoke(rootCall);

        foreach (BundleCallStack callStack in rootCall.Stacks)
        {
            CrawlThroughBundles(callStack, action);
        }
    }

    /// <summary>
    /// Crawls down the callstack executing the specified action for assets which "need compiling"
    /// </summary>
    /// <param name="rootCall"></param>
    /// <param name="action"></param>
    public void CrawlThroughCompilable(BundleCallStack rootCall, Action<EbxAssetEntry> action)
    {
        _checkedAssets.Add(rootCall.CallerId, new List<string>());

        foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx(rootCall.Caller))
        {
            // Only enumerate modified ebx
            if (!assetEntry.IsModified)
                continue;
                
            if (assetEntry.Type is "NetworkRegistryAsset" or "MeshVariationDatabase")
                continue;
                    
            CheckDependencies(assetEntry, rootCall, action);

            if (assetEntry.Bundles.Contains(rootCall.CallerId))
                continue;
                
            // This asset is already loaded indirectly
            if (assetEntry.Bundles.Any(b => _loadedBundles.Contains(b)))
                continue;
                
            action.Invoke(assetEntry);
            _checkedAssets[rootCall.CallerId].Add(assetEntry.Name);
        }
        
        // We cannot guarantee a blueprint bundle will actually be loaded
        if (rootCall.Caller.Type != BundleType.BlueprintBundle)
        {
            _loadedBundles.Add(rootCall.CallerId);
        }
            
        foreach (BundleCallStack callStack in rootCall.Stacks)
        {
            CrawlDownCompInternal(callStack, action);
        }
        
        _loadedBundles.Clear();
        _checkedAssets.Clear();
    }

    private void CrawlDownCompInternal(BundleCallStack rootCall, Action<EbxAssetEntry> action)
    {
        _checkedAssets.Add(rootCall.CallerId, new List<string>());

        foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx(rootCall.Caller))
        {
            // Only enumerate modified ebx
            if (!assetEntry.IsModified)
                continue;
                
            if (assetEntry.Type is "NetworkRegistryAsset" or "MeshVariationDatabase")
                continue;
                    
            CheckDependencies(assetEntry, rootCall, action);

            if (assetEntry.Bundles.Contains(rootCall.CallerId))
                continue;
                
            // This asset is already loaded indirectly
            if (assetEntry.Bundles.Any(b => _loadedBundles.Contains(b)))
                continue;
                
            action.Invoke(assetEntry);
            _checkedAssets[rootCall.CallerId].Add(assetEntry.Name);
        }
        
        // We cannot guarantee a blueprint bundle will actually be loaded
        if (rootCall.Caller.Type != BundleType.BlueprintBundle)
        {
            _loadedBundles.Add(rootCall.CallerId);
        }
            
        foreach (BundleCallStack callStack in rootCall.Stacks)
        {
            CrawlThroughCompilable(callStack, action);
        }

        if (rootCall.Caller.Type != BundleType.SharedBundle)
        {
            _loadedBundles.Remove(rootCall.CallerId);
        }
    }

    private void CheckDependencies(EbxAssetEntry assetEntry, BundleCallStack rootCall, Action<EbxAssetEntry> action)
    {
        foreach (Guid dependency in assetEntry.EnumerateDependencies())
        {
            EbxAssetEntry reference = App.AssetManager.GetEbxEntry(dependency);
            if (reference == null)
                continue;
                    
            // Already in this bundle
            if (reference.Bundles.Contains(rootCall.CallerId))
                continue;
                
            // This asset is already loaded indirectly
            if (reference.Bundles.Any(b => _loadedBundles.Contains(b)))
                continue;
                    
            // If this reference is in the next immediate bundle, don't bother adding it since we will handle it later
            if (reference.Bundles.Any(b => rootCall.CallsBundle(b, true)))
                continue;
            
            action.Invoke(assetEntry);
            _checkedAssets[rootCall.CallerId].Add(reference.Name);
            
            CheckDependencies(reference, rootCall, action);
        }
    }
}