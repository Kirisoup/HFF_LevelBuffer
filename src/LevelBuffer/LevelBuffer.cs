using System.Collections;
using UnityEngine.SceneManagement;

namespace LevelBuffer;

public sealed class LevelBuffer
{
	public static LevelBuffer? Current { 
		get => (field?._op.isDone is true) ? (field = null) : field;
		private set;
	}

	private LevelBuffer(string sceneName) 
	{
		SceneName = sceneName;
		_op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
		_op.allowSceneActivation = false;

		Plugin.Instance.StartCoroutine(Inspect());

		IEnumerator Inspect() {
			Plugin.Logger.LogInfo($"buffering scene {sceneName}");
			while (_op.progress < (0.9f - float.Epsilon)) {
				yield return new WaitForSeconds(0.2f);
				Plugin.Logger.LogInfo(_op.progress);
			}
			Plugin.Logger.LogInfo($"buffer finished for scene {sceneName}");
		}
	}

	private readonly AsyncOperation _op;

	public string SceneName { get; }

	public static LevelBuffer? Init(string sceneName) => 
		(Current is null) ? (Current = new(sceneName)) : null;

	public void Apply(Action? callback = null) {
		if (_op.isDone) return;
		_op.allowSceneActivation = true;
		if (callback is not null) Plugin.Instance.Await(
			condition: () => _op.isDone,
			onFinish: () => callback()
		);
	}

	internal static void LoadLevelAdapter(
		string sceneName,
		Action fallback,
		Action? prepare = null,
		Action? onFinish = null
	) {
		var buf = Current;
		if (buf is null) {
			fallback();
		} else if (buf.SceneName != sceneName) {
			fallback();
			buf.Apply();
		} else {
			prepare?.Invoke();
			buf.Apply(onFinish);
		}
	}
}