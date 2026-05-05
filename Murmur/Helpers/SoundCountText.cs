namespace Murmur.Helpers;

public static class SoundCountText
{
    public static string Format(int count) => count switch
    {
        0 => "no sounds",
        1 => "1 sound",
        _ => $"{count} sounds",
    };
}
