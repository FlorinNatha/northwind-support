namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when credentials fail authentication (AU-4).
/// Generates identical 401 response regardless of whether email or password was invalid.
/// </summary>
public sealed class AuthenticationFailedException : Exception
{
    public const string DefaultMessage = "Invalid email or password.";

    public AuthenticationFailedException(string message = DefaultMessage) : base(message)
    {
    }
}
