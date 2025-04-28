using AccEmit;
using HarmonyLib;
using Multiplayer;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

internal static class Multiplayer_App_EnterMenu
{
	static readonly Action<(App, bool)> __ExitLobby_call = 
		AccessTools.Method(typeof(App), "ExitLobby")
			?.EmitVoidCall<(App, bool)>()
			?? throw new NullReferenceException(nameof(__ExitLobby_call));

	[HarmonyPatch(typeof(App), "EnterMenu")]
	[HarmonyPrefix]
	static void Redirect(App __instance, ref bool __runOriginal) 
	{
		__runOriginal = false;

		lock (App.stateLock) {
			switch (App.state) {
			case AppSate.Menu: return;
			case AppSate.ClientLobby or AppSate.ServerLobby:
				__ExitLobby_call((__instance, false));
				goto case3;
			case AppSate.Customize or AppSate.PlayLevel 
			or AppSate.ClientLobby or AppSate.ServerLobby: case3:
				Game.instance.HasSceneLoaded = false;
				LevelBuffer.LoadLevelAdapter("Empty", 
					() => SceneManager.LoadSceneAsync("Empty"));
				goto default;
			case AppSate.Startup:
				Game.instance.HasSceneLoaded = false;
				SceneManager.LoadScene("Empty");
				goto default;
			default:
				Game.instance.state = GameState.Inactive;
				App.state = AppSate.Menu;
				return;
			}
		}
	}
}