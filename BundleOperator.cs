using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BundleCompiler.Agents;
using BundleCompiler.Caching;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler
{
    public static class BundleOperator
    {
        public static CacheManager CacheManager { get; private set; }

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
            foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx("UnlockAssetBase", true))
            {
                EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
                dynamic root = asset.RootObject;
                
                foreach (EbxAssetEntry levelEntry in App.AssetManager.EnumerateEbx("LevelData"))
                {
                    EbxAsset level = App.AssetManager.GetEbx(levelEntry);
                    dynamic levelRoot = level.RootObject;
                
                    if (levelRoot.UnlockIdTable.Identifiers.Contains(root.Identifier))
                        return;

                    levelRoot.UnlockIdTable.Identifiers.Add(root.Identifier);
                    App.AssetManager.ModifyEbx(App.AssetManager.GetEbxEntry(level.FileGuid).Name, level);
                }
            }
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
                        App.AssetManager.ModifyEbx(assetEntry.Name, App.AssetManager.GetEbx(assetEntry));
                    } break;
                }
            }

            foreach (ChunkAssetEntry chunk in App.AssetManager.EnumerateChunks())
            {
                chunk.AddedBundles.Clear();
            }

            foreach (ResAssetEntry resAssetEntry in App.AssetManager.EnumerateRes())
            {
                resAssetEntry.AddedBundles.Clear();
            }
            
            App.WhitelistedBundles.Clear();
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