namespace LevelBuffer;

public interface IBlockingOperation
{
	string SceneName { get; }
	void Apply(Action? callback = null);
}
