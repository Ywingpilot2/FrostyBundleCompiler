using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BundleCompiler.Agents;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostyEditor;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler.Caching;

public class LevelTypeCache
{
    public static int Magic = Utils.HashString("YW_LevelCache");
    public static int Version = 1002;
    
    public Dictionary<string, List<int>> InclusionSettingCache = new();
    
    public List<int> SingleplayerBundleCache = new();
    public List<int> MultiplayerBundleCache = new();
    public List<int> MenuBundleCache = new();

    #region Cache Writing
    
    public void GenerateMainCache(FrostyTaskWindow? task = null)
    {
        if (!MeshVariationDb.IsLoaded)
        {
            MeshVariationDb.LoadVariations(task);
        }
            
        NativeWriter writer = new NativeWriter(new FileStream(
            $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Level.cache",
            FileMode.Create));

        #region Header

        writer.Write(Magic);
        writer.Write(Version);

        #endregion

        #region Inclusion mapping
        
        task?.Update("Tracking bundle to inclusion setting...", 0.0);
        Dictionary<string, List<int>> inclusions = new Dictionary<string, List<int>>();
        foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx("WorldPartInclusion"))
        {
            EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
            foreach (dynamic assetObject in asset.Objects)
            {
                if (assetObject.GetType().Name != "WorldPartInclusionCriterion")
                    continue;

                foreach (CString option in assetObject.Options)
                {
                    if (option.IsNull())
                        continue;
                    
                    inclusions.Add(option.ToString(), new List<int>());
                }
            }
        }

        foreach (BundleEntry bundleEntry in App.AssetManager.EnumerateBundles(BundleType.SubLevel))
        {
            if (bundleEntry.Blueprint == null)
                continue;

            EbxAsset asset = App.AssetManager.GetEbx(bundleEntry.Blueprint);
            int bunId = App.AssetManager.GetBundleId(bundleEntry);
            foreach (dynamic assetObject in asset.Objects)
            {
                if (assetObject.GetType().Name != "SubWorldInclusionSetting")
                    continue;

                foreach (CString enabledOption in assetObject.EnabledOptions)
                {
                    if (enabledOption.IsNull())
                        continue;
                    
                    if (!inclusions.ContainsKey(enabledOption.ToString()))
                        continue;
                    
                    inclusions[enabledOption.ToString()].Add(bunId);
                }
            }
        }

        writer.Write(inclusions.Count);
        foreach (KeyValuePair<string,List<int>> valuePair in inclusions)
        {
            writer.WriteNullTerminatedString(valuePair.Key);
            writer.Write(valuePair.Value.Count);
            foreach (int bunId in valuePair.Value)
            {
                writer.Write(bunId);
            }
        }

        #endregion

        #region Level type

        List<int> menuBundles = new List<int>();
        List<int> singleBundles = new List<int>();
        List<int> multiBundles = new List<int>();
        task?.Update("Marking level bundle types...", 0.0);
        foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx("LevelData"))
        {
            EbxAssetEntry descriptionEntry = App.AssetManager.GetEbxEntry($"{assetEntry.Name}/Description");
            EbxAsset asset = App.AssetManager.GetEbx(descriptionEntry);
            dynamic root = asset.RootObject;

            if (root.Description.IsMenu)
            {
                menuBundles.Add(assetEntry.Bundles[0]);
            }
            else if (root.Description.IsCoop)
            {
                singleBundles.Add(assetEntry.Bundles[0]);
            }
            else
            {
                multiBundles.Add(assetEntry.Bundles[0]);
            }
        }
        
        writer.Write(menuBundles.Count);
        foreach (int menuBundle in menuBundles)
        {
            writer.Write(menuBundle);
        }
        
        writer.Write(singleBundles.Count);
        foreach (int menuBundle in singleBundles)
        {
            writer.Write(menuBundle);
        }
        
        writer.Write(multiBundles.Count);
        foreach (int menuBundle in multiBundles)
        {
            writer.Write(menuBundle);
        }

        #endregion

        writer.Dispose();
    }

    #endregion

    #region Cache Reading

    public bool LoadMainCache(ILogger? logger = null)
    {
        string path = $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Level.cache";
        if (!File.Exists(path))
        {
            App.Logger.LogWarning("It appears you haven't generated cache yet, consider generating it?\n(Menu/Tools/Bundle Compiler/Generate Cache)");
            return false;
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

        #region Inclusion Cache

        logger?.Log("Loading inclusion cache...");
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            string inclusion = reader.ReadNullTerminatedString();
            List<int> options = new List<int>();
            int opCount = reader.ReadInt();
            for (int j = 0; j < opCount; j++)
            {
                options.Add(reader.ReadInt());
            }
            
            InclusionSettingCache.Add(inclusion, options);
        }

        #endregion

        #region Level Type Cache

        count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            MenuBundleCache.Add(reader.ReadInt());
        }
        
        count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            SingleplayerBundleCache.Add(reader.ReadInt());
        }
        
        count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            MultiplayerBundleCache.Add(reader.ReadInt());
        }

        #endregion

        reader.Dispose();
        return true;
    }

    #endregion
        
    public void ClearAll()
    {
        InclusionSettingCache.Clear();
        MenuBundleCache.Clear();
        SingleplayerBundleCache.Clear();
        MultiplayerBundleCache.Clear();
    }
}