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

public class BundleCache
{
    public static int Magic = Utils.HashString("YW_BundleCache");
    public static int Version = 1002;
    
    public Dictionary<string, EbxAssetEntry> NetworkedBundles { get; }
    public Dictionary<string, EbxAssetEntry> VariationDatabases { get; }
    public List<string> NetworkedTypesCache { get; }

    #region Cache Writing

    public void GenerateMainCache(FrostyTaskWindow? task = null)
    {
        if (!MeshVariationDb.IsLoaded)
        {
            MeshVariationDb.LoadVariations(task);
        }
            
        NativeWriter writer = new NativeWriter(new FileStream(
            $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Bundles.cache",
            FileMode.Create));

        #region Header

        writer.Write(Magic);
        writer.Write(Version);

        #endregion

        #region Networking

        task?.Update("Marking Network Registries...", 0.0);
        int idx = 0;
        List<EbxAssetEntry> networkRegisters = App.AssetManager.EnumerateEbx("NetworkRegistryAsset").ToList();
        writer.Write(networkRegisters.Count);

        foreach (EbxAssetEntry networkCache in networkRegisters)
        {
            writer.Write(networkCache.Bundles[0]);
            writer.WriteNullTerminatedString(networkCache.Name);
            
            idx++;
            task?.Update(null, (idx / (float)networkRegisters.Count) * 100.0);
        }

        #endregion
            
        #region Variations(marking)

        task?.Update("Marking Variation Databases...", 0.0);
        List<EbxAssetEntry> variationDatabases = App.AssetManager.EnumerateEbx("MeshVariationDatabase").ToList();
        writer.Write(variationDatabases.Count);

        idx = 0;
        foreach (EbxAssetEntry variationDatabase in variationDatabases)
        {
            writer.Write(variationDatabase.Bundles[0]);
            writer.WriteNullTerminatedString(variationDatabase.Name);
                
            idx++;
            task?.Update(null, (idx / (float)variationDatabases.Count) * 100.0);
        }

        #endregion

        writer.Dispose();
    }

    #endregion

    #region Cache Reading

    public bool LoadMainCache(ILogger? logger = null)
    {
        string path = $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Bundles.cache";
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

        #region Networking

        logger?.Log("Reading networking cache...");
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            BundleEntry bundleEntry = App.AssetManager.GetBundleEntry(reader.ReadInt());
            EbxAssetEntry assetEntry = App.AssetManager.GetEbxEntry(reader.ReadNullTerminatedString());

            NetworkedBundles.Add(bundleEntry.Name, assetEntry);
        }

        string networkedTypesPath = $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_NetworkedTypes.txt";
        if (File.Exists(networkedTypesPath))
        {
            StreamReader txtReader = new StreamReader(networkedTypesPath);
            string? line = txtReader.ReadLine();
            while (line != null)
            {
                NetworkedTypesCache.Add(line);
                line = txtReader.ReadLine();
            }
        }

        #endregion
            
        #region Variations

        logger?.Log("Reading variation cache...");
        count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            BundleEntry bundleEntry = App.AssetManager.GetBundleEntry(reader.ReadInt());
            EbxAssetEntry assetEntry = App.AssetManager.GetEbxEntry(reader.ReadNullTerminatedString());

            VariationDatabases.Add(bundleEntry.Name, assetEntry);
        }

        #endregion

        reader.Dispose();
        return true;
    }

    #endregion
        
    public void ClearAll()
    {
        NetworkedBundles.Clear();
        VariationDatabases.Clear();
        NetworkedTypesCache.Clear();
    }

    public BundleCache()
    {
        NetworkedBundles = new Dictionary<string, EbxAssetEntry>();
        VariationDatabases = new Dictionary<string, EbxAssetEntry>();
        NetworkedTypesCache = new List<string>();
    }
}