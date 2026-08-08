using System;

namespace Builder.Data;

public static class ArrayExtensions
{
	public static void ForEach(this Array array, Action<Array, int[]> action)
	{
		if (array.LongLength != 0L)
		{
			ArrayTraverse arrayTraverse = new ArrayTraverse(array);
			do
			{
				action(array, arrayTraverse.Position);
			}
			while (arrayTraverse.Step());
		}
	}
}
