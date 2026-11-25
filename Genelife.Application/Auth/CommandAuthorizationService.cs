using Genelife.Api.Commands;
using Genelife.Api.Services;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Auth;

public sealed class CommandAuthorizationService
{
    private readonly SimulationEngine engine;
    private readonly ILogger<CommandAuthorizationService> logger;

    public CommandAuthorizationService(SimulationEngine engine, ILogger<CommandAuthorizationService> logger)
    {
        this.engine = engine;
        this.logger = logger;
    }

    public AuthorizationResult Authorize(string playerId, GenosCommand command, bool isAdmin = false)
    {
        if (isAdmin)
        {
            if (!engine.TryGetEntity(command.TargetNetworkId, out var adminTarget))
            {
                LogFailure(AuthorizationFailure.UnknownNetworkId, command);
                return AuthorizationResult.Denied(AuthorizationFailure.UnknownNetworkId,
                    "Target entity does not exist");
            }

            if (!engine.IsAlive(adminTarget))
            {
                LogFailure(AuthorizationFailure.TargetNotAlive, command);
                return AuthorizationResult.Denied(AuthorizationFailure.TargetNotAlive,
                    "Target entity is not alive");
            }

            return AuthorizationResult.Success();
        }

        if (!engine.TryGetEntity(command.TargetNetworkId, out var target))
        {
            LogFailure(AuthorizationFailure.UnknownNetworkId, command);
            return AuthorizationResult.Denied(AuthorizationFailure.UnknownNetworkId,
                "Target entity does not exist");
        }

        if (!engine.IsAlive(target))
        {
            LogFailure(AuthorizationFailure.TargetNotAlive, command);
            return AuthorizationResult.Denied(AuthorizationFailure.TargetNotAlive,
                "Target entity is not alive");
        }

        var ownerPlayerId = engine.GetOwnerPlayerId(target);
        if (ownerPlayerId == null)
        {
            LogFailure(AuthorizationFailure.NotOwned, command);
            return AuthorizationResult.Denied(AuthorizationFailure.NotOwned,
                "Target entity is not player-controlled");
        }

        if (!string.Equals(ownerPlayerId, playerId, StringComparison.Ordinal))
        {
            LogFailure(AuthorizationFailure.NotOwned, command);
            return AuthorizationResult.Denied(AuthorizationFailure.NotOwned,
                "Target entity is owned by another player");
        }

        if (command is InteractCommand interactCommand)
        {
            if (!engine.TryGetEntity(interactCommand.OtherNetworkId, out var other))
            {
                LogFailure(AuthorizationFailure.UnknownNetworkId, command);
                return AuthorizationResult.Denied(AuthorizationFailure.UnknownNetworkId,
                    "Interaction target does not exist");
            }

            if (!engine.IsAlive(other))
            {
                LogFailure(AuthorizationFailure.TargetNotAlive, command);
                return AuthorizationResult.Denied(AuthorizationFailure.TargetNotAlive,
                    "Interaction target is not alive");
            }
        }

        return AuthorizationResult.Success();
    }

    public AuthorizationResult AuthorizeAssign(string playerId, AssignCommand command, bool isAdmin = false)
    {
        if (!isAdmin)
        {
            LogFailure(AuthorizationFailure.NotOwned, command);
            return AuthorizationResult.Denied(AuthorizationFailure.NotOwned,
                "Only admins can assign entities");
        }

        if (!engine.TryGetEntity(command.TargetNetworkId, out var target))
        {
            LogFailure(AuthorizationFailure.UnknownNetworkId, command);
            return AuthorizationResult.Denied(AuthorizationFailure.UnknownNetworkId,
                "Target entity does not exist");
        }

        if (!engine.IsAlive(target))
        {
            LogFailure(AuthorizationFailure.TargetNotAlive, command);
            return AuthorizationResult.Denied(AuthorizationFailure.TargetNotAlive,
                "Target entity is not alive");
        }

        return AuthorizationResult.Success();
    }

    public AuthorizationResult AuthorizeTransfer(TransferCommand command, bool isAdmin = false)
    {
        if (!isAdmin)
        {
            if (!engine.TryGetEntity(command.TargetNetworkId, out var target))
            {
                LogFailure(AuthorizationFailure.UnknownNetworkId, command);
                return AuthorizationResult.Denied(AuthorizationFailure.UnknownNetworkId,
                    "Target entity does not exist");
            }

            if (!engine.IsAlive(target))
            {
                LogFailure(AuthorizationFailure.TargetNotAlive, command);
                return AuthorizationResult.Denied(AuthorizationFailure.TargetNotAlive,
                    "Target entity is not alive");
            }

            var ownerPlayerId = engine.GetOwnerPlayerId(target);
            if (ownerPlayerId == null)
            {
                LogFailure(AuthorizationFailure.NotOwned, command);
                return AuthorizationResult.Denied(AuthorizationFailure.NotOwned,
                    "Entity is not player-controlled");
            }

            if (!string.Equals(ownerPlayerId, command.FromPlayerId, StringComparison.Ordinal))
            {
                LogFailure(AuthorizationFailure.NotOwned, command);
                return AuthorizationResult.Denied(AuthorizationFailure.NotOwned,
                    "Entity is owned by another player");
            }
        }

        return AuthorizationResult.Success();
    }

    private void LogFailure(AuthorizationFailure failure, object command)
    {
        logger.LogWarning(
            "Authorization failed: {Failure} for command type {CommandType}",
            failure,
            command.GetType().Name);
    }
}