using System.Windows.Media;
using Frosty.Core;
using Frosty.Core.Windows;

namespace BundleCompiler.Extensions;

public class GenerateCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Cache";
    public override string MenuItemName => "Generate Cache";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        FrostyTaskWindow.Show("Generating cache...", "", task =>
        {
            BundleOperator.CacheManager.GenerateCache(task);
        });
    });
}

public class GenerateStackCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Cache";
    public override string MenuItemName => "Generate Callstack Cache";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        FrostyTaskWindow.Show("Generating cache...", "", task =>
        {
            BundleOperator.CacheManager.ClearCache();
            BundleOperator.CacheManager.CallStackCache.GenerateMainCache(task);
            BundleOperator.CacheManager.LoadCache();
        });
    });
}

public class GenerateBundleCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Cache";
    public override string MenuItemName => "Generate Bundle Cache";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        FrostyTaskWindow.Show("Generating cache...", "", task =>
        {
            BundleOperator.CacheManager.ClearCache();
            BundleOperator.CacheManager.BundleCache.GenerateMainCache(task);
            BundleOperator.CacheManager.LoadCache();
        });
    });
}

public class GenerateTypeCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Cache";
    public override string MenuItemName => "Generate Level Type Cache";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        FrostyTaskWindow.Show("Generating cache...", "", task =>
        {
            BundleOperator.CacheManager.ClearCache();
            BundleOperator.CacheManager.LevelTypeCache.GenerateMainCache(task);
            BundleOperator.CacheManager.LoadCache();
        });
    });
}

public class GenerateRefCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Cache";
    public override string MenuItemName => "Generate Reference Cache";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        FrostyTaskWindow.Show("Generating cache...", "", task =>
        {
            BundleOperator.CacheManager.ClearCache();
            BundleOperator.CacheManager.ReferenceCache.GenerateMainCache(task);
            BundleOperator.CacheManager.LoadCache();
        });
    });
}

public class ReloadCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Cache";
    public override string MenuItemName => "Reload Cache";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        BundleOperator.CacheManager.ClearCache();
        FrostyTaskWindow.Show("Reloading cache...", "", task =>
        {
            BundleOperator.CacheManager.ClearCache();
            BundleOperator.CacheManager.LoadCache(task.TaskLogger);
        });
    });
}