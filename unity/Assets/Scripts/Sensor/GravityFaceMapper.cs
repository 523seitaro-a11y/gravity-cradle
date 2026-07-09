public static class GravityFaceMapper
{
    public static GravityFace ParseFace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return GravityFace.Unknown;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "down": return GravityFace.Down;
            case "up": return GravityFace.Up;
            case "left": return GravityFace.Left;
            case "right": return GravityFace.Right;
            case "front":
            case "forward": return GravityFace.Front;
            case "back":
            case "backward": return GravityFace.Back;
            default: return GravityFace.Unknown;
        }
    }
}
