using Godot;

namespace QuickReload;

static class QuickReloadUtil
{
    public static bool IsMobilePlatform()
    {
        return OS.GetName() == "Android" || OS.GetName() == "iOS";
    }
}