using System.Collections.Generic;
using NUnit.Framework;

public class FirstDrawGuideTests
{
    [Test]
    public void ShowsOncePerCharacter()
    {
        var shown = new HashSet<string>();
        Assert.IsTrue(FirstDrawGuideMemory.ShouldShowFor("na", shown));
        shown.Add("na");
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor("na", shown));
    }

    [Test]
    public void IsCaseInsensitiveAndIgnoresBlanks()
    {
        var shown = new HashSet<string> { "na" };
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor("NA", shown));
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor("  ", shown));
        Assert.IsFalse(FirstDrawGuideMemory.ShouldShowFor(null, shown));
    }
}
