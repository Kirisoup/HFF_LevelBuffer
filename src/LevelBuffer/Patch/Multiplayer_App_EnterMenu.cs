using AccEmit;
using HarmonyLib;
using Multiplayer;
using UnityEngine.SceneManagement;

namespace LevelBuffer.Patch;

internal static class Multiplayer_App_EnterMenu
{
	static Multiplayer_App_EnterMenu() {
		var mi__ExitLobby = AccessTools.Method(typeof(App), "ExitLobby");
		ArgumentNullException.ThrowIfNull(mi__ExitLobby, nameof(mi__ExitLobby));
		call__ExitLobby = Emit.CallVoid<(App, bool)>(mi__ExitLobby!);
	}

	static readonly Action<(App, bool)> call__ExitLobby;

	const string scene = "Empty";

	[HarmonyPatch(typeof(App), "EnterMenu")]
	[HarmonyPrefix]
	static void Redirect(App __instance, ref bool __runOriginal) 
	{
		__runOriginal = false;

		lock (App.stateLock) {
			switch (App.state) {
			case AppSate.Menu: return;
			case AppSate.ClientLobby or AppSate.ServerLobby:
				call__ExitLobby((__instance, false));
				goto case3;
			case AppSate.Customize or AppSate.PlayLevel 
			or AppSate.ClientLobby or AppSate.ServerLobby: case3:
				Game.instance.HasSceneLoaded = false;
				LevelBuffer.LoadLevelAdapter(scene, 
					() => SceneManager.LoadSceneAsync(scene));
				goto default;
			case AppSate.Startup:
				Game.instance.HasSceneLoaded = false;
				SceneManager.LoadScene(scene);
				goto default;
			default:
				Game.instance.state = GameState.Inactive;
				App.state = AppSate.Menu;
				return;
			}
		}
	}
}