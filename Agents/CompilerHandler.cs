using System;
using System.Collections.Generic;
using System.Reflection;
using BundleCompiler.Caching;
using Frosty.Core;
using FrostySdk;
using FrostySdk.Managers;

namespace BundleCompiler.Agents;

public abstract class CompilerHandler
{
    private static Dictionary<string, CompilerHandler> _compileHandlers = new();

    public static bool HasHandler(string type)
    {
        if (!_compileHandlers.ContainsKey(type))
        {
            foreach (string key in _compileHandlers.Keys)
            {
                if (!TypeLibrary.IsSubClassOf(type, key))
                    continue;

                return true;
            }
        }
        else
        {
            return true;
        }
        
        return false;
    }

    public static CompilerHandler GetHandler(string type)
    {
        if (!_compileHandlers.ContainsKey(type))
        {
            foreach (string key in _compileHandlers.Keys)
            {
                if (!TypeLibrary.IsSubClassOf(type, key))
                    continue;

                return _compileHandlers[key];
            }
        }
        else
        {
            return _compileHandlers[type];
        }

        throw new IndexOutOfRangeException("Unable to find the specified handler");
    }

    static CompilerHandler()
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsSubclassOf(typeof(CompilerHandler)))
            {
                CompilerHandler editor = (CompilerHandler)Activator.CreateInstance(type);
                _compileHandlers.Add(editor.AssetType, editor);
            }
        }
    }

    public abstract string AssetType { get; }
    public abstract void CompileAssetBundle(EbxAssetEntry assetEntry, BundleCallStack bundleEntry, List<EbxAssetEntry> entries);
}

public class WeaponUnlockHandler : CompilerHandler
{
    public override string AssetType => "SoldierWeaponUnlockAsset";
    private static int _sharedBunId;
    private static BundleEntry _sharedBundle;
    
    public override void CompileAssetBundle(EbxAssetEntry assetEntry, BundleCallStack bundleEntry, List<EbxAssetEntry> entries)
    {
        if (assetEntry.Bundles.Contains(_sharedBunId) || assetEntry.AddedBundles.Contains(_sharedBunId))
        {
            RecursiveRemove(entries, assetEntry);
            return;
        }
        
        BundleOperator.AddWhitelistedBundle(_sharedBundle);
        BundleEditor.AddToBundle(assetEntry, BundleOperator.CacheManager.GetCallStack(_sharedBunId));
        foreach (Guid dependency in assetEntry.EnumerateDependencies())
        {
            EbxAssetEntry reference = App.AssetManager.GetEbxEntry(dependency);
            RecursiveAdd(reference, entries);
        }

        entries.Remove(assetEntry);
    }

    static WeaponUnlockHandler()
    {
        _sharedBunId = App.AssetManager.GetBundleId("win32/gameplay/bundles/weaponsbundlecommon");
        _sharedBundle = App.AssetManager.GetBundleEntry(_sharedBunId);
    }

    private void RecursiveRemove(List<EbxAssetEntry> entries, EbxAssetEntry entry)
    {
        entries.Remove(entry);
        foreach (Guid dependency in entry.EnumerateDependencies())
        {
            EbxAssetEntry reference = App.AssetManager.GetEbxEntry(dependency);
            if (!entries.Contains(reference))
                continue;
            
            RecursiveRemove(entries, reference);
        }
    }

    private void RecursiveAdd(EbxAssetEntry assetEntry, List<EbxAssetEntry> entries)
    {
        if (assetEntry.Bundles.Contains(_sharedBunId) || assetEntry.AddedBundles.Contains(_sharedBunId))
        {
            entries.Remove(assetEntry);
            return;
        }

        BundleEditor.AddToBundle(assetEntry, BundleOperator.CacheManager.GetCallStack(_sharedBunId));
        foreach (Guid dependency in assetEntry.EnumerateDependencies())
        {
            EbxAssetEntry referenceEntry = App.AssetManager.GetEbxEntry(dependency);
            
            // We shouldn't add things to this bundle unless its labelled as required
            if (!entries.Contains(referenceEntry))
                continue;
            
            RecursiveAdd(referenceEntry, entries);
        }
        entries.Remove(assetEntry);
    }
}

public class ProfileOptionHandler : CompilerHandler
{
    public override string AssetType => "ProfileOptionData";
    private static int _wsgameconfiguration;
    private static int _defaultsettingswin32;

    public override void CompileAssetBundle(EbxAssetEntry assetEntry, BundleCallStack bundleEntry, List<EbxAssetEntry> entries)
    {
        if (assetEntry.AddedBundles.Contains(_wsgameconfiguration))
        {
            entries.Remove(assetEntry);
            return;
        }
        
        BundleEditor.AddToBundle(assetEntry, BundleOperator.CacheManager.GetCallStack(_wsgameconfiguration));
        BundleEditor.AddToBundle(assetEntry, BundleOperator.CacheManager.GetCallStack(_defaultsettingswin32));
        entries.Remove(assetEntry);
    }

    static ProfileOptionHandler()
    {
        _wsgameconfiguration = App.AssetManager.GetBundleId("win32/gameplay/wsgameconfiguration");
        _defaultsettingswin32 = App.AssetManager.GetBundleId("win32/default_settings_win32");
    }
}