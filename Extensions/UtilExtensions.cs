using System;
using Frosty.Core;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;

namespace BundleCompiler.Extensions;

public class GenerateUnlockIdsExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Utils";
    public override string MenuItemName => "Generate Unlock Ids";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        foreach (EbxAssetEntry assetEntry in App.AssetManager.EnumerateEbx("UnlockAssetBase", modifiedOnly:true))
        {
            if (BundleOperator.CacheManager.IdCache.UnlockAssetToId.ContainsKey(assetEntry.Name))
                continue;

            EbxAsset asset = App.AssetManager.GetEbx(assetEntry);
            dynamic root = asset.RootObject;
            uint id = root.Identifier;
            
            if (BundleOperator.CacheManager.IdCache.UnlockIds.Contains((int)id))
            {
                uint newId = (uint)Utils.HashString(assetEntry.Name);
                if (BundleOperator.CacheManager.IdCache.UnlockIds.Contains((int)newId))
                {
                    Random rng = new Random();
                    newId = (uint)rng.Next(0, int.MaxValue);
                }
                
                root.Identifier = newId;
                App.AssetManager.ModifyEbx(assetEntry.Name, asset);
            }
        }
    });
}