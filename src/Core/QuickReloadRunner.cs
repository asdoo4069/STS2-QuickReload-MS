using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Platform.Steam;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using QuickReload.Multiplayer;

namespace QuickReload;

static class QuickReloadRunner
{
    public static Task RestartAsync()
    {
        return RestartAsync(null);
    }

    public static async Task RestartAsync(NPauseMenu? pauseMenu)
    {
        if (RunManager.Instance.IsSingleplayerOrFakeMultiplayer)
        {
            Log.Info("[QUICKRELOAD]: Single player run detected, restarting.");
            if (pauseMenu != null)
            {
                DisablePauseMenuButtons(pauseMenu);
            }
            bool ret = await RestartSinglePlayer();
            if (ret && pauseMenu != null)
            {
                Log.Warn("[QUICKRELOAD]: RestartSinglePlayer returned early with ret=true, enabling pause menu buttons.");
                EnablePauseMenuButtons(pauseMenu);
            }
        }
        else
        {
            Log.Info("[QUICKRELOAD]: Multiplayer run detected, restarting.");
            if (pauseMenu != null)
            {
                DisablePauseMenuButtons(pauseMenu);
            }
            bool ret = await RestartMultiPlayer();
            if (ret && pauseMenu != null)
            {
                Log.Warn("[QUICKRELOAD]: RestartMultiPlayer returned early with ret=true, enabling pause menu buttons.");
                EnablePauseMenuButtons(pauseMenu);
            }
        }
    }

    private static async Task<bool> RestartSinglePlayer()
    {
        SerializableRun serializableRun;
        RunState runState;

        await WaitForPendingSave();
        try
        {
            ReadSaveResult<SerializableRun>? readRunSaveResult = SaveManager.Instance.LoadRunSave();
            serializableRun = readRunSaveResult.SaveData
                              ?? throw new InvalidOperationException("[QUICKRELOAD]: Run save data was null.");
            runState = RunState.FromSerializable(serializableRun);
        }
        catch (Exception ex)
        {
            Log.Error($"[QUICKRELOAD]: Save validation failed: {ex}");
            return true;
        }

        var game = NGame.Instance ??
                   throw new InvalidOperationException("NGame.Instance was null during quick restart.");

        var runMusicController = NRunMusicController.Instance;
        runMusicController?.StopMusic();
        await game.Transition.FadeOut(0.3f);
        RunManager.Instance.CleanUp();

        try
        {
            await RunManager.Instance.SetUpSavedSingleplayer(runState, serializableRun);
            SfxCmd.Play(runState.Players[0].Character.CharacterTransitionSfx);
            game.ReactionContainer.InitializeNetworking((INetGameService)new NetSingleplayerGameService());
            await game.LoadRun(runState, serializableRun.PreFinishedRoom);
        }
        catch (Exception ex)
        {
            Log.Error($"[QUICKRELOAD]: Run load failed after cleanup: {ex}");
            await game.ReturnToMainMenu();
        }
        return false;
    }

    private static async Task<bool> RestartMultiPlayer()
    {
        var game = NGame.Instance ??
                   throw new InvalidOperationException("[QUICKRELOAD]: NGame.Instance was null during quick restart.");

        var netService = RunManager.Instance.NetService;
        if (netService is { IsConnected: true })
        {
            ulong playerId = CommandLineHelper.HasArg("fastmp") ? 1003 : Steamworks.SteamUser.GetSteamID().m_SteamID;

            netService.SendMessage(new QuickReloadMessage { playerId = playerId });
            Log.Info($"[QUICKRELOAD]: Sent QuickReloadMessage before cleanup. playerId={playerId}");
        }
        else
        {
            Log.Warn("[QUICKRELOAD]: Net service not connected before cleanup, skipping QuickReloadMessage.");
        }

        await game.Transition.FadeOut(0.3f);
        RunManager.Instance.CleanUp();
        await WaitForPendingSave();

        var mainMenu = NMainMenu.Create(false);
        game.RootSceneContainer.SetCurrentScene(mainMenu);


        // LAN 분기
        if (LanMultiplayerBridge.TryRestartAsLanHost(mainMenu))
            return false;

        NMultiplayerSubmenu? multiplayerSubmenu = mainMenu.OpenMultiplayerSubmenu();

        // Steam/fastmp 분기
        var platformType = !SteamInitializer.Initialized || CommandLineHelper.HasArg("fastmp")
            ? PlatformType.None
            : PlatformType.Steam;
        var localPlayerId = PlatformUtil.GetLocalPlayerId(platformType);

        // from NMultiplayerSubmenu
        ReadSaveResult<SerializableRun> readSaveResult =
            SaveManager.Instance.LoadAndCanonicalizeMultiplayerRunSave(localPlayerId);
        if (!readSaveResult.Success || readSaveResult.SaveData == null)
        {
            Log.Warn("[QUICKRELOAD]: Broken multiplayer run save detected, big problem");
            return true;
        }

        QuickReloadState.SetAutoReady(true);
        multiplayerSubmenu.StartHost(readSaveResult.SaveData);
        return false;
    }

    private static async Task WaitForPendingSave()
    {
        var currentRunSaveTask = SaveManager.Instance.CurrentRunSaveTask;
        if (currentRunSaveTask != null)
        {
            Log.Info("[QUICKRELOAD]: Saving in progress, waiting for it to be finished before quick restart.");
            try
            {
                await currentRunSaveTask;
            }
            catch (Exception ex)
            {
                Log.Error($"[QUICKRELOAD]: Save task failed while waiting to quick restart: {ex}");
            }
        }
    }

    private static void DisablePauseMenuButtons(NPauseMenu pauseMenu)
    {
        SetPauseMenuButtonsEnabled(pauseMenu, false);
    }

    private static void EnablePauseMenuButtons(NPauseMenu pauseMenu)
    {
        SetPauseMenuButtonsEnabled(pauseMenu, true);
    }

    private static void SetPauseMenuButtonsEnabled(NPauseMenu pauseMenu, bool enabled)
    {
        var buttonContainer = pauseMenu.GetNode<VBoxContainer>(QuickReloadPauseMenuPatch.ButtonContainerPath);

        foreach (var button in buttonContainer.GetChildren())
        {
            if (button is NPauseMenuButton pauseMenuButton)
            {
                if (enabled)
                {
                    pauseMenuButton.Enable();
                }
                else
                {
                    pauseMenuButton.Disable();
                }
            }
        }
    }
}
