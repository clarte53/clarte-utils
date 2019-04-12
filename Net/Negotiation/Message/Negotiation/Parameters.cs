using System;
using CLARTE.Serialization;

namespace CLARTE.Net.Negotiation.Message.Negotiation
{
	public class Parameters : Base
	{
		#region Members
		public ushort channel;
		public Net.Negotiation.Channel.Type type;
		public TimeSpan heartbeat;
		public bool autoReconnect;
		#endregion

		#region IBinarySerializable implementation
		public override uint FromBytes(Binary serializer, Binary.Buffer buffer, uint start)
		{
			uint read = 0;

			byte channel_type;
			ushort heartbeat_ticks;

			read += serializer.FromBytes(buffer, start + read, out channel);
			read += serializer.FromBytes(buffer, start + read, out channel_type);
			read += serializer.FromBytes(buffer, start + read, out heartbeat_ticks);
			read += serializer.FromBytes(buffer, start + read, out autoReconnect);

			type = (Net.Negotiation.Channel.Type) channel_type;

			if(heartbeat_ticks == ushort.MaxValue)
			{
				heartbeat = new TimeSpan(0, 0, 0, 0, -1);
			}
			else
			{
				heartbeat = new TimeSpan(heartbeat_ticks * 100 * TimeSpan.TicksPerMillisecond);
			}

			return read;
		}

		public override uint ToBytes(Binary serializer, ref Binary.Buffer buffer, uint start)
		{
			uint written = 0;

			const uint message_size = sizeof(byte) + sizeof(ushort) + sizeof(bool);

			serializer.ResizeBuffer(ref buffer, start + message_size);

			long heartbeat_ticks = heartbeat.Ticks / (100 * TimeSpan.TicksPerMillisecond);

			if((heartbeat.Days == 0 && heartbeat.Hours == 0 && heartbeat.Minutes == 0 && heartbeat.Seconds == 0 && heartbeat.Milliseconds == -1) || heartbeat_ticks > ushort.MaxValue)
			{
				heartbeat_ticks = ushort.MaxValue;
			}

			written += serializer.ToBytes(ref buffer, start + written, channel);
			written += serializer.ToBytes(ref buffer, start + written, (byte) type);
			written += serializer.ToBytes(ref buffer, start + written, (ushort) heartbeat_ticks);
			written += serializer.ToBytes(ref buffer, start + written, autoReconnect);

			return written;
		}
		#endregion
	}
}
