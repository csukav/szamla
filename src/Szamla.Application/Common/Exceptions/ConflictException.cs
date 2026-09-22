namespace Szamla.Application.Common.Exceptions;

/// <summary>A business rule was violated because the requested state already exists (maps to HTTP 409).</summary>
public sealed class ConflictException(string message) : Exception(message);
