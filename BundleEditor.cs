﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AtlasTexturePlugin;
using BundleCompiler.Caching;
using Frosty.Core.Viewport;
using FrostyEditor;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using MeshSetPlugin.Resources;

namespace BundleCompiler;

public class BundleEditor
{
    private static Dictionary<string, BundleEditor> _bundleEditors = new();

    public static void AddToBundle(EbxAssetEntry assetEntry, BundleCallStack bundleCallStack)
    {
        if (!_bundleEditors.ContainsKey(assetEntry.Type))
        {
            foreach (string key in _bundleEditors.Keys)
            {
                if (TypeLibrary.IsSubClassOf(assetEntry.Type, key))
                {
                    _bundleEditors[key].Bundle(assetEntry, bundleCallStack);
                    _bundleEditors[key].Ebx(assetEntry, bundleCallStack);
                    return;
                }
            }
            
            _bundleEditors["{none}"].Bundle(assetEntry, bundleCallStack);
            _bundleEditors["{none}"].Ebx(assetEntry, bundleCallStack);
            return;
        }
        
        _bundleEditors[assetEntry.Type].Bundle(assetEntry, bundleCallStack);
        _bundleEditors[assetEntry.Type].Ebx(assetEntry, bundleCallStack);
    }

    static BundleEditor()
    {
        _bundleEditors.Add("{none}", new BundleEditor());
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsSubclassOf(typeof(BundleEditor)))
            {
                BundleEditor editor = (BundleEditor)Activator.CreateInstance(type);
                _bundleEditors.Add(editor.AssetType, editor);
            }
        }
    }
    
    public virtual string AssetType => "{none}";
    
    public virtual void Bundle(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
        if (!assetEntry.HasModifiedData)
        {
            BundleOperator.PureBundled.Add(assetEntry.Guid);
        }
        App.AssetManager.ModifyEbx(assetEntry.Name, App.AssetManager.GetEbx(assetEntry));
        assetEntry.AddToBundle(bundleEntry.CallerId);
    }
    
    public virtual void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
        if (BundleOperator.CacheManager.GetNetworkedBundle(bundleEntry.Caller.Name) == null)
            return;
        
        EbxAsset registry = BundleOperator.CacheManager.GetNetworkedBundle(bundleEntry.Caller.Name)!;
        if (registry.Dependencies.Contains(assetEntry.Guid))
            return;

        EbxAsset asset = App.AssetManager.GetEbx(assetEntry);

        List<PointerRef> objects = ((dynamic)registry.RootObject).Objects;
        foreach (dynamic exportedObject in asset.ExportedObjects)
        {
            if (!BundleOperator.CacheManager.BundleCache.NetworkedTypesCache.Contains(exportedObject.GetType().Name))
                continue;
            
            EbxImportReference import = new EbxImportReference()
            {
                FileGuid = asset.FileGuid,
                ClassGuid = exportedObject.GetInstanceGuid().ExportedGuid
            };
            
            PointerRef pointerRef = new PointerRef(import);
            if (objects.Contains(pointerRef))
                continue;
            
            objects.Add(pointerRef);
        }

        EbxAssetEntry netregEntry = App.AssetManager.GetEbxEntry(registry.FileGuid);
        App.AssetManager.ModifyEbx(netregEntry.Name, registry);
        
        registry.AddDependency(assetEntry.Guid);
        netregEntry.ModifiedEntry.DependentAssets.Add(assetEntry.Guid);
    }
}

public class SoundWaveExtension : BundleEditor
{
    public override string AssetType => "SoundWaveAsset";
    public override void Bundle(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
        base.Bundle(assetEntry, bundleEntry);
        EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
        dynamic soundAsset = asset.RootObject;

        foreach (var soundDataChunk in soundAsset.Chunks)
        {
            ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(soundDataChunk.ChunkId);
            // TODO: Work around for me removing this check in AssetManager to force add chunks
            if (chunkEntry.Bundles.Count == 0 && !chunkEntry.IsAdded)
                continue;
            
            chunkEntry.AddToBundle(bundleEntry.CallerId);
            assetEntry.LinkAsset(chunkEntry);
        }
    }

    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
    }
}

