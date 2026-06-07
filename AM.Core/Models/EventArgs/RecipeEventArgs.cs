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
    /// <summary>Recipe vừa được load/switch (đa hình theo loại máy).</summary>
    public RecipeBase Recipe { get; }

    public RecipeEventArgs(RecipeBase recipe)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
    }
}
