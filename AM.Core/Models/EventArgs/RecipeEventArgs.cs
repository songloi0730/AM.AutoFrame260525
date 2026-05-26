// -------------------------------------------------------
// File:    RecipeEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs wrapper cho Recipe — dùng với EventHandler<RecipeEventArgs>
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>
/// EventArgs cho sự kiện RecipeChanged.
/// </summary>
public sealed class RecipeEventArgs : System.EventArgs
{
    /// <summary>Recipe vừa được load/switch.</summary>
    public Recipe Recipe { get; }

    public RecipeEventArgs(Recipe recipe)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
    }
}
