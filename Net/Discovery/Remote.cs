using System.Net;

namespace CLARTE.Net.Discovery
{
	public class Remote
	{
		#region Members
		protected string identifier;
		protected IPEndPoint endPoint;
		#endregion

		#region Constructors
		public Remote(string identifier, IPAddress ip, ushort port)
		{
			this.identifier = identifier;

			endPoint = new IPEndPoint(ip, port);
		}
		#endregion

		#region Getters / Setters
		public IPAddress IPAddress
		{
			get
			{
				return new IPAddress(endPoint.Address.GetAddressBytes(), endPoint.Address.ScopeId);
			}
		}

		public ushort Port
		{
			get
			{
				return (ushort) endPoint.Port;
			}
		}

		public string Type
		{
			get
			{
				return identifier;
			}
		}
		#endregion

		#region Overloads for use as HashSet keys
		public override bool Equals(object comparand)
		{
			if(!(comparand is Remote))
			{
				return false;
			}
			if(((Remote) comparand).endPoint.Equals(endPoint))
			{
				return ((Remote) comparand).identifier.Equals(identifier);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return endPoint.GetHashCode() ^ identifier.GetHashCode();
		}
		#endregion
	}
}
