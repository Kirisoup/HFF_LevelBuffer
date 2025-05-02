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

	internal static bool AllowReload { get; private set; }

	void Awake() 
	{
		var allowReload = Config.Bind("Tweaks", "allowReload", true, 
			"Allow re-loading of the same level");
		
		AllowReload = allowReload.Value;

		_harmony.PatchAll(typeof(Patch.Game_LoadLevel));
		_harmony.PatchAll(typeof(Patch.Multiplayer_App_EnterMenu));
		_harmony.PatchAll(typeof(Patch.Multiplayer_App_EnterLobbyAsync));
		_harmony.PatchAll(typeof(Patch.Multiplayer_App_EnterCustomization));
		_harmony.PatchAll(typeof(Patch.SwitchAssetBundle_LoadingCurrentScene_LoadScene));

		var regCommand = Config.Bind("Util", "registerCommand", false,
			"Registers extra debugging commands to the console");

		if (regCommand.Value) {
			_pool.Register("sbuf", str => {
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
				bool ok = BufferManager.TryStartNew(() => SingleOperation.New(scene));
				Shell.Print(ok 
					? $"buffering scene {scene}" 
					: "can not start buffer because thread is occupied");
			});
			_pool.Register("rbuf", str => {
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
				bool ok = BufferManager.TryStartNew(() => ReloadOperation.New(scene));
				Shell.Print(ok 
					? $"buffering scene {scene}" 
					: "can not start buffer because thread is occupied");
			});
			_pool.Register("rl", () => Multiplayer.App.instance.StartNextLevel(
				(ulong)Game.instance.currentLevelNumber, 0));
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
