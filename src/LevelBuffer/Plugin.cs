global using UnityEngine;
global using Object = UnityEngine.Object;
global using Debug = System.Diagnostics.Debug;
global using uDebug = UnityEngine.Debug;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace LevelBuffer;

[BepInPlugin(GUID, NAME, PluginInfo.PLUGIN_VERSION)]
sealed class Plugin : BaseUnityPlugin
{
	public const string NAME = nameof(LevelBuffer), GUID = $"tld.kirisoup.hff.{NAME}";

	internal static Plugin Instance {
		get => field 
			?? throw new InvalidOperationException($"Plugin {GUID} is not instantiated");
		set;
	}
	
	internal static new ManualLogSource Logger {
		get => field 
			?? throw new InvalidOperationException($"Plugin {GUID} is not instantiated");
		set;
	}

	Plugin() => (Instance, Logger) = (this, base.Logger);

	readonly Harmony _harmony = new(GUID);
	readonly CommandPool _pool = new();

	ConfigEntry<bool>? _cfgRegCommand;

	void Awake() 
	{
		_harmony.PatchAll(typeof(Patch.Game_LoadLevel));
		_harmony.PatchAll(typeof(Patch.Multiplayer_App_EnterMenu));
		_harmony.PatchAll(typeof(Patch.Multiplayer_App_EnterLobbyAsync));
		_harmony.PatchAll(typeof(Patch.Multiplayer_App_EnterCustomization));
		_harmony.PatchAll(typeof(Patch.SwitchAssetBundle_LoadingCurrentScene_LoadScene));
		
		_cfgRegCommand = Config.Bind(
			section: "Command",
			key: "registerCommand",
			false,
			description: "Whether should the plugin register a level `buf` command. It is only intended for testing purpose so leave it false unless you know what you're doing. "
		);

		if (_cfgRegCommand.Value) {
			_pool.Register(cmd: "buf", str => {
				if (str is null) {
					Shell.Print("missing argument");
					return;
				}
				var args = str
					.ToLowerInvariant()
					.Split([' '], StringSplitOptions.RemoveEmptyEntries);
				string scene = uint.TryParse(args[0], out uint index) 
					? Game.instance.levels[index]
					: args[0];
				Shell.Print($"buffering scene {scene}");
				LevelBuffer.Init(scene);
			});
		}

	}

	void OnDestroy() {
		_harmony.UnpatchSelf();
		_pool.Dispose();
	}

	internal void Await(Func<bool> condition, Action onFinish) {
		StartCoroutine(Coroutine());
		IEnumerator Coroutine() {
			while (!condition()) yield return null; 
			onFinish();
		}
	}
}
