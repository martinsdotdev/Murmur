using System;

namespace Murmur.Models;

public enum SoundKind
{
    BuiltInOgg,
    CustomOgg,
    Streaming,
}

public sealed record Sound(
    string Id,
    string DisplayName,
    string Category,
    Uri SourceUri,
    SoundKind Kind = SoundKind.BuiltInOgg)
{
    public bool IsCustom => Kind != SoundKind.BuiltInOgg;
}
