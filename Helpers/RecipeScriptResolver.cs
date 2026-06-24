using fuji_barcode.Models;

namespace fuji_barcode.Helpers;

public static class RecipeScriptResolver
{
    public static string Resolve(IReadOnlyList<RpaScriptInfo> scripts, string recipeName)
    {
        if (scripts.Count == 0)
            throw new InvalidOperationException($"No scripts found for recipe '{recipeName}'");

        var matches = scripts
            .Where(s => s.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase)
                        || s.Name.StartsWith(recipeName + "_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            throw new InvalidOperationException($"No script matched recipe '{recipeName}'");

        var exact = matches.FirstOrDefault(s =>
            s.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact.Name;

        var versioned = matches
            .Select(s => (Script: s, Version: ParseVersionSuffix(s.Name, recipeName)))
            .Where(x => x.Version is not null)
            .ToList();

        if (versioned.Count == 0)
            throw new InvalidOperationException(
                $"Multiple ambiguous matches for recipe '{recipeName}': " +
                string.Join(", ", matches.Select(m => m.Name)));

        var best = versioned
            .OrderByDescending(x => x.Version)
            .ToList();

        if (best.Count > 1 &&
            best[0].Version == best[1].Version)
        {
            throw new InvalidOperationException(
                $"Ambiguous versioned matches for recipe '{recipeName}': " +
                string.Join(", ", best.TakeWhile(x =>
                    x.Version == best[0].Version).Select(x => x.Script.Name)));
        }

        return best[0].Script.Name;
    }

    private static Version? ParseVersionSuffix(string scriptName, string recipeName)
    {
        var suffix = scriptName[(recipeName.Length + 1)..];
        var parts = suffix.Split('_', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (part.StartsWith("V", StringComparison.OrdinalIgnoreCase) &&
                Version.TryParse(part[1..], out var version))
            {
                return version;
            }
        }

        return null;
    }
}
