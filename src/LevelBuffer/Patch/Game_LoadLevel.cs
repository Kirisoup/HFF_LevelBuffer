using HarmonyLib;
using UnityEngine.SceneManagement;
using AccEmit;
using System.Collections;

namespace LevelBuffer.Patch;

internal static partial class Game_LoadLevel
{
	static readonly RefFunc<Game, Color> __skyColor_ref = 
		AccessTools.Field(typeof(Game), "skyColor")
			?.EmitLoadAddr<Game, Color>()
			?? throw new NullReferenceException(nameof(__skyColor_ref));

	static readonly RefFunc<Game, AssetBundle> __bundle_ref = 
		AccessTools.Field(typeof(Game), "bundle")
			?.EmitLoadAddr<Game, AssetBundle>()
			?? throw new NullReferenceException(nameof(__bundle_ref));
	
	static readonly Action<Game, Scene> __FixupLoadedBundle = 
		AccessTools.Method(typeof(Game), "FixupLoadedBundle")
			?.CreateDelegate<Action<Game, Scene>>()
			?? throw new NullReferenceException(nameof(__FixupLoadedBundle));

	[HarmonyPatch(typeof(Game), "LoadLevel")]
	[HarmonyPrefix]
	static void Redirect(
		string levelId, ulong levelNumber, int checkpointNumber, int checkpointSubObjectives, 
		Action onComplete, WorkshopItemSource levelType,
		Game __instance, ref IEnumerator __result, ref bool __runOriginal) 
	{
		__runOriginal = false;
		__result = Impl();

		IEnumerator Impl() {
			bool localLevel = false;
			Multiplayer.NetScope.ClearAllButPlayers();
			__instance.BeforeLoad();
			__skyColor_ref(__instance) = RenderSettings.ambientLight;
			__instance.skyboxMaterial = RenderSettings.skybox;
			__instance.state = GameState.LoadingLevel;
			bool isBundle = 
				!string.IsNullOrEmpty(levelId) || 
				(levelNumber > 32UL && levelNumber != ulong.MaxValue);
			if (isBundle) {
				if (string.IsNullOrEmpty(levelId)) {
					// Repo.GetLevel is synchronous, no await required
					// bool loaded2 = false;
					WorkshopRepository.instance.levelRepo.GetLevel(
						levelNumber, levelType, 
						l => {
							__instance.workshopLevel = l;
							// loaded2 = true;
						});
					// while (!loaded2) yield return null;
				} else {
					localLevel = levelId.StartsWith("lvl:");
					__instance.workshopLevel = WorkshopRepository.instance.levelRepo
						.GetItem(levelId);
				}
				RatingMenu.instance.LoadInit();
				if (!localLevel && __instance.workshopLevel != null) {
					Multiplayer.App.StartPlaytimeForItem(__instance.workshopLevel.workshopId);
					RatingMenu.instance.QueryRatingStatus(__instance.workshopLevel.workshopId, 
						true);
				}
			}
			Multiplayer.App.StartPlaytimeLocalPlayers();
			if (Plugin.AllowReload || __instance.currentLevelNumber != (int)levelNumber)
			{
				var oldLevel = Game.currentLevel;
				SubtitleManager.instance.SetProgress(I2.Loc.ScriptLocalization.TUTORIAL.LOADING);
				Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
				string sceneName = string.Empty;
				__instance.currentLevelType = levelType;
				switch (levelType) {
				case WorkshopItemSource.BuiltIn:
					sceneName = __instance.levels[(int)checked((IntPtr)levelNumber)];
					break;
				case WorkshopItemSource.EditorPick:
					uDebug.Log("Loading editor pick level");
					sceneName = __instance.editorPickLevels[(int)levelNumber];
					break;
				// case WorkshopItemSource.Subscription:
				// case WorkshopItemSource.LocalWorkshop:
				// case WorkshopItemSource.BuiltInLobbies:
				// case WorkshopItemSource.SubscriptionLobbies:
				default:
					// if (levelType != WorkshopItemSource.NotSpecified);
					break;
				}
				uDebug.Log("scename = " + sceneName);
				if (!isBundle) {
					if (string.IsNullOrEmpty(sceneName)) {
						sceneName = __instance.levels[(int)checked((IntPtr)levelNumber)];
					}
				} else {
					if (!localLevel && __instance.workshopLevel != null) {
						// bool loaded = false;
						WorkshopRepository.instance.levelRepo.LoadLevel(
							__instance.workshopLevel.workshopId, 
							l => {
								__instance.workshopLevel = l;
								// loaded = true;
							});
						// while (!loaded) yield return null;
					}

					ref AssetBundle __bundle = ref __bundle_ref(__instance);

					__bundle = null!;
					if (__instance.workshopLevel != null) {
						__bundle = FileTools.LoadBundle(__instance.workshopLevel.dataPath);
					}
					if (__bundle == null) {
						SubtitleManager.instance.ClearProgress();
						uDebug.Log("Level load failed.");
						Multiplayer.App.instance.ServerFailedToLoad();
						HumanAPI.SignalManager.EndReset();
						yield break;
					}
					string[] scenePath = __bundle.GetAllScenePaths();
					if (string.IsNullOrEmpty(sceneName)) {
						sceneName = Path.GetFileNameWithoutExtension(scenePath[0]);
					}
				}
				Game.instance.HasSceneLoaded = false;
				var loader = SwitchAssetBundle.LoadSceneAsync(sceneName);
				if (loader == null) {
					// var sceneLoader = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
					// while (!sceneLoader.isDone || !Game.instance.HasSceneLoaded) yield return null;
					// sceneLoader = null;

#region mod
					bool ok = false;
					bool adapted = BufferManager.Load(
						sceneName,
						onFinish: () => ok = true);

					if (adapted) {
						while (!ok) yield return null;
					} else {
						var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
						while (!op.isDone) yield return null;
					}
#endregion mod

					SubtitleManager.instance.SetProgress(I2.Loc.ScriptLocalization.TUTORIAL.LOADING, 
						1f, 1f);
					__instance.currentLevelNumber = (int)levelNumber;
				} else {
					while (!loader.isDone || !Game.instance.HasSceneLoaded) yield return null;
					loader = null!;
					SubtitleManager.instance.SetProgress(I2.Loc.ScriptLocalization.TUTORIAL.LOADING, 
						1f, 1f);
					__instance.currentLevelNumber = (int)levelNumber;
				}
			}
			if (Game.currentLevel == null) {
				HumanAPI.SignalManager.EndReset();
				onComplete?.Invoke();
				yield break;
			}
			if (isBundle) {
				__FixupLoadedBundle(__instance, SceneManager.GetActiveScene());
				__bundle_ref(__instance).Unload(false);
			}
			if (__instance.currentLevelNumber >= 0 && !isBundle && 
				levelType != WorkshopItemSource.EditorPick) {
				HumanAnalytics.instance.LoadLevel(__instance.levels[__instance.currentLevelNumber], 
					(int)levelNumber, checkpointNumber, 0f);
			}
			__instance.FixAssetBundleImport(false);
			__instance.AfterLoad(checkpointNumber, checkpointSubObjectives);
			if (Multiplayer.NetGame.isLocal){
				if (levelType == WorkshopItemSource.BuiltIn && 
					__instance.currentLevelNumber < __instance.levelCount - 1)
				{
					GameSave.PassCheckpointCampaign((uint)__instance.currentLevelNumber, 
						checkpointNumber, checkpointSubObjectives);
				}
				if (levelType == WorkshopItemSource.EditorPick) {
					GameSave.PassCheckpointEditorPick((uint)__instance.currentLevelNumber, 
						checkpointNumber, checkpointSubObjectives);
				}
			}
			onComplete?.Invoke();
			yield break;
		}
	}

}
