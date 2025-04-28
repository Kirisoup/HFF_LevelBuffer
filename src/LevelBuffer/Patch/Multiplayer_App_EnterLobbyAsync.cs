using System.Collections;
using AccEmit;
using HarmonyLib;
using HumanAPI;
using Multiplayer;
using UnityEngine.Assertions.Must;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

internal static class Multiplayer_App_EnterLobbyAsync
{
	static Multiplayer_App_EnterLobbyAsync() {
		var fi__lobbyAssetbundle = AccessTools.Field(typeof(App), "lobbyAssetbundle");
		ArgumentNullException.ThrowIfNull(fi__lobbyAssetbundle, nameof(fi__lobbyAssetbundle));
		get__lobbyAssetbundle = Emit.Ldfld<App, AssetBundle>(fi__lobbyAssetbundle);
		set__lobbyAssetbundle = Emit.Stfld<App, AssetBundle>(fi__lobbyAssetbundle);

		var fi__previousLobbyID = AccessTools.Field(typeof(App), "previousLobbyID");
		ArgumentNullException.ThrowIfNull(fi__previousLobbyID, nameof(fi__previousLobbyID));
		get__previousLobbyID = Emit.Ldfld<App, ulong>(fi__previousLobbyID);
		set__previousLobbyID = Emit.Stfld<App, ulong>(fi__previousLobbyID);

		var fi__queueAfterLevelLoad = AccessTools.Field(typeof(App), "queueAfterLevelLoad");
		ArgumentNullException.ThrowIfNull(fi__queueAfterLevelLoad, nameof(fi__queueAfterLevelLoad));
		get__queueAfterLevelLoad = Emit.Ldfld<App, Action>(fi__queueAfterLevelLoad);
		set__queueAfterLevelLoad = Emit.Stfld<App, Action>(fi__queueAfterLevelLoad);

		var mi__previousLobbyID = AccessTools.Method(typeof(App), "UpdateJoinable");
		ArgumentNullException.ThrowIfNull(mi__previousLobbyID, nameof(mi__previousLobbyID));
		call__UpdateJoinable = Emit.CallVoid<(App, bool)>(mi__previousLobbyID);

	}

	static readonly Func<App, AssetBundle> get__lobbyAssetbundle;
	static readonly Action<App, AssetBundle> set__lobbyAssetbundle;

	static readonly Func<App, ulong> get__previousLobbyID;
	static readonly Action<App, ulong> set__previousLobbyID;

	static readonly Func<App, Action> get__queueAfterLevelLoad;
	static readonly Action<App, Action> set__queueAfterLevelLoad;

	static readonly Action<(App, bool)> call__UpdateJoinable;

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
						set__lobbyAssetbundle(__instance, 
							FileTools.LoadBundle(workshopLevel.dataPath));
						var allScenePaths = get__lobbyAssetbundle(__instance)
							.GetAllScenePaths();
						sceneName = Path.GetFileNameWithoutExtension(allScenePaths[0]);
						App.StopPlaytimeForItem(get__previousLobbyID(__instance));
						App.StartPlaytimeForItem(workshopLevel.workshopId);
						set__previousLobbyID(__instance, workshopLevel.workshopId);
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
				call__UpdateJoinable((__instance, false));
				callback?.Invoke();
				if (get__queueAfterLevelLoad(__instance) is not null) {
					Action action = get__queueAfterLevelLoad(__instance);
					set__queueAfterLevelLoad(__instance, null!);
					if (NetGame.netlog) uDebug.Log("Executing queue");
					action();
				}
				if (get__lobbyAssetbundle(__instance) is not null) {
					get__lobbyAssetbundle(__instance).Unload(false);
					set__lobbyAssetbundle(__instance, null!);
				}
				Game.instance.FixAssetBundleImport(true);
			}
			yield break;
		}
	}

}
