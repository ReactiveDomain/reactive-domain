using ReactiveDomain.Messaging;

namespace ReactiveDomain
{
	public interface IMessageSerializer
	{
		StorableMessage Serialize(Message msg);
	}
}