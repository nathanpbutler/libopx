using System.Collections.Concurrent;
using nathanbutlerDEV.libopx.Enums;
using nathanbutlerDEV.libopx.Handlers;

namespace nathanbutlerDEV.libopx.Core;

/// <summary>
/// Registry for format handler instances.
/// Provides centralized storage and retrieval of format handlers by format type.
/// Thread-safe for concurrent access.
/// </summary>
public static class FormatRegistry
{
    private static readonly ConcurrentDictionary<Format, IFormatHandlerBase> _handlers = new();

    /// <summary>
    /// Static constructor that automatically registers all built-in format handlers.
    /// This ensures handlers are available when FormatIO or any other code needs them.
    /// </summary>
    static FormatRegistry()
    {
        Register(new VBIHandler());
        Register(new T42Handler());
        Register(new ANCHandler());
        Register(new TSHandler());
        Register(new MXFHandler());
    }

    /// <summary>
    /// Registers a format handler for a specific format.
    /// If a handler already exists for the format, it will be replaced.
    /// </summary>
    /// <param name="format">The format this handler processes</param>
    /// <param name="handler">The handler instance to register</param>
    /// <exception cref="ArgumentNullException">Thrown if handler is null</exception>
    public static void Register(Format format, IFormatHandlerBase handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _handlers[format] = handler;
    }

    /// <summary>
    /// Registers a format handler using its InputFormat property.
    /// If a handler already exists for the format, it will be replaced.
    /// </summary>
    /// <param name="handler">The handler instance to register</param>
    /// <exception cref="ArgumentNullException">Thrown if handler is null</exception>
    public static void Register(IFormatHandlerBase handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _handlers[handler.InputFormat] = handler;
    }

    /// <summary>
    /// Retrieves the handler for a specific format.
    /// </summary>
    /// <param name="format">The format to get the handler for</param>
    /// <returns>The handler instance if found</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no handler is registered for the format</exception>
    public static IFormatHandlerBase GetHandler(Format format)
    {
        if (_handlers.TryGetValue(format, out var handler))
        {
            return handler;
        }

        throw new KeyNotFoundException($"No handler registered for format: {format}");
    }

    /// <summary>
    /// Tries to retrieve the handler for a specific format.
    /// </summary>
    /// <param name="format">The format to get the handler for</param>
    /// <param name="handler">The handler instance if found, otherwise null</param>
    /// <returns>True if a handler was found, false otherwise</returns>
    public static bool TryGetHandler(Format format, out IFormatHandlerBase? handler)
    {
        return _handlers.TryGetValue(format, out handler);
    }

    /// <summary>
    /// Checks if a handler is registered for a specific format.
    /// </summary>
    /// <param name="format">The format to check</param>
    /// <returns>True if a handler is registered, false otherwise</returns>
    public static bool IsRegistered(Format format)
    {
        return _handlers.ContainsKey(format);
    }

    /// <summary>
    /// Unregisters a handler for a specific format.
    /// </summary>
    /// <param name="format">The format to unregister the handler for</param>
    /// <returns>True if the handler was removed, false if no handler was registered</returns>
    public static bool Unregister(Format format)
    {
        return _handlers.TryRemove(format, out _);
    }

    /// <summary>
    /// Gets all registered format types.
    /// </summary>
    /// <returns>An enumerable of all registered format types</returns>
    public static IEnumerable<Format> GetRegisteredFormats()
    {
        return _handlers.Keys.ToArray();
    }

    /// <summary>
    /// Clears all registered handlers.
    /// Primarily used for testing purposes.
    /// </summary>
    public static void Clear()
    {
        _handlers.Clear();
    }
}
