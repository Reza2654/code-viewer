using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta16FeatureTests
{
    [TestMethod]
    public void MiddleClickScroll_InsideDeadzone_ReturnsZeroVelocity()
    {
        var (vx1, vy1) = MiddleClickScrollController.CalculateScrollVelocity(0, 0);
        Assert.AreEqual(0.0, vx1, 0.0001);
        Assert.AreEqual(0.0, vy1, 0.0001);

        var (vx2, vy2) = MiddleClickScrollController.CalculateScrollVelocity(5.0, -7.0, deadZone: 8.0);
        Assert.AreEqual(0.0, vx2, 0.0001);
        Assert.AreEqual(0.0, vy2, 0.0001);
    }

    [TestMethod]
    public void MiddleClickScroll_OutsideDeadzone_CalculatesProgressiveVelocity()
    {
        var (vxRight, vyZero) = MiddleClickScrollController.CalculateScrollVelocity(28.0, 0.0, deadZone: 8.0);
        Assert.IsTrue(vxRight > 0, "Moving right must yield positive horizontal velocity.");
        Assert.AreEqual(0.0, vyZero, 0.0001);

        var (vxLeft, vyZero2) = MiddleClickScrollController.CalculateScrollVelocity(-28.0, 0.0, deadZone: 8.0);
        Assert.IsTrue(vxLeft < 0, "Moving left must yield negative horizontal velocity.");
        Assert.AreEqual(0.0, vyZero2, 0.0001);

        var (vxZero3, vyDown) = MiddleClickScrollController.CalculateScrollVelocity(0.0, 38.0, deadZone: 8.0);
        Assert.AreEqual(0.0, vxZero3, 0.0001);
        Assert.IsTrue(vyDown > 0, "Moving down must yield positive vertical velocity.");

        var (vxZero4, vyUp) = MiddleClickScrollController.CalculateScrollVelocity(0.0, -38.0, deadZone: 8.0);
        Assert.AreEqual(0.0, vxZero4, 0.0001);
        Assert.IsTrue(vyUp < 0, "Moving up must yield negative vertical velocity.");

        // Distance acceleration: further distance must yield higher speed
        var (_, vyFast) = MiddleClickScrollController.CalculateScrollVelocity(0.0, 100.0, deadZone: 8.0);
        Assert.IsTrue(vyFast > vyDown, "Further distance from anchor must produce faster velocity.");
    }

    [TestMethod]
    public void MiddleClickScroll_GetAutoScrollCursor_ReturnsDirectionalCursors()
    {
        // Inside deadzone -> SizeAll (Omnidirectional anchor)
        Assert.AreEqual(StandardCursorType.SizeAll, MiddleClickScrollController.GetAutoScrollCursor(0, 0));
        Assert.AreEqual(StandardCursorType.SizeAll, MiddleClickScrollController.GetAutoScrollCursor(4, -4));

        // Primarily vertical
        Assert.AreEqual(StandardCursorType.BottomSide, MiddleClickScrollController.GetAutoScrollCursor(0, 50));
        Assert.AreEqual(StandardCursorType.TopSide, MiddleClickScrollController.GetAutoScrollCursor(0, -50));

        // Primarily horizontal
        Assert.AreEqual(StandardCursorType.RightSide, MiddleClickScrollController.GetAutoScrollCursor(50, 0));
        Assert.AreEqual(StandardCursorType.LeftSide, MiddleClickScrollController.GetAutoScrollCursor(-50, 0));

        // Diagonals
        Assert.AreEqual(StandardCursorType.BottomRightCorner, MiddleClickScrollController.GetAutoScrollCursor(50, 50));
        Assert.AreEqual(StandardCursorType.TopLeftCorner, MiddleClickScrollController.GetAutoScrollCursor(-50, -50));
        Assert.AreEqual(StandardCursorType.TopRightCorner, MiddleClickScrollController.GetAutoScrollCursor(50, -50));
        Assert.AreEqual(StandardCursorType.BottomLeftCorner, MiddleClickScrollController.GetAutoScrollCursor(-50, 50));
    }

    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsBeta16()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        Assert.AreEqual("1.2.0-beta.16", version);

        var cmp = AppUpdateService.CompareVersions("1.2.0-beta.16", "1.2.0-beta.15");
        Assert.IsTrue(cmp > 0, "v1.2.0-beta.16 must be recognized as newer than v1.2.0-beta.15");
    }

    [TestMethod]
    public async Task RecentFilesService_FastOpenPruning_LoadsCleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CodeViewer_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = new RecentFilesService(10);
            var items = await service.GetRecentFilesAsync();
            Assert.IsNotNull(items);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
