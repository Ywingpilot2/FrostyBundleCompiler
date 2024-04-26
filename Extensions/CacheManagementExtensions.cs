using System.Windows.Media;
using Frosty.Core;
using Frosty.Core.Windows;

namespace BundleCompiler.Extensions;

public class GenerateCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Compilation";
    public override string MenuItemName => "Generate Cache";

    public override RelayCommand MenuItemClicked => new RelayCommand(o =>
    {
        FrostyTaskWindow.Show("Generating cache...", "", task =>
        {
            BundleOperator.CacheManager.GenerateCache(task);
        });
    });
}

public class ReloadCacheExtension : MenuExtension
{
    public override string TopLevelMenuName => "Tools";
    public override string SubLevelMenuName => "Bundle Compilation";
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