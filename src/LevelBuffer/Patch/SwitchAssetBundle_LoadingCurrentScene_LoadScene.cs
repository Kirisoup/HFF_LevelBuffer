using HarmonyLib;

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
		bool adapted = BufferManager.Load(
			__instance.mSceneName,
			() => __instance.mCurrentPhase = LSP.kDone);
		
		if (!adapted) return; 

		__runOriginal = false;
		__instance.mCurrentPhase = (LSP)int.MinValue; // pause SwitchAssetBundle.get_isDone()
	}
}
