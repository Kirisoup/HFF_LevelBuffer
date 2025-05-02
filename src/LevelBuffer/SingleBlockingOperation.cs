using UnityEngine.SceneManagement;

namespace LevelBuffer;

public readonly struct SingleOperation : IBlockingOperation
{
	public string SceneName { get; }
	private readonly AsyncOperation _op;

	private SingleOperation(string sceneName, AsyncOperation op) {
		SceneName = sceneName;
		_op = op;
	}

	[Obsolete(null, true)]
	public SingleOperation() => throw new NotSupportedException();

	public static SingleOperation New(string sceneName) {
		var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
		op.allowSceneActivation = false;
		return new(sceneName, op);
	}

	public void Apply(Action? callback = null) {
		_op.allowSceneActivation = true;
		if (callback is null) return;
		var op = _op;
		Plugin.Instance.Await(
			condition: () => op.isDone,
			onFinish: callback);
	}
}
