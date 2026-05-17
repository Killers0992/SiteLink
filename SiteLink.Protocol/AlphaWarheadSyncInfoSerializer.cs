using Mirror;

public static class AlphaWarheadSyncInfoSerializer
{
	public static void WriteAlphaWarheadSyncInfo (this NetworkWriter writer, AlphaWarheadSyncInfo value)
	{
		writer.WriteDouble (value.StartTime);
		if (value.StartTime != 0.0) {
			writer.WriteByte (value.ScenarioId);
			writer.WriteByte ((byte)value.ScenarioType);
		}
	}

	public static AlphaWarheadSyncInfo ReadAlphaWarheadSyncInfo (this NetworkReader reader)
	{
		double num = reader.ReadDouble ();
		if (num == 0.0) {
			return new AlphaWarheadSyncInfo {
				StartTime = 0.0
			};
		}
		byte scenarioId = reader.ReadByte ();
		byte scenarioType = reader.ReadByte ();
		return new AlphaWarheadSyncInfo {
			ScenarioId = scenarioId,
			ScenarioType = (WarheadScenarioType)scenarioType,
			StartTime = num
		};
	}
}
