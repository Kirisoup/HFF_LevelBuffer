using HarmonyLib;
using Multiplayer;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

internal static class Multiplayer_App_EnterCustomization
{
	[HarmonyPatch(typeof(App), "EnterCustomization")]
	[HarmonyPrefix]
	static void Redirect(Action controllerLoaded, ref bool __runOriginal)
	{
		__runOriginal = false;
		lock (App.stateLock) {
			App.state = AppSate.Customize;
			CustomizationController.onInitialized = controllerLoaded;
			bool adapted = BufferManager.Load("Customization");
			if (!adapted) SceneManager.LoadSceneAsync("Customization");
		}
	}
}
