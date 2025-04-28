using HarmonyLib;

namespace LevelBuffer;

sealed class CommandPool : IDisposable
{
	readonly List<string> _pool = [];

	public void Register(string cmd, Action f, string? comment = null) {
		_pool.Add(cmd);
		Shell.RegisterCommand(cmd, f, comment);
	}

	public void Register(string cmd, Action<string?> f, string? comment = null) {
		_pool.Add(cmd);
		Shell.RegisterCommand(cmd, f, comment);
	}

	public void Dispose() {
		var shreg = AccessTools.Field(typeof(Shell), "commands")?.GetValue(null)
			?? throw new InvalidOperationException($"failed to access Shell.commands");
		var cmds = AccessTools.Field(typeof(CommandRegistry), "commands")?.GetValue(shreg)
			as Dictionary<string, Action>
			?? throw new InvalidOperationException($"failed to access CommandRegistry.commands");
		var cmdsstr = AccessTools.Field(typeof(CommandRegistry), "commandsStr")?.GetValue(shreg)
			as Dictionary<string, Action<string>>
			?? throw new InvalidOperationException($"failed to access CommandRegistry.commandsStr");
		foreach (var cmd in _pool) {
			cmds?.Remove(cmd);
			cmdsstr?.Remove(cmd);
		}
		_pool.Clear();
	} 
}
