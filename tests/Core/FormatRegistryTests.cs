using nathanbutlerDEV.libopx.Core;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;
using Xunit;

namespace nathanbutlerDEV.libopx.Tests.Core;

/// <summary>
/// Unit tests for the FormatRegistry class.
/// Tests registration and retrieval of format handlers.
/// </summary>
[Collection("FormatRegistry")]
public class FormatRegistryTests
{
    // Note: Tests that need a clean registry state call Clear() explicitly
    // FormatRegistry auto-registers handlers in its static constructor

    [Fact]
    public void Register_WithFormatAndHandler_RegistersSuccessfully()
    {
        // Arrange
        var handler = new T42Handler();

        // Act
        FormatRegistry.Register(Format.T42, handler);

        // Assert
        Assert.True(FormatRegistry.IsRegistered(Format.T42));
    }

    [Fact]
    public void Register_WithHandler_UsesInputFormat()
    {
        // Arrange
        var handler = new T42Handler();

        // Act
        FormatRegistry.Register(handler);

        // Assert
        Assert.True(FormatRegistry.IsRegistered(Format.T42));
    }

    [Fact]
    public void Register_NullHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => FormatRegistry.Register(Format.T42, null!));
        Assert.Throws<ArgumentNullException>(() => FormatRegistry.Register(null!));
    }

    [Fact]
    public void GetHandler_RegisteredFormat_ReturnsHandler()
    {
        // Arrange
        var handler = new T42Handler();
        FormatRegistry.Register(handler);

        // Act
        var retrieved = FormatRegistry.GetHandler(Format.T42);

        // Assert
        Assert.Same(handler, retrieved);
    }

    [Fact]
    public void GetHandler_UnregisteredFormat_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => FormatRegistry.GetHandler(Format.VBI));
    }

    [Fact]
    public void TryGetHandler_RegisteredFormat_ReturnsTrue()
    {
        // Arrange
        var handler = new T42Handler();
        FormatRegistry.Register(handler);

        // Act
        var result = FormatRegistry.TryGetHandler(Format.T42, out var retrieved);

        // Assert
        Assert.True(result);
        Assert.Same(handler, retrieved);
    }

    [Fact]
    public void TryGetHandler_UnregisteredFormat_ReturnsFalse()
    {
        // Act
        var result = FormatRegistry.TryGetHandler(Format.VBI, out var retrieved);

        // Assert
        Assert.False(result);
        Assert.Null(retrieved);
    }

    [Fact]
    public void IsRegistered_RegisteredFormat_ReturnsTrue()
    {
        // Arrange
        var handler = new T42Handler();
        FormatRegistry.Register(handler);

        // Act & Assert
        Assert.True(FormatRegistry.IsRegistered(Format.T42));
    }

    [Fact]
    public void IsRegistered_UnregisteredFormat_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(FormatRegistry.IsRegistered(Format.VBI));
    }

    [Fact]
    public void Unregister_RegisteredFormat_ReturnsTrue()
    {
        // Arrange
        var handler = new T42Handler();
        FormatRegistry.Register(handler);

        // Act
        var result = FormatRegistry.Unregister(Format.T42);

        // Assert
        Assert.True(result);
        Assert.False(FormatRegistry.IsRegistered(Format.T42));
    }

    [Fact]
    public void Unregister_UnregisteredFormat_ReturnsFalse()
    {
        // Act
        var result = FormatRegistry.Unregister(Format.VBI);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Register_SameFormatTwice_ReplacesHandler()
    {
        // Arrange
        var handler1 = new T42Handler();
        var handler2 = new T42Handler();

        // Act
        FormatRegistry.Register(handler1);
        FormatRegistry.Register(handler2);

        var retrieved = FormatRegistry.GetHandler(Format.T42);

        // Assert
        Assert.Same(handler2, retrieved);
        Assert.NotSame(handler1, retrieved);
    }

    [Fact]
    public void GetRegisteredFormats_MultipleHandlers_ReturnsAllFormats()
    {
        // Arrange
        var t42Handler = new T42Handler();
        var vbiHandler = new VBIHandler();

        FormatRegistry.Register(t42Handler);
        FormatRegistry.Register(vbiHandler);

        // Act
        var formats = FormatRegistry.GetRegisteredFormats().ToList();

        // Assert
        Assert.Equal(2, formats.Count);
        Assert.Contains(Format.T42, formats);
        Assert.Contains(Format.VBI, formats);
    }

    [Fact]
    public void GetRegisteredFormats_NoHandlers_ReturnsEmpty()
    {
        // Arrange - Clear any existing handlers from other tests
        FormatRegistry.Clear();

        // Act
        var formats = FormatRegistry.GetRegisteredFormats().ToList();

        // Assert
        Assert.Empty(formats);
    }

    [Fact]
    public void Clear_RemovesAllHandlers()
    {
        // Arrange
        FormatRegistry.Register(new T42Handler());
        FormatRegistry.Register(new VBIHandler());
        Assert.Equal(2, FormatRegistry.GetRegisteredFormats().Count());

        // Act
        FormatRegistry.Clear();

        // Assert
        Assert.Empty(FormatRegistry.GetRegisteredFormats());
        Assert.False(FormatRegistry.IsRegistered(Format.T42));
        Assert.False(FormatRegistry.IsRegistered(Format.VBI));
    }
}
