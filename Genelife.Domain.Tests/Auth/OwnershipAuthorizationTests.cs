using Arch.Core;
using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genelife.UnitTests.Auth;

public sealed class OwnershipAuthorizationTests : IDisposable
{
    private readonly SimulationEngine engine;
    private readonly CommandAuthorizationService authorizer;
    private const string Player1 = "player-1";
    private const string Player2 = "player-2";
    private const ulong NonExistentId = 9999;

    public OwnershipAuthorizationTests()
    {
        engine = new SimulationEngine();
        authorizer = new CommandAuthorizationService(
            engine,
            NullLogger<CommandAuthorizationService>.Instance);
    }

    private void EnsureInitialized()
    {
        if (!engine.IsRunning)
        {
            engine.Initialize();

            engine.ExecuteWithWorld(world =>
            {
                var e1 = world.Create(
                    new GenName("Player1Genos"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(30),
                    new PlayerControlled(Player1),
                    new NetworkId(1));

                var e2 = world.Create(
                    new GenName("Player2Genos"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(25),
                    new PlayerControlled(Player2),
                    new NetworkId(2));

                world.Create(
                    new GenName("UnownedGenos"),
                    new Needs(),
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(40),
                    new NetworkId(3));

                return true;
            });
        }
    }

    public void Dispose()
    {
        engine.Dispose();
    }

    [Fact]
    public void Own_genos_move_command_succeeds()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(1) };
        var result = authorizer.Authorize(Player1, command);

        Assert.True(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.None, result.Failure);
    }

