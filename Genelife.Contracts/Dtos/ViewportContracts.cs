namespace Genelife.Api.DTOs;

public sealed record SetViewportRequest(int CenterX, int CenterY, int Radius);

public sealed record ViewportResult(
    bool Success,
    int CenterX,
    int CenterY,
    int Radius,
    string? Error);
