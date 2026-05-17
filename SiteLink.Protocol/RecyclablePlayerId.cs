using System;
using System.Collections.Generic;

public struct RecyclablePlayerId : IEquatable<RecyclablePlayerId>
{
	private const int MinQueue = 16;

	private static readonly Queue<int> FreeIds = new Queue<int> ();

	private static int _autoIncrement;

	public readonly int Value;

	public RecyclablePlayerId (int newId)
	{
		Value = newId;
	}

	public RecyclablePlayerId (bool useMinQueue)
	{
		int num = (useMinQueue ? 16 : 0);
		int value = ((FreeIds.Count >= num) ? FreeIds.Dequeue () : (++_autoIncrement));
		Value = value;
	}

	public void Destroy ()
	{
		if (Value != 0) {
			FreeIds.Enqueue (Value);
		}
	}

	public bool Equals (RecyclablePlayerId other)
	{
		return Value == other.Value;
	}

	public override bool Equals (object obj)
	{
		if (obj is RecyclablePlayerId other) {
			return Equals (other);
		}
		return false;
	}

	public override int GetHashCode ()
	{
		return Value;
	}

	public static bool operator == (RecyclablePlayerId left, RecyclablePlayerId right)
	{
		return left.Equals (right);
	}

	public static bool operator != (RecyclablePlayerId left, RecyclablePlayerId right)
	{
		return !left.Equals (right);
	}
}
