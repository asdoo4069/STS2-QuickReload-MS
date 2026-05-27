using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace QuickReload;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
static class QuickReloadMainMenuPatch
{
    static void Postfix(NMainMenu __instance)
    {
        if (!QuickReloadState.TryConsumePendingRestart(out var playerId))
        {
            Log.Info("[QUICKRELOAD]: Tried to consume pending restart on main menu, but none was pending.");
            return;
        }

        Log.Info($"[QUICKRELOAD]: Consumed pending restart on main menu. playerId={playerId}");

        if (CommandLineHelper.HasArg("fastmp") || CommandLineHelper.HasArg("clientId"))
        {
            QuickReloadState.SetAutoReady(true);
            __instance.OpenMultiplayerSubmenu().OnJoinFriendsPressed();
            Log.Info("[QUICKRELOAD]: Using fastmp quick-restart join flow via Join Friends screen.");
            return;
        }

        if (QuickReloadUtil.IsMobilePlatform())
        {
            Log.Info("[QUICKRELOAD]: Detected mobile platform, using mobile quick-restart join flow.");

            var hostIp = QuickReloadState.GetPendingHostIp();
            Log.Info($"[QUICKRELOAD]: hostIp={hostIp}");

            if (string.IsNullOrEmpty(hostIp))
            {
                Log.Warn("[QUICKRELOAD]: hostIp is empty, aborting mobile reconnect.");
                return;
            }

            var submenu = __instance.OpenMultiplayerSubmenu();
            var joinScreen = submenu.OnJoinFriendsPressed();
            QuickReloadState.SetAutoReady(true);
            var initializer = new ENetClientConnectionInitializer(1000, hostIp, 33771); // netId 하드코딩
            TaskHelper.RunSafely(joinScreen.JoinGameAsync(initializer));
            return;
        }

        var steamInitializer = SteamClientConnectionInitializer.FromPlayer(playerId);
        if (steamInitializer == null)
        {
            Log.Warn("[QUICKRELOAD]: Failed to create Steam connection initializer from player ID, aborting quick restart.");
            return;
        }

        QuickReloadState.SetAutoReady(true);
        TaskHelper.RunSafely(__instance.JoinGame(steamInitializer));
    }
}