    [Fact]
    public void Cross_player_move_command_rejected()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(2) };
        var result = authorizer.Authorize(Player1, command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.NotOwned, result.Failure);
    }

    [Fact]
    public void Own_genos_interact_command_succeeds()
    {
        EnsureInitialized();

        var command = new InteractCommand
        {
            TargetNetworkId = new NetworkId(1),
            OtherNetworkId = new NetworkId(2)
        };

        var result = authorizer.Authorize(Player1, command);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Cross_player_interact_command_rejected()
    {
        EnsureInitialized();

        var command = new InteractCommand
        {
            TargetNetworkId = new NetworkId(2),
            OtherNetworkId = new NetworkId(1)
        };

        var result = authorizer.Authorize(Player1, command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.NotOwned, result.Failure);
    }

    [Fact]
    public void Own_genos_use_command_succeeds()
    {
        EnsureInitialized();

        var command = new UseCommand { TargetNetworkId = new NetworkId(1) };
        var result = authorizer.Authorize(Player1, command);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Cross_player_use_command_rejected()
    {
        EnsureInitialized();

        var command = new UseCommand { TargetNetworkId = new NetworkId(2) };
        var result = authorizer.Authorize(Player1, command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.NotOwned, result.Failure);
    }

    [Fact]
    public void Spoofed_network_id_rejected()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(NonExistentId) };
        var result = authorizer.Authorize(Player1, command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.UnknownNetworkId, result.Failure);
    }

    [Fact]
    public void Spoofed_network_id_rejected_even_for_admin()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(NonExistentId) };
        var result = authorizer.Authorize(Player1, command, isAdmin: true);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.UnknownNetworkId, result.Failure);
    }

    [Fact]
    public void Admin_can_act_on_any_player_genos()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(1) };
        var result = authorizer.Authorize("admin-user", command, isAdmin: true);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Admin_can_act_on_other_player_genos()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(2) };
        var result = authorizer.Authorize("admin-user", command, isAdmin: true);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Unowned_genos_action_rejected_for_player()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(3) };
        var result = authorizer.Authorize(Player1, command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.NotOwned, result.Failure);
    }

    [Fact]
    public void Admin_can_act_on_unowned_genos()
    {
        EnsureInitialized();

        var command = new MoveCommand { TargetNetworkId = new NetworkId(3) };
        var result = authorizer.Authorize("admin-user", command, isAdmin: true);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Dead_target_rejected()
    {
        EnsureInitialized();

        Entity deadEntity = default;
        engine.ExecuteWithWorld(world =>
        {
            deadEntity = world.Create(
                new GenName("DeadGenos"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(0),
                new PlayerControlled(Player1),
                new NetworkId(100));
            world.Destroy(deadEntity);
            return true;
        });

        var command = new MoveCommand { TargetNetworkId = new NetworkId(100) };
        var result = authorizer.Authorize(Player1, command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.UnknownNetworkId, result.Failure);
    }

    [Fact]
    public void Dead_target_rejected_for_admin()
    {
        EnsureInitialized();

        engine.ExecuteWithWorld(world =>
        {
            var e = world.Create(
                new GenName("DeadGenos"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(0),
                new PlayerControlled(Player1),
                new NetworkId(101));
            world.Destroy(e);
            return true;
        });

        var command = new MoveCommand { TargetNetworkId = new NetworkId(101) };
        var result = authorizer.Authorize("admin", command, isAdmin: true);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.UnknownNetworkId, result.Failure);
    }

    [Fact]
    public void Two_player_isolation_matrix_move()
    {
        EnsureInitialized();

        var p1Command = new MoveCommand { TargetNetworkId = new NetworkId(1) };
        var p2Command = new MoveCommand { TargetNetworkId = new NetworkId(2) };

        Assert.True(authorizer.Authorize(Player1, p1Command).IsAuthorized);
        Assert.False(authorizer.Authorize(Player1, p2Command).IsAuthorized);
        Assert.False(authorizer.Authorize(Player2, p1Command).IsAuthorized);
        Assert.True(authorizer.Authorize(Player2, p2Command).IsAuthorized);
    }

    [Fact]
    public void Two_player_isolation_matrix_interact()
    {
        EnsureInitialized();

        var p1ToP2 = new InteractCommand
        {
            TargetNetworkId = new NetworkId(1),
            OtherNetworkId = new NetworkId(2)
        };
        var p2ToP1 = new InteractCommand
        {
            TargetNetworkId = new NetworkId(2),
            OtherNetworkId = new NetworkId(1)
        };

        Assert.True(authorizer.Authorize(Player1, p1ToP2).IsAuthorized);
        Assert.False(authorizer.Authorize(Player1, p2ToP1).IsAuthorized);
        Assert.False(authorizer.Authorize(Player2, p1ToP2).IsAuthorized);
        Assert.True(authorizer.Authorize(Player2, p2ToP1).IsAuthorized);
    }

    [Fact]
    public void Assign_command_requires_admin()
    {
        EnsureInitialized();

        var command = new AssignCommand
        {
            TargetNetworkId = new NetworkId(3),
            PlayerId = Player1
        };

        var playerResult = authorizer.AuthorizeAssign(Player1, command);
        Assert.False(playerResult.IsAuthorized);
        Assert.Equal(AuthorizationFailure.NotOwned, playerResult.Failure);

        var adminResult = authorizer.AuthorizeAssign("admin", command, isAdmin: true);
        Assert.True(adminResult.IsAuthorized);
    }

    [Fact]
    public void Assign_command_admin_rejected_for_unknown_network_id()
    {
        EnsureInitialized();

        var command = new AssignCommand
        {
            TargetNetworkId = new NetworkId(NonExistentId),
            PlayerId = Player1
        };

        var result = authorizer.AuthorizeAssign("admin", command, isAdmin: true);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.UnknownNetworkId, result.Failure);
    }

    [Fact]
    public void Transfer_command_owner_can_transfer()
    {
        EnsureInitialized();

        var command = new TransferCommand
        {
            TargetNetworkId = new NetworkId(1),
            FromPlayerId = Player1,
            ToPlayerId = Player2
        };

        var result = authorizer.AuthorizeTransfer(command);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Transfer_command_cross_player_rejected()
    {
        EnsureInitialized();

        var command = new TransferCommand
        {
            TargetNetworkId = new NetworkId(1),
            FromPlayerId = Player2,
            ToPlayerId = Player1
        };

        var result = authorizer.AuthorizeTransfer(command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.NotOwned, result.Failure);
    }

    [Fact]
    public void Transfer_command_admin_bypasses_ownership()
    {
        EnsureInitialized();

        var command = new TransferCommand
        {
            TargetNetworkId = new NetworkId(1),
            FromPlayerId = "unauthorized",
            ToPlayerId = "new-owner"
        };

        var result = authorizer.AuthorizeTransfer(command, isAdmin: true);

        Assert.True(result.IsAuthorized);
    }

    [Fact]
    public void Interact_command_rejects_spoofed_other_network_id()
    {
        EnsureInitialized();

        var command = new InteractCommand
        {
            TargetNetworkId = new NetworkId(1),
            OtherNetworkId = new NetworkId(NonExistentId)
        };

        var result = authorizer.Authorize(Player1, command);

        Assert.False(result.IsAuthorized);
        Assert.Equal(AuthorizationFailure.UnknownNetworkId, result.Failure);
    }

    [Fact]
    public void SetOwner_adds_player_controlled_component()
    {
        EnsureInitialized();

        engine.ExecuteWithWorld(world =>
        {
            var e = world.Create(
                new GenName("NewGenos"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(30),
                new NetworkId(50));
            return true;
        });

        Entity entity = default;
        engine.ExecuteWithWorld(world =>
        {
            var query = new QueryDescription().WithAll<NetworkId>();
            world.Query(in query, (Entity e, ref NetworkId nid) =>
            {
                if (nid.Value == 50)
                    entity = e;
            });
            return true;
        });

        Assert.True(engine.IsAlive(entity));
        Assert.Null(engine.GetOwnerPlayerId(entity));

        engine.SetOwner(entity, Player1);
        Assert.Equal(Player1, engine.GetOwnerPlayerId(entity));
    }

    [Fact]
    public void RemoveOwner_strips_player_controlled_component()
    {
        EnsureInitialized();

        engine.ExecuteWithWorld(world =>
        {
            var e = world.Create(
                new GenName("OwnedGenos"),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(30),
                new PlayerControlled(Player1),
                new NetworkId(60));
            return true;
        });

        Entity entity = default;
        engine.ExecuteWithWorld(world =>
        {
            var query = new QueryDescription().WithAll<NetworkId>();
            world.Query(in query, (Entity e, ref NetworkId nid) =>
            {
                if (nid.Value == 60)
                    entity = e;
            });
            return true;
        });

        Assert.Equal(Player1, engine.GetOwnerPlayerId(entity));

        engine.RemoveOwner(entity);
        Assert.Null(engine.GetOwnerPlayerId(entity));
    }

    [Fact]
    public void Unrecognized_command_type_preserves_base_checks()
    {
        EnsureInitialized();
        var unknownId = new NetworkId(NonExistentId);

        var move = new MoveCommand { TargetNetworkId = unknownId };
        Assert.Equal(AuthorizationFailure.UnknownNetworkId,
            authorizer.Authorize(Player1, move).Failure);

        var interact = new InteractCommand { TargetNetworkId = unknownId, OtherNetworkId = new NetworkId(1) };
        Assert.Equal(AuthorizationFailure.UnknownNetworkId,
            authorizer.Authorize(Player1, interact).Failure);

        var use = new UseCommand { TargetNetworkId = unknownId };
        Assert.Equal(AuthorizationFailure.UnknownNetworkId,
            authorizer.Authorize(Player1, use).Failure);
    }
}