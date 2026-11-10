using System;
using GaussianSplatting.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public class LearnedLUTVersionTests
{
    const string Valid = "{\"hu\":[[-190,50]],\"final_linear_rgb\":[[[0,0.2,0.4],[1,0.6,0.8]]]}";

    [Test]
    public void LinearInterpolationClampsEndpointsAndIgnoresInitialColors()
    {
        var root = JObject.Parse(Valid);
        root["initial_linear_rgb"] = new JArray();
        var lut = LearnedLUTVersion.Parse(root.ToString(), "test", 7);
        Assert.AreEqual(7, lut.layer);
        Assert.AreEqual(0f, lut.Evaluate(-300).r);
        Assert.AreEqual(1f, lut.Evaluate(200).r);
        Assert.AreEqual(0.5f, lut.Evaluate(-70).r, 1e-6f);
        Assert.AreEqual(0.4f, lut.Evaluate(-70).g, 1e-6f);
        Assert.AreEqual(0.6f, lut.Evaluate(-70).b, 1e-6f);
    }

    [TestCase(16)]
    [TestCase(32)]
    public void EveryControlAndIntervalIsPreserved(int count)
    {
        var hu = new JArray();
        var rgb = new JArray();
        for (int i = 0; i < count; i++)
        {
            hu.Add(-190 + i * i); // Nonuniform HU spacing.
            rgb.Add(new JArray((i % 3) / 2f, (i % 5) / 4f, (i % 7) / 6f));
        }
        var root = new JObject { ["hu"] = new JArray { hu }, ["final_linear_rgb"] = new JArray { rgb } };
        var lut = LearnedLUTVersion.Parse(root.ToString(), "test", 7);
        Assert.AreEqual(count, lut.hu.Length);
        for (int i = 0; i < count; i++)
        {
            Assert.AreEqual(lut.linearRGB[i].r, lut.Evaluate(lut.hu[i]).r, 1e-6f);
            if (i + 1 < count)
                Assert.AreEqual((lut.linearRGB[i].r + lut.linearRGB[i + 1].r) / 2,
                    lut.Evaluate((lut.hu[i] + lut.hu[i + 1]) / 2).r, 1e-6f);
        }
    }

    [TestCase("{}")] // Missing fields.
    [TestCase("{\"hu\":[[-190,-190]],\"final_linear_rgb\":[[[0,0,0],[1,1,1]]]}")]
    [TestCase("{\"hu\":[[50,-190]],\"final_linear_rgb\":[[[0,0,0],[1,1,1]]]}")]
    [TestCase("{\"hu\":[[-190,50]],\"final_linear_rgb\":[[[0,0,0]]]}")]
    [TestCase("{\"hu\":[[-190,50]],\"final_linear_rgb\":[[[0,0],[1,1,1]]]}")]
    [TestCase("{\"hu\":[[-190,50]],\"final_linear_rgb\":[[[0,0,0],[1.1,1,1]]]}")]
    [TestCase("{\"hu\":[[-190,50]],\"final_linear_rgb\":[[[0,0,0],[-0.1,1,1]]]}")]
    [TestCase("{\"hu\":[[-190,NaN]],\"final_linear_rgb\":[[[0,0,0],[1,1,1]]]}")]
    [TestCase("{\"hu\":[[-190,50]],\"final_linear_rgb\":[[[0,0,0],[1e99,1,1]]]}")]
    [TestCase("{\"hu\":[[-190,50]],\"final_linear_rgb\":[[[0,0,0],[\"1\",1,1]]]}")]
    [TestCase("{\"hu\":[[-190,50],[-190,50]],\"final_linear_rgb\":[[[0,0,0],[1,1,1]]]}")]
    public void InvalidControlsAreRejected(string json)
    {
        Assert.Throws<ArgumentException>(() => LearnedLUTVersion.Parse(json, "invalid", 7));
    }

    [Test]
    public void OptionalLayerMustMatchAndBomIsAccepted()
    {
        var root = JObject.Parse(Valid);
        root["layer"] = 4;
        Assert.Throws<ArgumentException>(() => LearnedLUTVersion.Parse(root.ToString(), "wrong", 7));
        root["layer"] = 7;
        Assert.AreEqual(7, LearnedLUTVersion.Parse("\uFEFF" + root, "ok", 7).layer);
    }

    [Test]
    public void MalformedDuplicateTrailingAndOversizedJsonAreRejected()
    {
        Assert.Catch(() => LearnedLUTVersion.Parse("not JSON", "invalid", 7));
        Assert.Catch(() => LearnedLUTVersion.Parse(Valid + "{}", "invalid", 7));
        Assert.Catch(() => LearnedLUTVersion.Parse("{\"hu\":[],\"hu\":[]}", "invalid", 7));
        Assert.Throws<ArgumentException>(() => LearnedLUTVersion.Parse(new string(' ', 1024 * 1024 + 1), "invalid", 7));
    }
}
