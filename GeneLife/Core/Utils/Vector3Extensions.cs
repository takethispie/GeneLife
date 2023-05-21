using System.Collections.Concurrent;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components;

namespace GeneLife.Core.Utils;

public static class Vector3Extensions
{
    public static Entity[] GetEntitiesInsideCube(this Vector3 origin, int distanceToBoundary, Entity[] entities)
    {
        var cubeOriginCorner = new Vector3(origin.X - distanceToBoundary, origin.Y - distanceToBoundary,
            origin.Z - distanceToBoundary);
        
        var oppositeCorner = cubeOriginCorner + new Vector3(distanceToBoundary, distanceToBoundary, distanceToBoundary);
        
        var insideEntities = new ConcurrentBag<Entity>();
        foreach (var entity in entities)
        {
            if (!entity.Has<Position>()) continue;
            var position = entity.Get<Position>();
            if(position.Coordinates.X > cubeOriginCorner.X
                   && position.Coordinates.X < oppositeCorner.X
                   && position.Coordinates.Y > cubeOriginCorner.Y
                   && position.Coordinates.Y < oppositeCorner.Y
                   && position.Coordinates.Z > cubeOriginCorner.Z
                   && position.Coordinates.Z < oppositeCorner.Z)
                insideEntities.Add(entity);
        }
        return insideEntities.ToArray();
    }
}