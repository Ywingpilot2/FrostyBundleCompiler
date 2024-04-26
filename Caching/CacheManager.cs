using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BundleCompiler.Agents;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostyEditor;
using FrostySdk;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler.Caching
{
    public class CacheManager
    {
        public readonly CallStackCache CallStackCache = new();
        public readonly BundleCache BundleCache = new();
        public readonly LevelTypeCache LevelTypeCache = new();

        public List<BundleCallStack> RootCallStacks => CallStackCache.RootCallStacks;

        public void LoadCache(ILogger? logger = null)
        {
            CallStackCache.LoadMainCache(logger);
            BundleCache.LoadMainCache(logger);
            LevelTypeCache.LoadMainCache(logger);
        }

        public void ClearCache()
        {
            CallStackCache.ClearAll();
            BundleCache.ClearAll();
            LevelTypeCache.ClearAll();
        }

        public void GenerateCache(FrostyTaskWindow? taskWindow = null)
        {
            ClearCache();
            CallStackCache.GenerateMainCache(taskWindow);
            BundleCache.GenerateMainCache(taskWindow);
            LevelTypeCache.GenerateMainCache(taskWindow);
            LoadCache(taskWindow?.TaskLogger);
        }

        #region Cache Management

        public BundleEntry? GetRootCall(BundleEntry entry)
        {
            foreach (BundleCallStack rootCall in RootCallStacks)
            {
                if (!rootCall.CallsBundle(entry, true))
                    continue;

                return rootCall.Caller;
            }
            
            return null;
        }

        public bool HasCallStack(int id)
        {
            return CallStackCache.CallStackIds.ContainsKey(id);
        }

        public BundleCallStack GetCallStack(int id)
        {
            return CallStackCache.CallStackIds[id];
        }

        public EbxAsset? GetNetworkedBundle(string bundleName)
        {
            if (!BundleCache.NetworkedBundles.ContainsKey(bundleName))
                return null;

            EbxAsset asset = App.AssetManager.GetEbx(BundleCache.NetworkedBundles[bundleName]);
            return asset;
        }
        
        public EbxAsset? GetMvdbBundle(string bundleName)
        {
            if (!BundleCache.VariationDatabases.ContainsKey(bundleName))
                return null;

            EbxAsset asset = App.AssetManager.GetEbx(BundleCache.VariationDatabases[bundleName]);
            return asset;
        }

        #endregion
    }

    public struct BundleCallStack
    {
        public BundleEntry Caller { get; }
        public int CallerId { get; }
        public EbxAssetEntry? Asset { get; }
        public List<BundleCallStack> Stacks { get; }

        public bool CallsBundle(BundleEntry bundleEntry, bool isRecursive = false)
        {
            foreach (BundleCallStack callStack in Stacks)
            {
                if (callStack.Caller == bundleEntry)
                    return true;

                if (isRecursive)
                {
                    if (!callStack.CallsBundle(bundleEntry, true))
                        continue;

                    return true;
                }
            }
            
            return false;
        }
        
        public bool CallsBundle(int bunId, bool isRecursive = false)
        {
            BundleEntry bundleEntry = App.AssetManager.GetBundleEntry(bunId);

            return CallsBundle(bundleEntry, isRecursive);
        }

        public BundleCallStack(BundleEntry caller)
        {
            Caller = caller;
            CallerId = App.AssetManager.GetBundleId(caller);
            if (caller.Blueprint != null)
            {
                Asset = caller.Blueprint;
            }
            Stacks = new List<BundleCallStack>();
        }

        public BundleCallStack(int callerId)
        {
            CallerId = callerId;
            Caller = App.AssetManager.GetBundleEntry(callerId);
            if (Caller.Blueprint != null)
            {
                Asset = Caller.Blueprint;
            }
            Stacks = new List<BundleCallStack>();
        }

        public BundleCallStack(BundleEntry caller, EbxAssetEntry asset)
        {
            Caller = caller;
            Asset = asset;
            Stacks = new List<BundleCallStack>();
        }

        public override string ToString()
        {
            return $"{Caller.DisplayName} Callstack";
        }
    }
}