using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Saves;

namespace QuickReload;

/// <summary>
/// LanMultiplayer-MS 모드가 로드된 환경에서 reflection을 통해 LAN 멀티 재시작을 처리합니다.
/// LanMultiplayer-MS가 없는 환경에서는 아무 동작도 하지 않습니다.
/// </summary>
internal static class LanMultiplayerBridge
{
    private const string AssemblyName = "LanMultiplayer-MS";
    private const string RootNamespace = "SlayTheSpire2.LAN.Multiplayer.Reforged";

    private static readonly Type? LanRunSaveManagerServiceType =
        Type.GetType($"{RootNamespace}.Services.LanRunSaveManagerService, {AssemblyName}");
    private static readonly Type? LanHostHelperType =
        Type.GetType($"{RootNamespace}.Helpers.LanHostHelper, {AssemblyName}");
    private static readonly Type? SettingsServiceType =
        Type.GetType($"{RootNamespace}.Services.SettingsService, {AssemblyName}");

    private static readonly MethodInfo? StartHostMethod = LanHostHelperType?.GetMethod(
        "StartHost",
        [typeof(SerializableRun), typeof(Control), typeof(NSubmenuStack), typeof(ushort), typeof(int)]);

    /// <summary>
    /// LAN 멀티 런 세이브가 존재하면 LAN 호스트로 재시작합니다.
    /// </summary>
    /// <returns>LAN 분기로 처리했으면 true, 아니면 false</returns>
    public static bool TryRestartAsLanHost(NMainMenu mainMenu)
    {
        if (LanRunSaveManagerServiceType == null || LanHostHelperType == null || SettingsServiceType == null || StartHostMethod == null)
            return false;

        var lanServiceInstance = LanRunSaveManagerServiceType.GetProperty("Instance")!.GetValue(null);
        var hasLanSave = (bool)LanRunSaveManagerServiceType.GetProperty("HasMultiplayerRunSave")!.GetValue(lanServiceInstance)!;
        if (!hasLanSave)
            return false;

        Log.Info("[QUICKRELOAD]: LAN multiplayer run detected, using LanHostHelper.");

        var localPlayerId = PlatformUtil.GetLocalPlayerId(PlatformType.None);
        Log.Info($"[QUICKRELOAD]: localPlayerId={localPlayerId}");

        var readSaveResult = (ReadSaveResult<SerializableRun>)LanRunSaveManagerServiceType
            .GetMethod("LoadAndCanonicalizeMultiplayerRunSave")!
            .Invoke(lanServiceInstance, [localPlayerId])!;
        Log.Info($"[QUICKRELOAD]: readSaveResult.Success={readSaveResult.Success}");

        if (!readSaveResult.Success || readSaveResult.SaveData == null)
        {
            Log.Warn("[QUICKRELOAD]: Broken LAN multiplayer run save detected.");
            return true;
        }

        var settingsInstance = SettingsServiceType.GetProperty("Instance")!.GetValue(null);
        var settingsModel = SettingsServiceType.GetField("SettingsModel")!.GetValue(settingsInstance)!;
        var port = (ushort)settingsModel.GetType().GetProperty("HostPort")!.GetValue(settingsModel)!;
        var maxPlayers = (int)settingsModel.GetType().GetProperty("HostMaxPlayers")!.GetValue(settingsModel)!;

        var stack = mainMenu.SubmenuStack;
        var dummyOverlay = new Control();
        QuickReloadState.SetAutoReady(true);
        StartHostMethod.Invoke(null, [readSaveResult.SaveData, dummyOverlay, stack, port, maxPlayers]);
        return true;
    }
}