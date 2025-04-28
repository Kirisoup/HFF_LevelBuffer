using HarmonyLib;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

using LSP = SwitchAssetBundle.LoadingScenePhase;

internal static class SwitchAssetBundle_LoadingCurrentScene_LoadScene
{
	[HarmonyPatch(
		typeof(SwitchAssetBundle.LoadingCurrentScene), 
		"LoadScene")]
	[HarmonyPrefix]
	static void Redirect(SwitchAssetBundle.LoadingCurrentScene __instance, ref bool __runOriginal) 
	{
		var buffer = LevelBuffer.Current;
		if (buffer is null) return;

		string sceneName = __instance.mSceneName;
		if (buffer.SceneName != sceneName) {
			Plugin.Logger.LogInfo($"cleaning up wrong scene buffer {buffer.SceneName}");
			buffer.Apply();
			return;
		}

		__runOriginal = false;
		__instance.mCurrentPhase = (LSP)int.MinValue; // pause SwitchAssetBundle.get_isDone()

		Plugin.Logger.LogInfo($"loading scene {sceneName} from buffer");
		buffer.Apply(() => __instance.mCurrentPhase = LSP.kDone); // resume as done
	}
}
