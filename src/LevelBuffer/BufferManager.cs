namespace LevelBuffer;

public static class BufferManager
{
	private static IBlockingOperation? _current;

	public static bool TryStartNew(Func<IBlockingOperation> factory) {
		if (_current is not null) return false;
		_current = factory();
		return true;
	}

	internal static bool Load(
		string sceneName,
		Action? onFinish = null
	) {
		if (_current is null) return false;
		if (_current.SceneName != sceneName) {
			_current?.Apply(() => _current = null);
			return false;
		}
		_current.Apply(() => {
			_current = null;
			onFinish?.Invoke();
		});
		return true;
	}
}
