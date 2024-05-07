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
        public readonly ReferenceCache ReferenceCache = new();
        public readonly UnlockIdCache IdCache = new();

        public List<BundleCallStack> RootCallStacks => CallStackCache.RootCallStacks;

        public void LoadCache(ILogger? logger = null)
        {
            CallStackCache.LoadMainCache(logger);
            BundleCache.LoadMainCache(logger);
            LevelTypeCache.LoadMainCache(logger);
            ReferenceCache.LoadMainCache(logger);
            IdCache.LoadMainCache(logger);
        }

        public void ClearCache()
        {
            CallStackCache.ClearAll();
            BundleCache.ClearAll();
            LevelTypeCache.ClearAll();
            ReferenceCache.ClearAll();
            IdCache.ClearAll();
        }

        public void GenerateCache(FrostyTaskWindow? taskWindow = null)
        {
            ClearCache();
            CallStackCache.GenerateMainCache(taskWindow);
            BundleCache.GenerateMainCache(taskWindow);
            LevelTypeCache.GenerateMainCache(taskWindow);
            ReferenceCache.GenerateMainCache(taskWindow);
            IdCache.GenerateMainCache(taskWindow);
            LoadCache(taskWindow?.TaskLogger);
        }

        #region Cache Management

        public IEnumerable<BundleCallStack> EnumerateRootCalls(BundleType type = BundleType.None)
        {
            foreach (BundleCallStack callStack in RootCallStacks)
            {
                if (type != BundleType.None && callStack.Caller.Type != type)
                    continue;

                yield return callStack;
            }
        }

        public List<EbxImportReference> GetNetworkReferences(int bunId)
        {
            BundleEntry bentry = App.AssetManager.GetBundleEntry(bunId);
            if (bentry == null)
                return new List<EbxImportReference>();

            return ReferenceCache.NetworkBunReferences[bunId];
        }

        public List<EbxImportReference> GetNetworkReferences(BundleCallStack callStack)
        {
            if (BundleCache.NetworkedBundles.ContainsKey(callStack.Caller.Name))
                return new List<EbxImportReference>();

            return GetNetworkReferences(callStack.CallerId);
        }

        public List<EbxImportReference> GetNetworkReferences(string name)
        {
            return ReferenceCache.NetworkStrReferences[name];
        }

        public List<EbxImportReference> GetNetworkReferences(Guid registry)
        {
            return ReferenceCache.NetworkReferences[registry];
        }

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

        public EbxAsset? GetNetworkedBundle(int bunId)
        {
            BundleEntry bentry = App.AssetManager.GetBundleEntry(bunId);
            if (bentry == null)
                return null;

            return GetNetworkedBundle(bentry.Name);
        }

        public EbxAsset? GetNetworkedBundle(string bundleName)
        {
            if (!BundleCache.NetworkedBundles.ContainsKey(bundleName))
                return null;

            EbxAsset asset = App.AssetManager.GetEbx(BundleCache.NetworkedBundles[bundleName]);
            return asset;
        }
        
        public EbxAsset? GetMvdbBundle(int bunId)
        {
            BundleEntry bentry = App.AssetManager.GetBundleEntry(bunId);
            if (bentry == null)
                return null;

            return GetMvdbBundle(bentry.Name);
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