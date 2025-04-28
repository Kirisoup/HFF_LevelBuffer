using HarmonyLib;
using HumanAPI;
using I2.Loc;
using Multiplayer;
using System.Collections;
using UnityEngine.SceneManagement;
using AccEmit;

namespace LevelBuffer.Patch;

internal static class Game_LoadLevel
{
	static readonly Action<Game, Color> set_skyColor;

	static readonly Func<Game, AssetBundle> get_bundle;
	static readonly Action<Game, AssetBundle> set_bundle;
	
	static readonly Action<(Game, Scene)> call_FixupLoadedBundle;

	static Game_LoadLevel() {
		var fi__skyColor = AccessTools.Field(typeof(Game), "skyColor");
		set_skyColor = Emit.Stfld<Game, Color>(fi__skyColor);

		var fi__bundle = AccessTools.Field(typeof(Game), "bundle");
		get_bundle = Emit.Ldfld<Game, AssetBundle>(fi__bundle);
		set_bundle = Emit.Stfld<Game, AssetBundle>(fi__bundle);

		var mi__FixupLoadedBundle = AccessTools.Method(typeof(Game), "FixupLoadedBundle");
		call_FixupLoadedBundle = Emit.CallVoid<(Game, Scene)>(mi__FixupLoadedBundle);
	}

	[HarmonyPatch(typeof(Game), "LoadLevel")]
	[HarmonyPrefix]
	static void Redirect(
		string levelId, ulong levelNumber, int checkpointNumber, int checkpointSubObjectives, 
		Action onComplete, WorkshopItemSource levelType,
		Game __instance, ref IEnumerator __result, ref bool __runOriginal) 
	{
		__runOriginal = false;
		__result = Iter();

		IEnumerator Iter() {
			bool localLevel = false;
			NetScope.ClearAllButPlayers();
			__instance.BeforeLoad();
			set_skyColor(__instance, RenderSettings.ambientLight);
			__instance.skyboxMaterial = RenderSettings.skybox;
			__instance.state = GameState.LoadingLevel;
			bool isBundle = 
				!string.IsNullOrEmpty(levelId) || 
				(levelNumber > 32UL && levelNumber != ulong.MaxValue);
			if (isBundle) {
				if (string.IsNullOrEmpty(levelId)) {
					bool loaded2 = false;
					WorkshopRepository.instance.levelRepo.GetLevel(
						levelNumber, levelType, 
						l => {
							__instance.workshopLevel = l;
							loaded2 = true;
						});
					while (!loaded2) yield return null;
				} else {
					localLevel = levelId.StartsWith("lvl:");
					__instance.workshopLevel = WorkshopRepository.instance.levelRepo
						.GetItem(levelId);
				}
				RatingMenu.instance.LoadInit();
				if (!localLevel && __instance.workshopLevel != null) {
					App.StartPlaytimeForItem(__instance.workshopLevel.workshopId);
					RatingMenu.instance.QueryRatingStatus(__instance.workshopLevel.workshopId, 
						true);
				}
			}
			App.StartPlaytimeLocalPlayers();
			if (__instance.currentLevelNumber != (int)levelNumber)
			{
				var oldLevel = Game.currentLevel;
				SubtitleManager.instance.SetProgress(ScriptLocalization.TUTORIAL.LOADING);
				Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
				string sceneName = string.Empty;
				__instance.currentLevelType = levelType;
				switch (levelType)
				{
				case WorkshopItemSource.BuiltIn:
					sceneName = __instance.levels[(int)checked((IntPtr)levelNumber)];
					break;
				case WorkshopItemSource.EditorPick:
					Debug.Log("Loading editor pick level");
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
				Debug.Log("scename = " + sceneName);
				if (!isBundle) {
					if (string.IsNullOrEmpty(sceneName)) {
						sceneName = __instance.levels[(int)checked((IntPtr)levelNumber)];
					}
				} else {
					if (!localLevel && __instance.workshopLevel != null) {
						bool loaded = false;
						WorkshopRepository.instance.levelRepo.LoadLevel(
							__instance.workshopLevel.workshopId, 
							l => {
								__instance.workshopLevel = l;
								loaded = true;
							});
						while (!loaded) yield return null;
					}
					set_bundle(__instance, null!);
					if (__instance.workshopLevel != null) {
						set_bundle(__instance, FileTools.LoadBundle(__instance.workshopLevel
							.dataPath));
					}
					if (get_bundle(__instance) == null) {
						SubtitleManager.instance.ClearProgress();
						Debug.Log("Level load failed.");
						App.instance.ServerFailedToLoad();
						SignalManager.EndReset();
						yield break;
					}
					string[] scenePath = get_bundle(__instance).GetAllScenePaths();
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
#region modification
					bool isDone = false;
					LevelBuffer.LoadLevel(sceneName, () => isDone = true);
					while (!isDone) yield return null;
#endregion modification

					SubtitleManager.instance.SetProgress(ScriptLocalization.TUTORIAL.LOADING, 
						1f, 1f);
					__instance.currentLevelNumber = (int)levelNumber;
				} else {
					while (!loader.isDone || !Game.instance.HasSceneLoaded) yield return null;
					loader = null!;
					SubtitleManager.instance.SetProgress(ScriptLocalization.TUTORIAL.LOADING, 
						1f, 1f);
					__instance.currentLevelNumber = (int)levelNumber;
				}
			}
			if (Game.currentLevel == null) {
				SignalManager.EndReset();
				onComplete?.Invoke();
				yield break;
			}
			if (isBundle) {
				call_FixupLoadedBundle((__instance, SceneManager.GetActiveScene()));
				get_bundle(__instance).Unload(false);
			}
			if (__instance.currentLevelNumber >= 0 && !isBundle && 
				levelType != WorkshopItemSource.EditorPick) {
				HumanAnalytics.instance.LoadLevel(__instance.levels[__instance.currentLevelNumber], 
					(int)levelNumber, checkpointNumber, 0f);
			}
			__instance.FixAssetBundleImport(false);
			__instance.AfterLoad(checkpointNumber, checkpointSubObjectives);
			if (NetGame.isLocal){
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
