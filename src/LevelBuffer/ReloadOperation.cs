using UnityEngine.SceneManagement;

namespace LevelBuffer;

public readonly struct ReloadOperation : IBlockingOperation
{
	public string SceneName { get; }
	private readonly AsyncOperation _opEmpty;
	private readonly AsyncOperation _opLevel;

	private ReloadOperation(string sceneName, AsyncOperation opEmpty, AsyncOperation opLevel) {
		SceneName = sceneName;
		_opEmpty = opEmpty;
		_opLevel = opLevel;
	}

	[Obsolete(null, true)]
	public ReloadOperation() => throw new NotSupportedException();

	public static ReloadOperation New(string sceneName) {
		var opEmpty = SceneManager.LoadSceneAsync("Empty", LoadSceneMode.Single);
		var opLevel = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
		opEmpty.allowSceneActivation = false;
		opLevel.allowSceneActivation = false;
		return new(sceneName, opEmpty, opLevel);
	}

	public void Apply(Action? callback = null) {
		_opEmpty.allowSceneActivation = true;
		_opLevel.allowSceneActivation = true;
		if (callback is null) return;
		var op = _opLevel;
		Plugin.Instance.Await(
			condition: () => op.isDone,
			onFinish: callback);
	}
}