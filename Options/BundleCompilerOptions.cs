using Frosty.Core;
using FrostySdk.Attributes;

namespace BundleCompiler.Options;

public class BundleCompilerOptions : OptionsExtension
{
    [DisplayName("Compile Multiplayer")]
    public bool BundleMulti { get; set; }
    
    [DisplayName("Compile Singleplayers")]
    [Description("Compile singleplayer levels")]
    public bool BundleSingle { get; set; }
    
    [DisplayName("Compile Menus")]
    public bool BundleMenu { get; set; }

    public override void Load()
    {
        BundleMulti = Config.Get("BundleCompiler_Multi", false);
        BundleSingle = Config.Get("BundleCompiler_Single", true);
        BundleMenu = Config.Get("BundleCompiler_Menu", true);
    }

    public override void Save()
    {
        Config.Add("BundleCompiler_Multi", BundleMulti);
        Config.Add("BundleCompiler_Single", BundleSingle);
        Config.Add("BundleCompiler_Menu", BundleMenu);
    }
}