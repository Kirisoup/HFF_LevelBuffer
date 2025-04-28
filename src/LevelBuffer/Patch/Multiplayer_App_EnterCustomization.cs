using HarmonyLib;
using Multiplayer;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

internal static class Multiplayer_App_EnterCustomization
{
	const string scene = "Customization";

	[HarmonyPatch(typeof(App), "EnterCustomization")]
	[HarmonyPrefix]
	static void Redirect(Action controllerLoaded, ref bool __runOriginal)
	{
		__runOriginal = false;
		lock (App.stateLock) {
			App.state = AppSate.Customize;
			CustomizationController.onInitialized = controllerLoaded;
			LevelBuffer.LoadLevelAdapter(scene, 
				fallback: () => SceneManager.LoadSceneAsync(scene));
		}
	}
}
