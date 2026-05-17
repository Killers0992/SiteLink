using System;

[Serializable]
public struct AlphaWarheadSyncInfo : IEquatable<AlphaWarheadSyncInfo>
{
	public byte ScenarioId;

	public WarheadScenarioType ScenarioType;

	public double StartTime;

	public bool InProgress => StartTime != 0.0;

	public override int GetHashCode ()
	{
		return (int)(StartTime * 10.0) % 32767 * 255 + ScenarioId + (int)ScenarioType;
	}

	public static bool operator == (AlphaWarheadSyncInfo left, AlphaWarheadSyncInfo right)
	{
		return left.Equals (right);
	}

	public static bool operator != (AlphaWarheadSyncInfo left, AlphaWarheadSyncInfo right)
	{
		return !left.Equals (right);
	}

	public bool Equals (AlphaWarheadSyncInfo other)
	{
		if (other.ScenarioId == ScenarioId && other.StartTime == StartTime) {
			return other.ScenarioType == ScenarioType;
		}
		return false;
	}

	public override bool Equals (object obj)
	{
		if (obj is AlphaWarheadSyncInfo other) {
			return Equals (other);
		}
		return false;
	}
}
