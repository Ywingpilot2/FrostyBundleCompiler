using System;
using System.Runtime.InteropServices;
using Frosty.Core;
using FrostySdk.Interfaces;

namespace BundleCompiler.Extensions;

public class BundleCompilerStartupAction : StartupAction
{
    public override Action<ILogger> Action => action;

    private void action(ILogger logger)
    {
        BundleOperator.Initialize(logger);
        App.Logger.Log("Currently running on: {0}", RuntimeInformation.ProcessArchitecture);
    }
}