using System;

namespace BetaTornBot.Shared;

/// <summary>Thrown when the Torn API returns an error or a response the library can't understand.</summary>
public sealed class TornApiException : Exception
{
    public TornApiException(string message) : base(message) { }
    public TornApiException(string message, Exception inner) : base(message, inner) { }
}
