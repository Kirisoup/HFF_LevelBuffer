using System.Collections;
using AccEmit;
using HarmonyLib;
using HumanAPI;
using Multiplayer;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

internal static class Multiplayer_App_EnterLobbyAsync
{
	static readonly (Func<App, AssetBundle> get, Action<App, AssetBundle> set) __lobbyAssetbundle =
		AccessTools.Field(typeof(App), "lobbyAssetbundle")
			?.EmitPair<App, AssetBundle>()
			?? throw new NullReferenceException(nameof(__lobbyAssetbundle));

	static readonly (Func<App, ulong> get, Action<App, ulong> set) __previousLobbyID =
		AccessTools.Field(typeof(App), "previousLobbyID")
			?.EmitPair<App, ulong>()
			?? throw new NullReferenceException(nameof(__previousLobbyID));

	static readonly (Func<App, Action> get, Action<App, Action> set) __queueAfterLevelLoad =
		AccessTools.Field(typeof(App), "queueAfterLevelLoad")
			?.EmitPair<App, Action>()
			?? throw new NullReferenceException(nameof(__queueAfterLevelLoad));

	static readonly Action<(App, bool)> __UpdateJoinable_call = 
		AccessTools.Method(typeof(App), "UpdateJoinable")
			?.EmitVoidCall<(App, bool)>()
			?? throw new NullReferenceException(nameof(__UpdateJoinable_call));

	[HarmonyPatch(typeof(App), "EnterLobbyAsync")]
	[HarmonyPrefix]
	static void Redirect(bool asServer, Action? callback,
		App __instance, ref IEnumerator __result, ref bool __runOriginal)
	{
		__runOriginal = false;
		__result = Impl();

		IEnumerator Impl() {
			NetScope.ClearAllButPlayers();
			lock (App.stateLock) {
				App.state = (!asServer) ? AppSate.ClientLoadLobby : AppSate.ServerLoadLobby;
				__instance.SuspendDeltasForLoad();
				Game.instance.HasSceneLoaded = false;
				string? sceneName = null;
				if (Game.multiplayerLobbyLevel >= 128UL) {
					bool loaded = false;
					WorkshopLevelMetadata? workshopLevel = null;
					WorkshopRepository.instance.levelRepo.LoadLevel(Game.multiplayerLobbyLevel, 
						l => {
							workshopLevel = l;
							loaded = true;
						});
					while (!loaded) yield return null;
					if (workshopLevel is not null) {
						__lobbyAssetbundle.set(__instance, 
							FileTools.LoadBundle(workshopLevel.dataPath));
						var allScenePaths = __lobbyAssetbundle.get(__instance)
							.GetAllScenePaths();
						sceneName = Path.GetFileNameWithoutExtension(allScenePaths[0]);
						App.StopPlaytimeForItem(__previousLobbyID.get(__instance));
						App.StartPlaytimeForItem(workshopLevel.workshopId);
						__previousLobbyID.set(__instance, workshopLevel.workshopId);
					} else if (!NetGame.isServer) {
						SubtitleManager.instance.ClearProgress();
						uDebug.Log("Level load failed.");
						App.instance.ServerFailedToLoad();
						SignalManager.EndReset();
						yield break;
					}
				}
				else sceneName = WorkshopRepository.GetLobbyFilename(Game.multiplayerLobbyLevel);
				if (string.IsNullOrEmpty(sceneName)) {
					sceneName = WorkshopRepository.GetLobbyFilename(0UL);
					Game.multiplayerLobbyLevel = 0UL;
				}

				// AsyncOperation loader = SceneManager.LoadSceneAsync(sceneName);
				// if (loader != null) {
				// 	while (!loader.isDone || !Game.instance.HasSceneLoaded) yield return null;
				// }

#region mod
				bool ok = false;
				LevelBuffer.LoadLevelAdapter(sceneName!, 
					fallback: () => {
						var op = SceneManager.LoadSceneAsync(sceneName);
						Plugin.Instance.Await(
							condition: () => op.isDone && Game.instance.HasSceneLoaded,
							onFinish: () => ok = Game.instance.HasSceneLoaded = true);
					},
					onFinish: () => ok = true);
				while (!ok || !Game.instance.HasSceneLoaded) yield return null; 
#endregion mod

				if (App.state != AppSate.ServerLoadLobby && App.state != AppSate.ClientLoadLobby)
					uDebug.Log("Exiting wrong app state (" + App.state.ToString() + ")");
				App.state = (!asServer) ? AppSate.ClientLobby : AppSate.ServerLobby;
				__instance.ResumeDeltasAfterLoad();
				if (!RatingMenu.instance.ShowRatingMenu())
					MenuSystem.instance.ShowMainMenu<MultiplayerLobbyMenu>(false);
				Game.instance.state = GameState.Inactive;
				__UpdateJoinable_call((__instance, false));
				callback?.Invoke();
				if (__queueAfterLevelLoad.get(__instance) is not null) {
					Action action = __queueAfterLevelLoad.get(__instance);
					__queueAfterLevelLoad.set(__instance, null!);
					if (NetGame.netlog) uDebug.Log("Executing queue");
					action();
				}
				if (__lobbyAssetbundle.get(__instance) is not null) {
					__lobbyAssetbundle.get(__instance).Unload(false);
					__lobbyAssetbundle.set(__instance, null!);
				}
				Game.instance.FixAssetBundleImport(true);
			}
			yield break;
		}
	}
}
