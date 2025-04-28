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
		_harmony.PatchAll(typeof(Patch.Game_LoadLevel));
		_pool.Register(cmd: "buf", str => {
			if (str is null) {
				Shell.Print("missing argument");
				return;
			}
			var args = str
				.ToLowerInvariant()
				.Split([' '], StringSplitOptions.RemoveEmptyEntries);
			LevelBuffer.Init(uint.TryParse(args[0], out uint index) 
				? Game.instance.levels[index]
				: args[0]);
		});
	}

	void OnDestroy() {
		_harmony.UnpatchSelf();
		_pool.Dispose();
	}
}