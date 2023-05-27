using System.Collections.Concurrent;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Data;

namespace GeneLife.Core.Extensions;

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

    public static bool CloseEnoughForWalkingTo(this Vector3 origin, Vector3 target) 
        => Vector3.Distance(origin, target) < Constants.MaxWalkingDistance;
}