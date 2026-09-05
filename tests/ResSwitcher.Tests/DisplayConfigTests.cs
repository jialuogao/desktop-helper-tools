using ResSwitcher.Core;
using Xunit;

namespace ResSwitcher.Tests;

/// <summary>现代显示配置 API 的纯几何变换测试。</summary>
public class DisplayConfigTests
{
    [Theory]
    [InlineData(0x0001FFFFu, 0x0001u)]
    [InlineData(0x00030003u, 0x0003u)]
    [InlineData(0xFFFFFFFFu, 0xFFFFu)]
    public void DecodeSourceModeIndex_UsesVirtualModeLowWord(uint encoded, uint expected)
    {
        Assert.Equal(expected, DisplayApi.DecodeSourceModeIndex(encoded));
    }

    [Fact]
    public void RebasePositions_MovesTargetToOriginAndPreservesRelativeOffsets()
    {
        var result = DisplayApi.RebasePositions(
            [(0, 0), (1920, 120), (-1600, 60)], 1);

        Assert.Equal([( -1920, -120), (0, 0), (-3520, -60)], result);
    }

    [Fact]
    public void RebasePositions_RejectsInvalidTargetIndex()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DisplayApi.RebasePositions([(0, 0)], 1));
    }

    [Fact]
    public void Resolution_EqualityAndToString_BehavesCorrectly()
    {
        var r1 = new Resolution(1920, 1080);
        var r2 = new Resolution(1920, 1080);
        Assert.Equal(r1, r2);
        Assert.Equal("1920 × 1080", r1.ToString());
    }
}
