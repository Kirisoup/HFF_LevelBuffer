global using UnityEngine;
global using Object = UnityEngine.Object;
using BepInEx;
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

	void Awake() 
	{
		Logger.LogInfo("initializing");
		_harmony.PatchAll(typeof(Mod.Game_LoadLevel));
		
		_pool.Register(cmd: "buf", str => {
			var args = str?
				.ToLowerInvariant()
				.Split([' '], StringSplitOptions.RemoveEmptyEntries);
			if (args is null) {
				Shell.Print("argument missing");
				return;
			}
			if (LevelBuffer.Current is not null) {
				Shell.Print($"buffer is occupied with scene {LevelBuffer.Current.SceneName}");
				return;
			}
			string sceneName = uint.TryParse(args[0], out uint index) 
				? Game.instance.levels[index]
				: args[0];
			// Logger.LogInfo($"");
			LevelBuffer.Init(sceneName);
		});
	}

	void OnDestroy() {
		Logger.LogInfo("removing");
		_harmony.UnpatchSelf();
	}

	sealed class MultiInstantiationException() : 
		InvalidOperationException($"Plugin {GUID} should not be instantiated multiple times");
}