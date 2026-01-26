using nathanbutlerDEV.libopx.Core;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Core;

public class PartitionPackTests
{
    [Fact]
    public void Parse_ValidData_ExtractsFields()
    {
        // Arrange - Minimal 64-byte partition pack (big-endian)
        var data = new byte[64];

        // Major version = 1 (bytes 0-1, big-endian)
        data[0] = 0x00; data[1] = 0x01;
        // Minor version = 3 (bytes 2-3, big-endian)
        data[2] = 0x00; data[3] = 0x03;
        // KAG size = 512 (bytes 4-7, big-endian: 0x00000200)
        data[4] = 0x00; data[5] = 0x00; data[6] = 0x02; data[7] = 0x00;
        // This partition = 0 (bytes 8-15)
        // Previous partition = 0 (bytes 16-23)
        // Footer partition = 0x1000000 (bytes 24-31, big-endian)
        data[24] = 0x00; data[25] = 0x00; data[26] = 0x00; data[27] = 0x00;
        data[28] = 0x01; data[29] = 0x00; data[30] = 0x00; data[31] = 0x00;

        // Act
        var pack = PartitionPack.Parse(data);

        // Assert
        Assert.Equal((ushort)1, pack.MajorVersion);
        Assert.Equal((ushort)3, pack.MinorVersion);
        Assert.Equal(512u, pack.KagSize);
        Assert.Equal(0x1000000L, pack.FooterPartition);
    }

    [Fact]
    public void Parse_TooShortData_ThrowsArgumentException()
    {
        // Arrange
        var data = new byte[32]; // Too short

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PartitionPack.Parse(data));
    }

    [Fact]
    public void Parse_ZeroFooterPartition_IndicatesNoFooter()
    {
        // Arrange
        var data = new byte[64]; // All zeros

        // Act
        var pack = PartitionPack.Parse(data);

        // Assert
        Assert.Equal(0L, pack.FooterPartition);
    }
}
