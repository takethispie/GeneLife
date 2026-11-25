namespace Genelife.Appearance;

public static class AppearanceValidation
{
    public static IReadOnlyList<string> Validate(in AppearanceDescriptor descriptor)
    {
        var errors = new List<string>(10);
        Check(errors, descriptor.SkinTone, nameof(AppearanceDescriptor.SkinTone));
        Check(errors, descriptor.FaceShape, nameof(AppearanceDescriptor.FaceShape));
        Check(errors, descriptor.HairColor, nameof(AppearanceDescriptor.HairColor));
        Check(errors, descriptor.HairStyle, nameof(AppearanceDescriptor.HairStyle));
        Check(errors, descriptor.EyeColor, nameof(AppearanceDescriptor.EyeColor));
        Check(errors, descriptor.BodyFrame, nameof(AppearanceDescriptor.BodyFrame));
        Check(errors, descriptor.AgeBand, nameof(AppearanceDescriptor.AgeBand));
        Check(errors, descriptor.Expression, nameof(AppearanceDescriptor.Expression));

        if (descriptor.HeightScale > 100)
            errors.Add($"HeightScale must be in 0..100, got {descriptor.HeightScale}.");
        if (descriptor.AppearanceVersion == 0)
            errors.Add("AppearanceVersion must be positive.");
        else if (descriptor.AppearanceVersion > AppearanceDescriptor.CurrentVersion)
            errors.Add(
                $"AppearanceVersion {descriptor.AppearanceVersion} is newer than supported version " +
                $"{AppearanceDescriptor.CurrentVersion}.");

        return errors;
    }

    public static bool IsValid(in AppearanceDescriptor descriptor) => Validate(descriptor).Count == 0;

    public static void EnsureValid(in AppearanceDescriptor descriptor, string context)
    {
        var errors = Validate(descriptor);
        if (errors.Count == 0)
            return;
        throw new InvalidOperationException(
            $"Invalid appearance for {context}: {string.Join("; ", errors)}");
    }

    private static void Check<T>(List<string> errors, T value, string field)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            errors.Add($"{field} has undefined value {Convert.ToInt32(value)}");
    }
}
