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

	public void Apply(Action<LevelBuffer>? callback = null) {
		if (_op.isDone) return;
		_op.allowSceneActivation = true;
		if (callback is not null) Plugin.Instance.StartCoroutine(Await(
			() => _op.isDone,
			() => callback(this)
		));
	}

	public static void LoadLevel(string sceneName, Action? callback = null) {
		if (Current is null) {
			LoadSceneOriginal(sceneName, callback);
			return;
		}
		var instance = Current;
		instance._op.allowSceneActivation = true;
		
		if (Current.SceneName != sceneName) {
			Plugin.Logger.LogInfo($"cleaning up wrong scene buffer {instance.SceneName}");
			Plugin.Instance.StartCoroutine(Await(
				() => instance._op.isDone,
				() => LoadSceneOriginal(sceneName, callback)
			));
			return;
		}

		Plugin.Logger.LogInfo($"loading scene {sceneName} from buffer");
		if (callback is not null) Plugin.Instance.StartCoroutine(Await(
			() => instance._op.isDone,
			callback
		));
		return;
	}

	static void LoadSceneOriginal(string sceneName, Action? callback) {
		var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
		if (callback is not null) Plugin.Instance.StartCoroutine(Await(
			() => op.isDone && Game.instance.HasSceneLoaded,
			callback));
	}

	static IEnumerator Await(Func<bool> condition, Action callback) {
		while (!condition()) yield return null; 
		callback();
	}
}