public class MovieTextureExtension : BundleEditor
{
    public override string AssetType => "MovieTextureBaseAsset";
    public override void Bundle(EbxAssetEntry entry, BundleCallStack bentry)
    {
        base.Bundle(entry, bentry);

        EbxAsset asset = App.AssetManager.GetEbx(entry);
        dynamic movieAsset = asset.RootObject;

        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(movieAsset.ChunkGuid);
        // TODO: Work around for me removing this check in AssetManager to force add chunks
        if (chunkEntry.Bundles.Count != 0 || chunkEntry.IsAdded)
        {
            chunkEntry.AddToBundle(bentry.CallerId);
        }
        
        entry.LinkAsset(chunkEntry);

        chunkEntry = App.AssetManager.GetChunkEntry(movieAsset.SubtitleChunkGuid);
        if (chunkEntry != null)
        {
            chunkEntry.AddToBundle(bentry.CallerId);
            entry.LinkAsset(chunkEntry);
        }
    }
    
    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
    }
}

public class PathfindingExtension : BundleEditor
{
    public override string AssetType => "PathfindingBlobAsset";
    public override void Bundle(EbxAssetEntry entry, BundleCallStack bentry)
    {
        base.Bundle(entry, bentry);

        EbxAsset asset = App.AssetManager.GetEbx(entry);
        dynamic blobAsset = asset.RootObject;

        if (asset.RootObject.GetType().GetProperty("Blobs") != null)
        {
            foreach (var blob in blobAsset.Blobs)
            {
                ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(blob.BlobId);
                chunkEntry.AddToBundle(bentry.CallerId);
                using (NativeReader nativeReader = new NativeReader(App.AssetManager.GetChunk(chunkEntry)))
                {
                    App.AssetManager.ModifyChunk(chunkEntry.Id, nativeReader.ReadToEnd());
                }
            }
        }
        else
        {
            ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(blobAsset.Blob.BlobId);
            chunkEntry.AddToBundle(bentry.CallerId);
            using (NativeReader nativeReader = new NativeReader(App.AssetManager.GetChunk(chunkEntry)))
            {
                App.AssetManager.ModifyChunk(chunkEntry.Id, nativeReader.ReadToEnd());
            }
        }
    }
        
    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
    }
}

public class AtlasTexureExtension : BundleEditor
{
    public override string AssetType => "AtlasTextureAsset";
    public override void Bundle(EbxAssetEntry entry, BundleCallStack bentry)
    {
        base.Bundle(entry, bentry);

        EbxAsset asset = App.AssetManager.GetEbx(entry);
        dynamic textureAsset = asset.RootObject;

        ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureAsset.Resource);
        resEntry.AddToBundle(bentry.CallerId);

        AtlasTexture texture = App.AssetManager.GetResAs<AtlasTexture>(resEntry);
        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(texture.ChunkId);
        
        // TODO: Work around for me removing this check in AssetManager to force add chunks
        if (chunkEntry.Bundles.Count != 0 || chunkEntry.IsAdded)
        {
            chunkEntry.AddToBundle(bentry.CallerId);
        }

        resEntry.LinkAsset(chunkEntry);
        entry.LinkAsset(resEntry);
    }
        
    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
    }
}

