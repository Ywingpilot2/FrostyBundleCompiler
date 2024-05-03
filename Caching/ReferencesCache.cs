using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frosty.Core;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler.Caching;

public class ReferenceCache
{
    public static int Magic = Utils.HashString("YW_RefCache");
    public static int Version = 1002;
    
    internal Dictionary<Guid, List<EbxImportReference>> NetworkReferences { get; }
    internal Dictionary<string, List<EbxImportReference>> NetworkStrReferences { get; }
    internal Dictionary<int, List<EbxImportReference>> NetworkBunReferences { get; }

    #region Cache Writing

    public void GenerateMainCache(FrostyTaskWindow? task = null)
    {
        task?.Update("Caching references...", 0.0);
        NativeWriter writer = new NativeWriter(new FileStream(
            $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Reference.cache",
            FileMode.Create));

        #region Header

        writer.Write(Magic);
        writer.Write(Version);

        #endregion

        List<(Guid, List<EbxImportReference>)> cache = new List<(Guid, List<EbxImportReference>)>();
        List<EbxAssetEntry> networkRegisters = App.AssetManager.EnumerateEbx("NetworkRegistryAsset").ToList();

        for (var i = 0; i < networkRegisters.Count; i++)
        {
            EbxAssetEntry assetEntry = networkRegisters[i];
            EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
            List<EbxImportReference> refs = new List<EbxImportReference>();

            List<PointerRef> objects = ((dynamic)asset.RootObject).Objects;
            Parallel.ForEach(objects, objRef =>
            {
                if (!refs.Contains(objRef.External))
                {
                    lock (refs)
                    {
                        refs.Add(objRef.External);
                    }
                }
            });
            
            cache.Add((assetEntry.Guid, refs));
            
            task?.Update("Finding Network Registry references...", (i / (float)networkRegisters.Count) * 100.0);
        }

        task?.Update("Caching Network Registry references...", 0.0);
        writer.Write(cache.Count);
        for (var i = 0; i < cache.Count; i++)
        {
            Guid reg = cache[i].Item1;
            List<EbxImportReference> references = cache[i].Item2;
            
            writer.Write(reg);
            writer.Write(references.Count);
            foreach (EbxImportReference importReference in references)
            {
                writer.Write(importReference.FileGuid);
                writer.Write(importReference.ClassGuid);
            }

            task?.Update("Caching Network Registry references...", (i / (float)cache.Count) * 100.0);
        }

        writer.Dispose();
    }

    #endregion

    #region Cache Reading

    public bool LoadMainCache(ILogger? logger = null)
    {
        logger?.Log("Loading reference cache...");
        
        string path = $@"{AppDomain.CurrentDomain.BaseDirectory}Caches\{ProfilesLibrary.ProfileName}_Reference.cache";
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
            Guid regId = reader.ReadGuid();
            int regCount = reader.ReadInt();
            List<EbxImportReference> refs = new List<EbxImportReference>();

            for (int j = 0; j < regCount; j++)
            {
                Guid fileGuid = reader.ReadGuid();
                Guid classGuid = reader.ReadGuid();
                refs.Add(new EbxImportReference {FileGuid = fileGuid, ClassGuid = classGuid});
            }
            
            NetworkReferences.Add(regId, refs);
            NetworkStrReferences.Add(App.AssetManager.GetEbxEntry(regId).Name, refs);
            NetworkBunReferences.Add(App.AssetManager.GetEbxEntry(regId).Bundles[0], refs);
        }

        reader.Dispose();
        return true;
    }

    #endregion
        
    public void ClearAll()
    {
        NetworkReferences.Clear();
        NetworkStrReferences.Clear();
        NetworkBunReferences.Clear();
    }

    public ReferenceCache()
    {
        NetworkReferences = new Dictionary<Guid, List<EbxImportReference>>();
        NetworkStrReferences = new Dictionary<string, List<EbxImportReference>>();
        NetworkBunReferences = new Dictionary<int, List<EbxImportReference>>();
    }
}