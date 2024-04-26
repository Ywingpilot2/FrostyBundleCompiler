using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BundleCompiler.Caching;
using Frosty.Core;
using FrostySdk.Managers;

namespace BundleCompiler.Agents
{
    public class CompilerAgent
    {
        // This organizes everything which needs to be compiled into a big orderly list
        private Dictionary<int, List<EbxAssetEntry>> _assetsToCompile = new();
        // This is a list of every asset we have determined so far. 
        private List<string> _checkedAssets = new();
        // A list of bundles that are currently loaded
        private List<int> _loadedBundles = new();

        private void CrawlDownStack(BundleCallStack rootCall)
        {
            int bunId = rootCall.CallerId;

            // We need to only check this call if it contains anything modified
            // Problem: LevelDatas will almost always be modified due to UnlockIdTables
            // Solution: Don't count LevelDatas. If they reference anything new, its almost guaranteed a layerdata will be modified alongside it anyway
            if (App.AssetManager.EnumerateEbx(rootCall.Caller).Any(e => e.IsModified && e.AssetType != "LevelData"))
            {
                _assetsToCompile.Add(bunId, new List<EbxAssetEntry>());
                foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx(rootCall.Caller))
                {
                    // Only enumerate modified ebx
                    if (!assetEntry.IsModified)
                        continue;
                
                    if (assetEntry.Type is "NetworkRegistryAsset" or "MeshVariationDatabase")
                        continue;
                    
                    CheckDependencies(assetEntry, rootCall);

                    if (assetEntry.Bundles.Contains(bunId))
                        continue;
                
                    // This asset is already loaded indirectly
                    if (assetEntry.Bundles.Any(b => _loadedBundles.Contains(b)))
                        continue;
                
                    if (_assetsToCompile[bunId].Contains(assetEntry))
                        continue;
                
                    _assetsToCompile[bunId].Add(assetEntry);
                    _checkedAssets.Add(assetEntry.Name);
                }
            }

            _loadedBundles.Add(bunId);
            foreach (BundleCallStack callStack in rootCall.Stacks)
            {
                CrawlDownStack(callStack);
            }
        }

        private void CheckDependencies(EbxAssetEntry assetEntry, BundleCallStack rootCall)
        {
            int bunId = App.AssetManager.GetBundleId(rootCall.Caller);
            
            foreach (Guid dependency in assetEntry.EnumerateDependencies())
            {
                EbxAssetEntry reference = App.AssetManager.GetEbxEntry(dependency);
                    
                // Already in this bundle
                if (reference.Bundles.Contains(bunId))
                    continue;
                
                // This asset is already loaded indirectly
                if (reference.Bundles.Any(b => _loadedBundles.Contains(b)))
                    continue;
                    
                // If this reference is in the next immediate bundle, don't bother adding it since we will handle it later
                if (reference.Bundles.Any(b => rootCall.CallsBundle(b, true)))
                    continue;

                if (_checkedAssets.Contains(reference.Name))
                {
                    BundleEntry entry = GetAssetBundle(reference)!;

                    if (entry.Type == BundleType.SubLevel && rootCall.Caller.Type is BundleType.SharedBundle)
                    {
                        int idToRem = App.AssetManager.GetBundleId(entry);
                        _assetsToCompile[idToRem].Remove(reference);
                    }
                    else
                    {
                        continue;
                    }
                }
                
                if (_assetsToCompile[bunId].Contains(reference))
                    continue;
                    
                _assetsToCompile[bunId].Add(reference);
                _checkedAssets.Add(reference.Name);
                
                CheckDependencies(reference, rootCall);
            }
        }

        private BundleEntry? GetAssetBundle(EbxAssetEntry assetEntry)
        {
            foreach (KeyValuePair<int,List<EbxAssetEntry>> valuePair in _assetsToCompile)
            {
                if (!valuePair.Value.Contains(assetEntry))
                    continue;

                return App.AssetManager.GetBundleEntry(valuePair.Key);
            }

            return null;
        }

        public void CompileAssets(BundleCallStack rootCall)
        {
#if DEVELOPER___DEBUG
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            CrawlDownStack(rootCall);
#if DEVELOPER___DEBUG
            stopwatch.Stop();
            App.Logger.LogWarning("Spent {0} crawling down stacks for {1}", stopwatch.Elapsed.ToString(), rootCall.ToString());
            stopwatch.Reset();
            stopwatch.Start();
#endif

            foreach (KeyValuePair<int,List<EbxAssetEntry>> valuePair in _assetsToCompile)
            {
                List<EbxAssetEntry> entries = valuePair.Value;
                while (entries.Count != 0)
                {
                    if (CompilerHandler.HasHandler(entries[0].Type))
                    {
                        CompilerHandler.GetHandler(entries[0].Type).CompileAssetBundle(entries[0], BundleOperator.CacheManager.GetCallStack(valuePair.Key), entries);
                    }
                    else
                    {
                        BundleEditor.AddToBundle(entries[0], BundleOperator.CacheManager.GetCallStack(valuePair.Key));
                        entries.Remove(entries[0]);
                    }
                }
            }

#if DEVELOPER___DEBUG
            stopwatch.Stop();
            App.Logger.LogWarning("Compiled {0}'s bundles in {1}", rootCall.ToString(), stopwatch.Elapsed.ToString());
#endif
        }

        public CompilerAgent()
        {
            // Root shared bundles are always loaded
            foreach (BundleCallStack rootCallStack in BundleOperator.CacheManager.RootCallStacks)
            {
                if (rootCallStack.Caller.Type != BundleType.SharedBundle)
                    continue;
                
                _loadedBundles.Add(App.AssetManager.GetBundleId(rootCallStack.Caller));
            }
        }
    }
}