public class MeshExtension : BundleEditor
{
    public override string AssetType => "MeshAsset";
    public override void Bundle(EbxAssetEntry entry, BundleCallStack bentry)
    {
        EbxAsset asset = App.AssetManager.GetEbx(entry);
        dynamic meshAsset = asset.RootObject;

        //Add res to BUNDLES AND LINK
        ResAssetEntry resEntry = App.AssetManager.GetResEntry(meshAsset.MeshSetResource);
        resEntry.AddToBundle(bentry.CallerId);
        entry.LinkAsset(resEntry);

        MeshSet meshSetRes = App.AssetManager.GetResAs<MeshSet>(resEntry);
        App.AssetManager.ModifyRes(resEntry.ResRid, meshSetRes);
        
        //Double check if there are any LODs in the Rigid Mesh, if there are, bundle and link them. Else, just bundle the EBX and move on.
        if (meshSetRes.Lods.Count > 0)
        {
            foreach (MeshSetLod lod in meshSetRes.Lods)
            {
                if (lod.ChunkId != Guid.Empty)
                {
                    ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(lod.ChunkId);
                    // TODO: Work around for me removing this check in AssetManager to force add chunks
                    if (chunkEntry.Bundles.Count == 0 && !chunkEntry.IsAdded)
                        continue;
                    
                    chunkEntry.AddToBundle(bentry.CallerId);
                    resEntry.LinkAsset(chunkEntry);
                }
            }
        }

        if (!MeshVariationDb.IsLoaded)
        {
            MeshVariationDb.LoadVariations();
        }

        //SWBF2 has a fancy setup with SBDs, we need to bundle those too
        if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII))
        {
            ResAssetEntry block = App.AssetManager.GetResEntry(entry.Name.ToLower() + "_mesh/blocks");
            block.AddToBundle(bentry.CallerId);
        }

        base.Bundle(entry, bentry);
    }

    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
        if (BundleOperator.CacheManager.GetMvdbBundle(bundleEntry.Caller.Name) == null)
            return;
        
        EbxAsset mvDb = BundleOperator.CacheManager.GetMvdbBundle(bundleEntry.Caller.Name)!;

        if (mvDb.Dependencies.Contains(assetEntry.Guid))
            return;
        
        EbxAsset asset = App.AssetManager.GetEbx(assetEntry);

        dynamic entry = TypeLibrary.CreateObject("MeshVariationDatabaseEntry");
        foreach (dynamic assetObject in asset.Objects)
        {
            switch (assetObject.GetType().Name)
            {
                case "MeshMaterial":
                {
                    dynamic materialEntry = TypeLibrary.CreateObject("MeshVariationDatabaseMaterial");
                    materialEntry.Material = new PointerRef(new EbxImportReference() { FileGuid = assetEntry.Guid, ClassGuid = assetObject.GetInstanceGuid().ExportedGuid });

                    if (assetObject.Shader.TextureParameters.Count != 0)
                    {
                        materialEntry.TextureParameters = assetObject.Shader.TextureParameters;
                    }
                    else if (MeshVariationDb.GetVariations(assetEntry.Guid) != null)
                    {
                        // We need to get texture params from another mvdb
                        MeshVariation refVariation = MeshVariationDb.GetVariations(assetEntry.Guid).Variations[0];
                        foreach (MeshVariationMaterial mvm in refVariation.Materials) // For each material in the original assets MVDB Entry
                        {
                            if (mvm.MaterialGuid == assetObject.GetInstanceGuid().ExportedGuid) // If it has the same guid
                            {
                                // We then use its texture params as the texture params in the variation
                                foreach (dynamic texParam in (dynamic)mvm.TextureParameters)
                                {
                                    materialEntry.TextureParameters.Add(texParam);
                                    PointerRef value = texParam.Value;
                                    EbxAssetEntry tex = App.AssetManager.GetEbxEntry(value.External.FileGuid);
                                    
                                    // Don't add this texture if its being handled later
                                    if (tex.EnumerateBundles().Any(b => App.AssetManager.GetBundleEntry(b).Type == BundleType.SharedBundle 
                                                                        || bundleEntry.CallsBundle(b)))
                                        continue;
                                    
                                    AddToBundle(tex, bundleEntry);
                                }
                                break;
                            }
                        }
                    }

                    entry.Materials.Add(materialEntry);
                } break;
                default:
                {
                    entry.Mesh = new PointerRef(new EbxImportReference() { FileGuid = assetEntry.Guid, ClassGuid = asset.RootInstanceGuid });
                } break;
            }
        }

        ((dynamic)mvDb.RootObject).Entries.Add(entry);
        EbxAssetEntry mvDbEntry = App.AssetManager.GetEbxEntry(mvDb.FileGuid);
        App.AssetManager.ModifyEbx(mvDbEntry.Name, mvDb);
        mvDb.AddDependency(assetEntry.Guid);
        mvDbEntry.ModifiedEntry.DependentAssets.Add(assetEntry.Guid);
    }
}

public class ObjectVariationExtension : BundleEditor
{
    public override string AssetType => "ObjectVariation";

    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
        if (BundleOperator.CacheManager.GetMvdbBundle(bundleEntry.CallerId) == null)
            return;
        EbxAsset mvdb = BundleOperator.CacheManager.GetMvdbBundle(bundleEntry.CallerId)!;
        dynamic mvdbRoot = mvdb.RootObject;

        dynamic newMvEntry = TypeLibrary.CreateObject("MeshVariationDatabaseEntry"); //Create a new entry for us to add to the MVDB
        
        EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
        dynamic root = asset.RootObject;
        uint hash = root.NameHash;
        newMvEntry.VariationAssetNameHash = hash;

        EbxAssetEntry? meshEntry = null;
        MeshVariation? refVar = null;
        if (MeshVariationDb.FindVariations(hash, true) != null)
        {
            refVar = MeshVariationDb.FindVariations(hash, true).First();
            meshEntry = App.AssetManager.GetEbxEntry(refVar.MeshGuid);
        }
        else
        {
            // TODO: Make use of vbx project to store variations of a mesh in text format
            // We should make a new file format, something like a metadata file which contains stuff like this which the user can specify
            App.Logger.LogWarning("Currently duped object variations aren't supported, therefore this cannot be added to mvdbs. Please tell ywingpilot2!");
        }

        if (meshEntry == null)
        {
            App.Logger.LogError("Could not find mesh for variation {0}", assetEntry.Name);
            return;
        }

