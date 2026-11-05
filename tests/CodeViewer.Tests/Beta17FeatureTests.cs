using System;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta17FeatureTests
{
    [TestMethod]
    public void AppUpdateService_CompareVersions_RecognizesBeta17NewerThanBeta16()
    {
        var cmp = AppUpdateService.CompareVersions("1.2.0-beta.17", "1.2.0-beta.16");
        Assert.IsTrue(cmp > 0, "v1.2.0-beta.17 must be recognized as newer than v1.2.0-beta.16");
    }
}
