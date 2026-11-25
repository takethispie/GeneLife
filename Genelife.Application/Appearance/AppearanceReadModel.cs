using Genelife.Api.DTOs;
using Genelife.Appearance;
using Genelife.Components.Gen;

namespace Genelife.Api.Appearance;

public static class AppearanceReadModel
{
    public static AppearanceLayersDto ToLayersDto(in AppearanceLayers layers) => new(
        layers.BodyPart,
        layers.SkinPalette,
        layers.FacePart,
        layers.EyePart,
        layers.HairPart,
        layers.OutfitPart,
        layers.ExpressionPart,
        layers.HeightScale,
        layers.LayerOrderVersion);

    public static AppearanceGenosStatus Build(Arch.Core.World world, Arch.Core.Entity entity)
    {
        if (!AppearanceMapper.TryDerive(world, entity, out var descriptor))
            throw new InvalidOperationException("Genos has no appearance descriptor.");

        var isPlayer = world.Has<Genelife.Components.Gen.PlayerControlled>(entity);
        var layers = AppearanceComposer.Compose(descriptor, isPlayer);
        return new AppearanceGenosStatus(
            AppearanceMapper.StableKey(world, entity),
            world.Has<GenName>(entity) ? world.Get<GenName>(entity).Name : string.Empty,
            world.Has<Alive>(entity) ? world.Get<Alive>(entity).Age : 0,
            isPlayer,
            isPlayer ? world.Get<Genelife.Components.Gen.PlayerControlled>(entity).PlayerId : null,
            world.Has<Genelife.Components.Life.Lifecycle>(entity)
                ? world.Get<Genelife.Components.Life.Lifecycle>(entity).Stage.ToString()
                : string.Empty,
            AppearanceMapper.ToDto(descriptor),
            ToLayersDto(layers));
    }
}
