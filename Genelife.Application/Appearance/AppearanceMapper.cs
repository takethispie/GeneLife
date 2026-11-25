using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Appearance;
using Genelife.Components.Gen;

namespace Genelife.Api.Appearance;

public static class AppearanceMapper
{
    public static AppearanceDto ToDto(in AppearanceDescriptor descriptor) => new()
    {
        SkinTone = (byte)descriptor.SkinTone,
        FaceShape = (byte)descriptor.FaceShape,
        HairColor = (byte)descriptor.HairColor,
        HairStyle = (byte)descriptor.HairStyle,
        EyeColor = (byte)descriptor.EyeColor,
        BodyFrame = (byte)descriptor.BodyFrame,
        HeightScale = descriptor.HeightScale,
        AgeBand = (byte)descriptor.AgeBand,
        Expression = (byte)descriptor.Expression,
        AppearanceVersion = descriptor.AppearanceVersion
    };

    public static AppearanceDescriptor FromDto(in AppearanceDto dto) => new(
        (SkinTone)dto.SkinTone,
        (FaceShape)dto.FaceShape,
        (HairColor)dto.HairColor,
        (HairStyle)dto.HairStyle,
        (EyeColor)dto.EyeColor,
        (BodyFrame)dto.BodyFrame,
        dto.HeightScale,
        (AgeBand)dto.AgeBand,
        (Expression)dto.Expression,
        dto.AppearanceVersion);

    public static ulong StableKey(World world, Entity entity) =>
        world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : (ulong)entity.Id;

    public static bool TryDerive(
        World world,
        Entity entity,
        out AppearanceDescriptor descriptor,
        byte appearanceVersion = AppearanceDescriptor.CurrentVersion)
    {
        descriptor = default;
        if (!world.IsAlive(entity) || !world.Has<Traits>(entity) || !world.Has<Alive>(entity))
            return false;

        descriptor = AppearanceDerivation.Derive(
            world.Get<Traits>(entity),
            world.Get<Alive>(entity).Age,
            StableKey(world, entity),
            appearanceVersion);
        return true;
    }
}
