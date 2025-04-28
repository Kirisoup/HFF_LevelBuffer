using System.Diagnostics.CodeAnalysis;

namespace LevelBuffer;

static class PanicExtention
{
	extension (ArgumentException) {
		public static void ThrowIfNull([NotNull] object? arg, string name) {
			if (arg is null) throw new ArgumentNullException(name);
		}
	}
}