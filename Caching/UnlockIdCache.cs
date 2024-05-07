using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler.Caching;

public class UnlockIdCache
{
    public static int Magic = Utils.HashString("YW_IdCache");
    public static int Version = 1002;
    
    // TODO: Uint instead of int(woopsie)
    public List<int> UnlockIds { get; }
    public Dictionary<string, int> UnlockAssetToId { get; }
    public Dictionary<int, EbxAssetEntry> UnlockAssets { get; }

    #region Cache Writing

    public void GenerateMainCache(FrostyTaskWindow? task = null)
    {
        task?.Update("Caching references...", 0.0);
        NativeWriter writer = new NativeWriter(new FileStream(
            $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_IdCache.cache",
            FileMode.Create));

        #region Header

        writer.Write(Magic);
        writer.Write(Version);

        #endregion

        List<(EbxAssetEntry, int)> unlocks = new List<(EbxAssetEntry, int)>();
        foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx("UnlockAssetBase"))
        {
            EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
            dynamic root = asset.RootObject;
            unlocks.Add((assetEntry, (int)root.Identifier));
        }
        
        writer.Write(unlocks.Count);
        foreach ((EbxAssetEntry, int) unlock in unlocks)
        {
            writer.Write(unlock.Item1.Guid);
            writer.Write(unlock.Item2);
        }

        writer.Dispose();
    }

    #endregion

    #region Cache Reading

    public bool LoadMainCache(ILogger? logger = null)
    {
        logger?.Log("Loading reference cache...");
        
        string path = $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_IdCache.cache";
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

        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            EbxAssetEntry assetEntry = App.AssetManager.GetEbxEntry(reader.ReadGuid());
            int id = reader.ReadInt();
            UnlockAssets.Add(id, assetEntry);
            UnlockAssetToId.Add(assetEntry.Name, id);
            UnlockIds.Add(id);
        }

        reader.Dispose();
        return true;
    }

    #endregion
        
    public void ClearAll()
    {
        UnlockIds.Clear();
        UnlockAssets.Clear();
        UnlockAssetToId.Clear();
    }

    public UnlockIdCache()
    {
        UnlockIds = new List<int>();
        UnlockAssets = new Dictionary<int, EbxAssetEntry>();
        UnlockAssetToId = new Dictionary<string, int>();
    }
}