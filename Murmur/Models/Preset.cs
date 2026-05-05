using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Murmur.Models;

/// <summary>Named saved mix mapping sound id → volume in 0..1. Volumes of 0 are omitted
/// (treated as "missing == 0"). The default preset's UUID matches Blanket's so settings
/// could theoretically migrate between the two apps.</summary>
public sealed partial class Preset : ObservableObject
{
    public string Id { get; set; } = "";

    [ObservableProperty]
    public partial string VisibleName { get; set; } = "";

    public Dictionary<string, double> SoundVolumes { get; set; } = new();

    [ObservableProperty]
    public partial bool HideInactive { get; set; }

    public Preset DeepClone(string newId, string newName) => new()
    {
        Id = newId,
        VisibleName = newName,
        SoundVolumes = new Dictionary<string, double>(SoundVolumes),
        HideInactive = HideInactive,
    };
}
