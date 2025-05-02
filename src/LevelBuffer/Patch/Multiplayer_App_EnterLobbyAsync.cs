using System.Collections;
using AccEmit;
using HarmonyLib;
using HumanAPI;
using Multiplayer;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

internal static class Multiplayer_App_EnterLobbyAsync
{
	static readonly RefFunc<App, AssetBundle> __lobbyAssetbundle_ref =
		AccessTools.Field(typeof(App), "lobbyAssetbundle")
			?.EmitLoadAddr<App, AssetBundle>()
			?? throw new NullReferenceException(nameof(__lobbyAssetbundle_ref));

	static readonly RefFunc<App, ulong> __previousLobbyID_ref =
		AccessTools.Field(typeof(App), "previousLobbyID")
			?.EmitLoadAddr<App, ulong>()
			?? throw new NullReferenceException(nameof(__previousLobbyID_ref));

	static readonly RefFunc<App, Action> __queueAfterLevelLoad_ref =
		AccessTools.Field(typeof(App), "queueAfterLevelLoad")
			?.EmitLoadAddr<App, Action>()
			?? throw new NullReferenceException(nameof(__queueAfterLevelLoad_ref));

	static readonly Action<App, bool> __UpdateJoinable = 
		AccessTools.Method(typeof(App), "UpdateJoinable")
			?.CreateDelegate<Action<App, bool>>()
			?? throw new NullReferenceException(nameof(__UpdateJoinable));

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

						ref AssetBundle __lobbyAssetbundle = ref __lobbyAssetbundle_ref(__instance);

						__lobbyAssetbundle = FileTools.LoadBundle(workshopLevel.dataPath);
						var allScenePaths = __lobbyAssetbundle.GetAllScenePaths();
						sceneName = Path.GetFileNameWithoutExtension(allScenePaths[0]);

						ref ulong __previousLobbyID = ref __previousLobbyID_ref(__instance);

						App.StopPlaytimeForItem(__previousLobbyID);
						App.StartPlaytimeForItem(workshopLevel.workshopId);
						__previousLobbyID = workshopLevel.workshopId;
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
				bool adapted = BufferManager.Load(
					sceneName!,
					onFinish: () => ok = true);
				
				if (adapted) {
					while (!ok) yield return null;
				} else {
					var op = SceneManager.LoadSceneAsync(sceneName);
					while (!op.isDone) yield return null;
				}
#endregion mod

				if (App.state != AppSate.ServerLoadLobby && App.state != AppSate.ClientLoadLobby) {
					uDebug.Log("Exiting wrong app state (" + App.state.ToString() + ")");
				}
				App.state = (!asServer) ? AppSate.ClientLobby : AppSate.ServerLobby;
				__instance.ResumeDeltasAfterLoad();
				if (!RatingMenu.instance.ShowRatingMenu()) {
					MenuSystem.instance.ShowMainMenu<MultiplayerLobbyMenu>(false);
				}
				Game.instance.state = GameState.Inactive;
				__UpdateJoinable(__instance, false);
				callback?.Invoke();

				ref Action __queueAfterLevelLoad = ref __queueAfterLevelLoad_ref(__instance);

				if (__queueAfterLevelLoad is not null) {
					Action action = __queueAfterLevelLoad;
					__queueAfterLevelLoad = null!;
					if (NetGame.netlog) uDebug.Log("Executing queue");
					action();
				}

				ref AssetBundle __lobbyAssetbundle1 = ref __lobbyAssetbundle_ref(__instance);

				if (__lobbyAssetbundle1 is not null) {
					__lobbyAssetbundle1.Unload(false);
					__lobbyAssetbundle1 = null!;
				}
				Game.instance.FixAssetBundleImport(true);
			}
			yield break;
		}
	}
}
