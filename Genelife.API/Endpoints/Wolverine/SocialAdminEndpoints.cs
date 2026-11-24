using Genelife.Api.Auth;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Api.Social;
using Genelife.Social;
using Microsoft.AspNetCore.Authorization;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class SocialAdminEndpoints
{
    [WolverineGet("/api/social/admin/overview")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static Task<SocialAdminOverviewDto> Overview(
        SimulationEngine engine,
        PlayerDirectoryService directory,
        SocialGraphService social,
        ModerationService moderation,
        SocialPrivacyRegistry privacy,
        ChatService chat,
        ChatOptions chatOptions,
        CancellationToken cancellationToken) =>
        SocialReadModel.BuildAsync(
            engine,
            directory,
            social,
            moderation,
            privacy,
            chat,
            chatOptions,
            cancellationToken);
}
