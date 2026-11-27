using System.Reflection;
using Genelife.Api.DTOs;

namespace Genelife.UnitTests.Snapshots;

/// <summary>
/// Shared reflection probe for the snapshot DTOs. A field missing from a delta comparison is
/// never replicated and the failure is invisible to every other test, so each DTO is probed
/// property by property instead of being trusted to a hand-maintained list. An unrecognised
/// property type throws rather than passing, so adding a field of a new type fails loudly.
/// </summary>
internal static class DtoGuard
{
    public static T Clone<T>(T source) where T : new()
    {
        var clone = new T();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            property.SetValue(clone, property.GetValue(source));
        return clone;
    }

    /// <summary>
    /// Names of every property whose value does <em>not</em> change the equality verdict, i.e.
    /// the fields a delta would silently drop.
    /// </summary>
    public static IReadOnlyList<string> IgnoredProperties<T>(T baseline, Func<T, T, bool> equal)
        where T : new()
    {
        var ignored = new List<string>();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var variant = Clone(baseline);
            property.SetValue(variant, Perturb(property.GetValue(variant), property.PropertyType));
            if (equal(baseline, variant))
                ignored.Add(property.Name);
        }

        return ignored;
    }

    private static object? Perturb(object? value, Type type)
    {
        if (type == typeof(string)) return (string?)value + "x";
        if (type == typeof(int)) return (int)value! + 1;
        if (type == typeof(float)) return (float)value! + 1f;
        if (type == typeof(decimal)) return (decimal)value! + 1m;
        if (type == typeof(bool)) return !(bool)value!;
        if (type == typeof(ulong)) return (ulong)value! + 1UL;
        if (type == typeof(ulong?)) return value is ulong id ? id + 1UL : 1UL;
        if (type == typeof(AppearanceDto)) return new AppearanceDto { SkinTone = 99, AppearanceVersion = 2 };

        throw new InvalidOperationException(
            $"DtoGuard has no perturbation for {type.Name}. Add one so the new field is actually probed.");
    }
}