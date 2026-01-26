using nathanbutlerDEV.libopx.Core;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Core;

public class MxfIndexTests
{
    [Fact]
    public void GetEditUnitOffset_ConstantByteSize_CalculatesCorrectly()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 100,
            IsConstantByteSize = true,
            ConstantEditUnitByteCount = 500,
        };

        // Act & Assert
        Assert.Equal(1000L, index.GetEditUnitOffset(0));
        Assert.Equal(1500L, index.GetEditUnitOffset(1));
        Assert.Equal(2000L, index.GetEditUnitOffset(2));
        Assert.Equal(50500L, index.GetEditUnitOffset(99));
    }

    [Fact]
    public void GetEditUnitOffset_VariableByteSize_UsesStreamOffsets()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 3,
            IsConstantByteSize = false,
            StreamOffsets = [0, 512, 1536],
        };

        // Act & Assert
        Assert.Equal(1000L, index.GetEditUnitOffset(0));
        Assert.Equal(1512L, index.GetEditUnitOffset(1));
        Assert.Equal(2536L, index.GetEditUnitOffset(2));
    }

    [Fact]
    public void GetEditUnitOffset_OutOfRange_ThrowsException()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 10,
            IsConstantByteSize = true,
            ConstantEditUnitByteCount = 500,
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => index.GetEditUnitOffset(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.GetEditUnitOffset(10));
    }

    [Fact]
    public void GetSystemPacketOffset_AddsKlvKeySize()
    {
        // Arrange
        var index = new MxfIndex
        {
            EditRate = (25, 1),
            BodyPartitionOffset = 1000,
            EditUnitCount = 10,
            IsConstantByteSize = true,
            ConstantEditUnitByteCount = 500,
        };

        // Act
        var offset = index.GetSystemPacketOffset(0);

        // Assert - Should add 16 bytes for KLV key
        Assert.Equal(1016L, offset);
    }
}
