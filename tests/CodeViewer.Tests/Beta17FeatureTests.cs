using System;
using Avalonia.Controls;
using Avalonia.Input;
using CodeViewer.Models;
using CodeViewer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeViewer.Tests;

[TestClass]
public class Beta17FeatureTests
{
    [TestMethod]
    public void AppUpdateService_GetCurrentAppVersion_ReportsBeta17()
    {
        var version = AppUpdateService.GetCurrentAppVersion();
        Assert.AreEqual("1.2.0-beta.17", version);

        var cmp = AppUpdateService.CompareVersions("1.2.0-beta.17", "1.2.0-beta.16");
        Assert.IsTrue(cmp > 0, "v1.2.0-beta.17 must be recognized as newer than v1.2.0-beta.16");
    }

    [TestMethod]
    public void AppSettings_EnableMiddleClickScroll_DefaultsToTrueAndClones()
    {
        var settings = new AppSettings();
        Assert.IsTrue(settings.EnableMiddleClickScroll, "EnableMiddleClickScroll must default to true.");

        var cloned = settings.Clone();
        Assert.IsTrue(cloned.EnableMiddleClickScroll);

        cloned.EnableMiddleClickScroll = false;
        var cloned2 = cloned.Clone();
        Assert.IsFalse(cloned2.EnableMiddleClickScroll);
    }

    [TestMethod]
    public void MiddleClickScroll_InsideDeadZone_ReturnsZeroVelocity()
    {
        var (vx0, vy0) = MiddleClickScrollController.CalculateScrollVelocity(0, 0, deadZone: 10.0);
        Assert.AreEqual(0.0, vx0, 0.0001);
        Assert.AreEqual(0.0, vy0, 0.0001);

        var (vxInside, vyInside) = MiddleClickScrollController.CalculateScrollVelocity(6, -7, deadZone: 10.0);
        Assert.AreEqual(0.0, vxInside, 0.0001);
        Assert.AreEqual(0.0, vyInside, 0.0001);
    }

    [TestMethod]
    public void MiddleClickScroll_ProgressiveVelocity_IsGentleAndCapped()
    {
        // 20px distance (10px excess) -> gentle reading speed
        var (vxSmall, vySmall) = MiddleClickScrollController.CalculateScrollVelocity(0, 20.0, deadZone: 10.0);
        Assert.IsTrue(vySmall > 0, "Downwards movement must produce positive vertical velocity.");
        Assert.IsTrue(vySmall < 2.0, "Small distance must produce gentle velocity (< 2.0 px/tick).");

        // 60px distance (50px excess) -> comfortable scrolling
        var (vxMed, vyMed) = MiddleClickScrollController.CalculateScrollVelocity(0, 60.0, deadZone: 10.0);
        Assert.IsTrue(vyMed > vySmall, "Medium distance must be faster than small distance.");
        Assert.IsTrue(vyMed < 10.0, "Medium distance must remain comfortable (< 10 px/tick).");

        // Extreme distance -> capped at MaxSpeed (28.0)
        var (vxHuge, vyHuge) = MiddleClickScrollController.CalculateScrollVelocity(0, 500.0, deadZone: 10.0, maxSpeed: 28.0);
        Assert.AreEqual(28.0, vyHuge, 0.0001, "Extreme distance must be capped at MaxSpeed.");
    }

    [TestMethod]
    public void MiddleClickScroll_DirectionalCursors_MatchMotionAngle()
    {
        // Deadzone
        Assert.AreEqual(StandardCursorType.SizeAll, MiddleClickScrollController.GetAutoScrollCursor(0, 0, deadZone: 10.0));
        Assert.AreEqual(StandardCursorType.SizeAll, MiddleClickScrollController.GetAutoScrollCursor(5, 5, deadZone: 10.0));

        // Dominant Vertical
        Assert.AreEqual(StandardCursorType.BottomSide, MiddleClickScrollController.GetAutoScrollCursor(0, 40, deadZone: 10.0));
        Assert.AreEqual(StandardCursorType.TopSide, MiddleClickScrollController.GetAutoScrollCursor(0, -40, deadZone: 10.0));

        // Dominant Horizontal
        Assert.AreEqual(StandardCursorType.RightSide, MiddleClickScrollController.GetAutoScrollCursor(40, 0, deadZone: 10.0));
        Assert.AreEqual(StandardCursorType.LeftSide, MiddleClickScrollController.GetAutoScrollCursor(-40, 0, deadZone: 10.0));

        // Diagonals
        Assert.AreEqual(StandardCursorType.BottomRightCorner, MiddleClickScrollController.GetAutoScrollCursor(30, 30, deadZone: 10.0));
        Assert.AreEqual(StandardCursorType.TopLeftCorner, MiddleClickScrollController.GetAutoScrollCursor(-30, -30, deadZone: 10.0));
        Assert.AreEqual(StandardCursorType.TopRightCorner, MiddleClickScrollController.GetAutoScrollCursor(30, -30, deadZone: 10.0));
        Assert.AreEqual(StandardCursorType.BottomLeftCorner, MiddleClickScrollController.GetAutoScrollCursor(-30, 30, deadZone: 10.0));
    }
}