        // The variation and mesh entry need to be in the same mvdb
        if (!meshEntry.Bundles.Contains(bundleEntry.CallerId))
        {
            AddToBundle(meshEntry, bundleEntry);
        }

        EbxAsset mesh = App.AssetManager.GetEbx(meshEntry);
        newMvEntry.Mesh = new PointerRef(new EbxImportReference { FileGuid = meshEntry.Guid, ClassGuid = mesh.RootInstanceGuid});

        int idx = -1;
        foreach (dynamic assetObject in asset.Objects)
        {
            idx++;
            if (assetObject.GetType().Name != "MeshMaterialVariation")
                continue;
            
            dynamic newMaterialEntry = TypeLibrary.CreateObject("MeshVariationDatabaseMaterial");
            newMaterialEntry.MaterialVariation = new PointerRef(new EbxImportReference
            {
                FileGuid = assetEntry.Guid,
                ClassGuid = assetObject.GetInstanceGuid().ExportedGuid
            });
            
            if (refVar != null)
            {
                newMaterialEntry.Material = new PointerRef(new EbxImportReference
                { FileGuid = meshEntry.Guid, 
                    ClassGuid = refVar.Materials[idx - 1].MaterialGuid });
            }
            else
            {
                dynamic? mat = mesh.Objects.ElementAtOrDefault(idx);
                if (mat == null)
                {
                    App.Logger.LogError("Unable to find material on {0} for variation {1}", meshEntry.Filename, assetEntry.Filename);
                    continue;
                }
                newMaterialEntry.Material = new PointerRef(new EbxImportReference
                { FileGuid = meshEntry.Guid, 
                    ClassGuid = mat.GetInstanceGuid().ExportedGuid });
            }

            if (assetObject.Shader.TextureParameters.Count != 0)
            {
                newMaterialEntry.TextureParameters = assetObject.Shader.TextureParameters;
            }
            else if (refVar != null)
            {
                MeshVariationMaterial material = refVar.Materials[idx - 1];
                foreach (dynamic texParam in (dynamic)material.TextureParameters)
                {
                    newMaterialEntry.TextureParameters.Add(texParam);
                    PointerRef value = texParam.Value;
                    EbxAssetEntry textureEntry = App.AssetManager.GetEbxEntry(value.External.FileGuid);
                    if (!textureEntry.Bundles.Contains(bundleEntry.CallerId))
                    {
                        AddToBundle(textureEntry, bundleEntry);
                    }
                }
            }

            newMvEntry.Materials.Add(newMaterialEntry);
        }

        mvdbRoot.Entries.Add(newMvEntry);
        EbxAssetEntry mvdbEntry = App.AssetManager.GetEbxEntry(mvdb.FileGuid);
        App.AssetManager.ModifyEbx(mvdbEntry.Name, mvdb);
        mvdb.AddDependency(assetEntry.Guid);
        mvdbEntry.ModifiedEntry.DependentAssets.Add(assetEntry.Guid);
    }
}

public class SvgImageExtension : BundleEditor
{
    public override string AssetType => "SvgImage";
    public override void Bundle(EbxAssetEntry entry, BundleCallStack bentry)
    {
        base.Bundle(entry, bentry);

        EbxAsset asset = App.AssetManager.GetEbx(entry);
        dynamic svgAsset = asset.RootObject;

        ResAssetEntry resEntry = App.AssetManager.GetResEntry(svgAsset.Resource);
        resEntry.AddToBundle(bentry.CallerId);

        entry.LinkAsset(resEntry);
    }
        
    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
    }
}

public class TextureExtension : BundleEditor
{
    public override string AssetType => "TextureBaseAsset";
    public override void Bundle(EbxAssetEntry entry, BundleCallStack bentry)
    {
        base.Bundle(entry, bentry);

        EbxAsset asset = App.AssetManager.GetEbx(entry);
        dynamic textureAsset = asset.RootObject;

        ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureAsset.Resource);
        resEntry.AddToBundle(bentry.CallerId);

        Texture texture = App.AssetManager.GetResAs<Texture>(resEntry);
        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(texture.ChunkId);
        
        // TODO: Work around for me removing this check in AssetManager to force add chunks
        if (chunkEntry.Bundles.Count != 0 || chunkEntry.IsAdded)
        {
            chunkEntry.AddToBundle(bentry.CallerId);
        }
        
        chunkEntry.FirstMip = texture.FirstMip;

        resEntry.LinkAsset(chunkEntry);
        entry.LinkAsset(resEntry);
    }
        
    public override void Ebx(EbxAssetEntry assetEntry, BundleCallStack bundleEntry)
    {
    }
}