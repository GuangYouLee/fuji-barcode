using fuji_barcode.Helpers;
using fuji_barcode.Models;
using Xunit;

namespace fuji_barcode.Tests;

public class RecipeScriptResolverTests
{
    [Fact]
    public void Exact_match_returns_script_name()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "MyRecipe" }
        };

        var result = RecipeScriptResolver.Resolve(scripts, "MyRecipe");

        Assert.Equal("MyRecipe", result);
    }

    [Fact]
    public void Prefix_match_selects_highest_version()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "MyRecipe_20260526_V1.0" },
            new() { Name = "MyRecipe_20260526_V2.1" }
        };

        var result = RecipeScriptResolver.Resolve(scripts, "MyRecipe");

        Assert.Equal("MyRecipe_20260526_V2.1", result);
    }

    [Fact]
    public void Exact_preferred_over_prefix_match()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "MyRecipe" },
            new() { Name = "MyRecipe_20260526_V1.0" }
        };

        var result = RecipeScriptResolver.Resolve(scripts, "MyRecipe");

        Assert.Equal("MyRecipe", result);
    }

    [Fact]
    public void No_match_throws()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "OtherRecipe" }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RecipeScriptResolver.Resolve(scripts, "MyRecipe"));

        Assert.Contains("No script matched", ex.Message);
    }

    [Fact]
    public void Ambiguous_non_versioned_matches_throw()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "MyRecipe_A" },
            new() { Name = "MyRecipe_B" }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RecipeScriptResolver.Resolve(scripts, "MyRecipe"));

        Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ambiguous_same_version_matches_throw()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "MyRecipe_20260526_V1.0" },
            new() { Name = "MyRecipe_20260527_V1.0" }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RecipeScriptResolver.Resolve(scripts, "MyRecipe"));

        Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_scripts_throws()
    {
        var scripts = new List<RpaScriptInfo>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RecipeScriptResolver.Resolve(scripts, "MyRecipe"));

        Assert.Contains("No scripts found", ex.Message);
    }

    [Fact]
    public void Case_insensitive_exact_match()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "myrecipe" }
        };

        var result = RecipeScriptResolver.Resolve(scripts, "MyRecipe");

        Assert.Equal("myrecipe", result);
    }

    [Fact]
    public void Case_insensitive_prefix_match()
    {
        var scripts = new List<RpaScriptInfo>
        {
            new() { Name = "MYRECIPE_20260526_V1.0" }
        };

        var result = RecipeScriptResolver.Resolve(scripts, "MyRecipe");

        Assert.Equal("MYRECIPE_20260526_V1.0", result);
    }
}
