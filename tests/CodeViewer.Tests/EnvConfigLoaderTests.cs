using System.IO;
using CodeViewer.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class EnvConfigLoaderTests
{
    [TestMethod]
    public void LoadConfig_ReturnsValidEditorConfiguration()
    {
        var config = EnvConfigLoader.LoadConfig();

        Assert.IsNotNull(config);
        Assert.IsTrue(config.DefaultFontSize >= 8.0 && config.DefaultFontSize <= 36.0);
        Assert.IsTrue(config.MaxRecentFiles > 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(config.DefaultFontFamily));
    }
}
