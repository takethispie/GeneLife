﻿using Arch.Core;
using Arch.System;
using GeneLife.Core.Components.Utils;

namespace GeneLife.Core.Systems;

public delegate void HookUpdateNotification(Entity entity);

public class UIHookSystem : BaseSystem<World, float>
{
    private readonly QueryDescription _hookedEntitiesQuery = new QueryDescription().WithAll<UIHook>();
    
    public event HookUpdateNotification EntityUpdated;
    public UIHookSystem(World world) : base(world)
    {
    }

    public override void Update(in float t)
    {
        World.Query(in _hookedEntitiesQuery, (in Entity entity) => OnEntityUpdated(entity));
    }
    
    protected virtual void OnEntityUpdated(Entity entity)
    {
        EntityUpdated.Invoke(entity); 
    }
}