using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.CommonComponents;

namespace GeneLife.Utils;

public static class Vector3Extensions
{

    public static Entity[] GetEntitiesInsideCube(this Vector3 origin, int distanceToBoundary, Entity[] entities)
    {
        var cubeOriginCorner = new Vector3(origin.X - distanceToBoundary, origin.Y - distanceToBoundary,
            origin.Z - distanceToBoundary);
        
        var oppositeCorner = new Vector3(origin.X + distanceToBoundary, origin.Y + distanceToBoundary,
            origin.Z + distanceToBoundary);
        
        return entities.Where(entity =>
        {
            if (!entity.Has<Position>()) return false;
            var position = entity.Get<Position>();
            return position.Coordinates.X > cubeOriginCorner.X
                   && position.Coordinates.X < oppositeCorner.X
                   && position.Coordinates.Y > cubeOriginCorner.Y
                   && position.Coordinates.Y < oppositeCorner.Y
                   && position.Coordinates.Z > cubeOriginCorner.Z
                   && position.Coordinates.Z < oppositeCorner.Z;
        }).ToArray();
    }
}