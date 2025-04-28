global using UnityEngine;
global using Object = UnityEngine.Object;
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
				LevelBuffer.Init(uint.TryParse(args[0], out uint index) 
					? Game.instance.levels[index]
					: args[0]);
			});
		}

	}

	void OnDestroy() {
		_harmony.UnpatchSelf();
		_pool.Dispose();
	}
}