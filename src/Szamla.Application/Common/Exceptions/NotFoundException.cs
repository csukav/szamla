namespace Szamla.Application.Common.Exceptions;

/// <summary>The requested entity doesn't exist, or doesn't belong to the caller's tenant (maps to HTTP 404 — the two cases are deliberately indistinguishable to the caller).</summary>
public sealed class NotFoundException(string message) : Exception(message);
