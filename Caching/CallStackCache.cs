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

namespace BundleCompiler.Caching;

public class CallStackCache
{
    public static int Magic = Utils.HashString("YW_CallStackCache");
    public static int Version = 1002;
        
    public List<BundleCallStack> RootCallStacks { get; }
    public readonly Dictionary<int, BundleCallStack> CallStackIds = new();
    
    #region Cache Writing

    private List<int> _accountedIds = new();
    public void GenerateMainCache(FrostyTaskWindow? task = null)
    {
        if (!MeshVariationDb.IsLoaded)
        {
            MeshVariationDb.LoadVariations(task);
        }
            
        NativeWriter writer = new NativeWriter(new FileStream(
            $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Callstack.cache",
            FileMode.Create));

        #region Header

        writer.Write(Magic);
        writer.Write(Version);

        #endregion

        #region Callstack

        List<EbxAssetEntry> levels = App.AssetManager.EnumerateEbx("LevelData").ToList();
        task?.Update("Computing Level Bundle Call Stacks...");
        writer.Write(levels.Count);

        int idx = 0;
        foreach (EbxAssetEntry level in levels)
        {
            GenerateCallStack(writer, App.AssetManager.GetBundleEntry(level.Bundles[0]));
                
            idx++;
            task?.Update(null, (idx / (float)levels.Count) * 100.0);
        }

        idx = 0;
        task?.Update("Computing Shared Bundle Call Stacks...");
        List<BundleEntry> leftovers = App.AssetManager.EnumerateBundles(BundleType.SharedBundle)
            .Where(a => !_accountedIds.Contains(App.AssetManager.GetBundleId(a))).ToList();

        writer.Write(leftovers.Count);
        foreach (BundleEntry bundle in leftovers)
        {
            GenerateCallStack(writer, bundle);
                
            idx++;
            task?.Update(null, (idx / (float)levels.Count) * 100.0);
        }

        #endregion

        writer.Dispose();
    }
        
    private void GenerateCallStack(NativeWriter writer, BundleEntry bundleEntry)
    {
        _accountedIds.Add(App.AssetManager.GetBundleId(bundleEntry));
            
        writer.WriteNullTerminatedString(bundleEntry.Name);
        List<BundleEntry> children = new List<BundleEntry>();

        if (bundleEntry.Blueprint == null)
        {
            SharedCallStackAgent agent = new SharedCallStackAgent();
            agent.GenerateCallStack(bundleEntry, children);
        }
        else if (BlueprintCallStackAgent.Agents.ContainsKey(bundleEntry.Blueprint.Type))
        {
            BlueprintCallStackAgent.Agents[bundleEntry.Blueprint.Type].GenerateCallStack(bundleEntry, children);
        }

        writer.Write(children.Count);
        foreach (BundleEntry child in children)
        {
            GenerateCallStack(writer, child);
        }
    }

    #endregion

    #region Cache Reading

    public bool LoadMainCache(ILogger? logger = null)
    {
        string path = $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Callstack.cache";
        if (!File.Exists(path))
        {
            App.Logger.LogWarning("It appears you haven't generated cache yet, consider generating it?\n(Menu/Tools/Bundle Compiler/Generate Cache)");
            return false;
        }
            
        if (!MeshVariationDb.IsLoaded)
        {
            MeshVariationDb.LoadVariations();
        }

        NativeReader reader = new NativeReader(new FileStream(path, FileMode.Open));

        #region Header

        int mag = reader.ReadInt();
        if (mag != Magic)
        {
            reader.Dispose();
            return false;
        }

        int ver = reader.ReadInt();
        if (ver != Version)
        {
            logger?.LogError("This cache is out of date.");
            App.Logger.LogError("This cache is out of date.");
            reader.Dispose();
            return false;
        }

        #endregion

        #region Callstack

        logger?.Log("Reading bundle callstack cache...");
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            RootCallStacks.Add(LoadCallstack(reader));
        }
            
        count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            RootCallStacks.Add(LoadCallstack(reader));
        }

        #endregion

        reader.Dispose();
        return true;
    }

    private BundleCallStack LoadCallstack(NativeReader reader)
    {
        int bunId = App.AssetManager.GetBundleId(reader.ReadNullTerminatedString());
        BundleEntry bundleEntry = App.AssetManager.GetBundleEntry(bunId);
        BundleCallStack callStack = new BundleCallStack(bundleEntry);

        if (!CallStackIds.ContainsKey(bunId))
        {
            CallStackIds.Add(bunId, callStack);
        }

        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            callStack.Stacks.Add(LoadCallstack(reader));
        }

        return callStack;
    }

    #endregion
        
    public void ClearAll()
    {
        RootCallStacks.Clear();
        CallStackIds.Clear();
    }

    public CallStackCache()
    {
        RootCallStacks = new List<BundleCallStack>();
    }
}