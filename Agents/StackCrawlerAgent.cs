using System;
using BundleCompiler.Caching;

namespace BundleCompiler.Agents;

/// <summary>
/// This simple agent crawls down a stack and conducts user specified actions
/// </summary>
public class StackCrawlerAgent
{
    public void CrawlDownStack(BundleCallStack rootCall, Action<BundleCallStack> action)
    {
        action.Invoke(rootCall);

        foreach (BundleCallStack callStack in rootCall.Stacks)
        {
            CrawlDownStack(callStack, action);
        }
    }
}