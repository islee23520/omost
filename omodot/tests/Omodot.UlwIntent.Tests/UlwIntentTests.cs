using Omodot.UlwIntent;
using Xunit;

namespace Omodot.UlwIntent.Tests;

public class UlwIntentTests
{
    [Fact]
    public void GetUlwIntentPrompt_ReturnsExpectedPrompts()
    {
        Assert.Equal("ULTRAWORK MODE ENABLED!", UlwIntentDetector.GetUlwIntentPrompt(UlwIntentType.Ultrawork));
        Assert.Equal("HYPERPLAN MODE ENABLED!", UlwIntentDetector.GetUlwIntentPrompt(UlwIntentType.Hyperplan));
        Assert.Equal("HYPERPLAN ULTRAWORK MODE ENABLED!", UlwIntentDetector.GetUlwIntentPrompt(UlwIntentType.HyperplanUltrawork));
    }

    [Fact]
    public void DetectUlwIntent_DetectsUltraworkAliasesOutsideCodeSpans()
    {
        var result = UlwIntentDetector.DetectUlwIntent("please ulw this");
        var intent = Assert.Single(result);
        Assert.Equal(UlwIntentType.Ultrawork, intent.Type);
        Assert.Equal("ULTRAWORK MODE ENABLED!", intent.Prompt);
    }

    [Fact]
    public void DetectUlwIntent_IgnoresUltraworkInsideInlineCode()
    {
        var result = UlwIntentDetector.DetectUlwIntent("`ulw` only in code");
        Assert.Empty(result);
    }

    [Fact]
    public void DetectUlwIntent_DetectsHyperplanAliasesWithoutUltrawork()
    {
        var result = UlwIntentDetector.DetectUlwIntent("please hpp this");
        var intent = Assert.Single(result);
        Assert.Equal(UlwIntentType.Hyperplan, intent.Type);
        Assert.Equal("HYPERPLAN MODE ENABLED!", intent.Prompt);
    }

    [Fact]
    public void DetectUlwIntent_PrefersCombinedHyperplanUltraworkForAdjacentAliases()
    {
        var result = UlwIntentDetector.DetectUlwIntent("hyperplan ulw");
        var intent = Assert.Single(result);
        Assert.Equal(UlwIntentType.HyperplanUltrawork, intent.Type);
        Assert.Equal("HYPERPLAN ULTRAWORK MODE ENABLED!", intent.Prompt);
    }

    [Fact]
    public void RemoveCode_RemovesFencedAndInlineCode()
    {
        var result = UlwIntentDetector.RemoveCode("```ulw\nignore\n``` run ulw");
        Assert.Equal(" run ulw", result);
    }
}
