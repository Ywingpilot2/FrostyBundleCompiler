using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BundleCompiler.Agents;
using BundleCompiler.Caching;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler
{
    public static class BundleOperator
    {
        public static CacheManager CacheManager { get; private set; }
        public static List<Guid> PureBundled { get; } = new();
        public static bool WhitelistBundles = false;

        public static void CompileBundles(FrostyTaskWindow? task = null)
        {
            int idx = 0;
            foreach (BundleCallStack callStack in CacheManager.RootCallStacks)
            {
                CompileBundle(callStack, task);

                idx++;
                task?.Update(null, (idx / (float)CacheManager.RootCallStacks.Count) * 100.0);
            }
        }

        public static void CompileBundle(BundleCallStack callStack, FrostyTaskWindow? task = null)
        {
            task?.Update($"Compiling {callStack.Caller.Name}");
            CompilerAgent agent = new CompilerAgent();
            agent.CompileAssets(callStack);
        }

        public static void CompileIdTables()
        {
            foreach (BundleCallStack callStack in CacheManager.RootCallStacks)
            {
                if (callStack.Caller.Type != BundleType.SubLevel)
                    continue;

                CompileIdTable(callStack);
            }
        }

        public static void CompileIdTable(BundleCallStack callStack)
        {
            StackCrawlerAgent agent = new StackCrawlerAgent();
            
            EbxAssetEntry? levelEntry = callStack.Asset;
            if (levelEntry == null)
                return;
                
            EbxAsset level = App.AssetManager.GetEbx(levelEntry);
            dynamic levelRoot = level.RootObject;
                
            agent.CrawlThroughCompilable(callStack, entry =>
            {
                if (!TypeLibrary.IsSubClassOf(entry.Type, "UnlockAssetBase"))
                    return;

                uint identifier;
                if (CacheManager.IdCache.UnlockAssetToId.ContainsKey(entry.Name))
                {
                    identifier = (uint)CacheManager.IdCache.UnlockAssetToId[entry.Name];
                }
                else
                {
                    EbxAsset asset = App.AssetManager.GetEbx(entry);
                    dynamic root = asset.RootObject;
                    identifier = (uint)root.Identifier;
                }
                
                if (levelRoot.UnlockIdTable.Identifiers.Contains(identifier))
                    return;

                levelRoot.UnlockIdTable.Identifiers.Add(identifier);
                    
                App.AssetManager.ModifyEbx(levelEntry.Name, level);
            });
        }

        public static void ClearBundles()
        {
            List<EbxAssetEntry> assetEntries = App.AssetManager.EnumerateEbx("", true).ToList();
            foreach (EbxAssetEntry assetEntry in assetEntries)
            {
                switch (assetEntry.Type)
                {
                    case "MeshVariationDatabase":
                    case "NetworkRegistryAsset":
                    {
                        if (assetEntry.IsAdded)
                            break;
                        
                        App.AssetManager.RevertAsset(assetEntry);
                    } break;
                    case "LevelData": break;
                    case "SubWorldData": break;
                    default:
                    {
                        if (assetEntry.AddedBundles.Count == 0)
                            break;
                        
                        assetEntry.AddedBundles.Clear();
                        if (!assetEntry.HasModifiedData || PureBundled.Contains(assetEntry.Guid))
                        {
                            App.AssetManager.RevertAsset(assetEntry);
                            PureBundled.Remove(assetEntry.Guid);
                        }
                    } break;
                }
            }

            foreach (ChunkAssetEntry chunk in App.AssetManager.EnumerateChunks())
            {
                chunk.AddedBundles.Clear();
                if (!chunk.HasModifiedData)
                {
                    App.AssetManager.RevertAsset(chunk);
                }
            }

            foreach (ResAssetEntry resAssetEntry in App.AssetManager.EnumerateRes())
            {
                resAssetEntry.AddedBundles.Clear();
                if (!resAssetEntry.HasModifiedData)
                {
                    App.AssetManager.RevertAsset(resAssetEntry);
                }
            }
            
            App.WhitelistedBundles.Clear();
            WhitelistBundles = false;
        }

        public static void AddWhitelistedBundle(BundleCallStack callStack)
        {
            AddWhitelistedBundle(callStack.Caller);
        }
        
        public static void AddWhitelistedBundle(BundleEntry bentry)
        {
            if (!WhitelistBundles)
                return;
            
            int hash = HashBundle(bentry);
            if (App.WhitelistedBundles.Contains(hash))
                return;
            
            App.WhitelistedBundles.Add(hash);
        }
        
        public static int HashBundle(BundleEntry bundle)
        {
            int hash = Utils.HashString(bundle.Name, true);

            if (bundle.Name.Length == 8 && int.TryParse(bundle.Name, System.Globalization.NumberStyles.HexNumber, null, out int tmp))
                hash = tmp;

            return hash;
        }

        public static void Initialize(ILogger? logger = null)
        {
            CacheManager.LoadCache(logger);
        }

        static BundleOperator()
        {
            CacheManager = new CacheManager();
        }
    }
}