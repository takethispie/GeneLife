namespace Genelife.Api.Auth;

public enum AuthorizationFailure
{
    None,
    UnknownNetworkId,
    NotOwned,
    OutOfRange,
    TargetNotAlive
}

public sealed record AuthorizationResult
{
    public bool IsAuthorized { get; }
    public AuthorizationFailure Failure { get; }
    public string? Detail { get; }

    private AuthorizationResult(bool isAuthorized, AuthorizationFailure failure, string? detail)
    {
        IsAuthorized = isAuthorized;
        Failure = failure;
        Detail = detail;
    }

    public static AuthorizationResult Success() =>
        new(true, AuthorizationFailure.None, null);

    public static AuthorizationResult Denied(AuthorizationFailure failure, string? detail = null) =>
        new(false, failure, detail);
}