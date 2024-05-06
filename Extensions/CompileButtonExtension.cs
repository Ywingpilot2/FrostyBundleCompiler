using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using BundleCompiler.Caching;
using BundleCompiler.Windows;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Managers;

namespace BundleCompiler.Extensions
{
    public class CompileButtonExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Bundle Compilation";
        public override string MenuItemName => "Compile Bundles";
        
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/compile.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand(o =>
        {
            FrostyTaskWindow.Show("Compiling Bundles...", "", task =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                BundleOperator.ClearBundles();
                BundleOperator.CompileBundles(task);
                stopwatch.Stop();
                App.Logger.Log("Compiled bundles in {0}", stopwatch.Elapsed.ToString());
            });
        });
    }
    
    public class CompileBundleExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Bundle Compilation";
        public override string MenuItemName => "Compile Bundle";
        
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/compile.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand(o =>
        {
            List<string> options = new List<string>();
            List<string> fullNames = new List<string>();
            foreach (BundleCallStack callStack in BundleOperator.CacheManager.RootCallStacks)
            {
                if (callStack.Caller.Type != BundleType.SubLevel)
                    continue;
                
                options.Add(callStack.Caller.Name.Split('/').Last());
                fullNames.Add(callStack.Caller.Name);
            }

            string? result = CompileBundleWindow.Show(options);
            if (result == null)
                return;
            
            FrostyTaskWindow.Show("Compiling bundle...", "", task =>
            {
                BundleOperator.ClearBundles();
                // TODO: Why is this causing issues?
                // foreach (BundleCallStack sharedCall in BundleOperator.CacheManager.EnumerateRootCalls(BundleType.SharedBundle))
                // {
                //     BundleOperator.CompileBundle(sharedCall, task);
                // }
                
                foreach (int i in BundleOperator.CacheManager.LevelTypeCache.MenuBundleCache)
                {
                    BundleCallStack menuStack = BundleOperator.CacheManager.GetCallStack(i);
                    BundleOperator.CompileBundle(menuStack, task);
                }
                
                int bunId = App.AssetManager.GetBundleId(fullNames[options.IndexOf(result)]);
                if (bunId == -1)
                    return;

                BundleCallStack callStack = BundleOperator.CacheManager.GetCallStack(bunId);
                BundleOperator.CompileBundle(callStack, task);
            });
        });
    }
    
    public class CompileIdTablesButtonExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Bundle Compilation";
        public override string MenuItemName => "Compile UnlockIdTables";
        
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/compile.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand(o =>
        {
            FrostyTaskWindow.Show("Compiling UnlockIdTables...", "", task =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                BundleOperator.CompileIdTables();
                stopwatch.Stop();
                App.Logger.Log("Compiled UnlockIdTables in {0}", stopwatch.Elapsed.ToString());
            });
        });
    }
    
    public class CompileTableExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Bundle Compilation";
        public override string MenuItemName => "Compile UnlockIdTable";
        
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/compile.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand(o =>
        {
            List<string> options = new List<string>();
            List<string> fullNames = new List<string>();
            foreach (BundleCallStack callStack in BundleOperator.CacheManager.RootCallStacks)
            {
                if (callStack.Caller.Type != BundleType.SubLevel || callStack.Asset?.Type != "LevelData")
                    continue;
                
                options.Add(callStack.Caller.Name.Split('/').Last());
                fullNames.Add(callStack.Caller.Name);
            }

            string? result = CompileBundleWindow.Show(options);
            if (result == null)
                return;
            
            FrostyTaskWindow.Show("Compiling table...", "", task =>
            {
                BundleOperator.ClearBundles();

                foreach (int i in BundleOperator.CacheManager.LevelTypeCache.MenuBundleCache)
                {
                    BundleCallStack menuStack = BundleOperator.CacheManager.GetCallStack(i);
                    BundleOperator.CompileIdTable(menuStack);
                }
                
                int bunId = App.AssetManager.GetBundleId(fullNames[options.IndexOf(result)]);
                if (bunId == -1)
                    return;

                BundleCallStack callStack = BundleOperator.CacheManager.GetCallStack(bunId);
                BundleOperator.CompileIdTable(callStack);
            });
        });
    }
    
    public class ClearButtonExtension : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Bundle Compilation";
        public override string MenuItemName => "Clear Bundles";
        
        public override RelayCommand MenuItemClicked => new RelayCommand(o =>
        {
            FrostyTaskWindow.Show("Clearing Bundles...", "", task =>
            {
                BundleOperator.ClearBundles();
            });
            App.Logger.Log("Cleared bundles");
        });
    }
}