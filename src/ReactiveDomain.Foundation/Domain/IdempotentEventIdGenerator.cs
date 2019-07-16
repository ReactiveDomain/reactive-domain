using System;
using System.Text;

namespace ReactiveDomain
{
    public class IdempotentEventIdGenerator
    {
        //DON'T CHANGE THIS!
        public static readonly Guid Namespace = new Guid("DB726A34-B4C6-4BBD-927E-FBC2E5781867");

        private readonly NameBasedGuidGenerator _generator;

        public IdempotentEventIdGenerator()
        {
            _generator = new NameBasedGuidGenerator(Namespace);
        }

        /// <summary>
        /// Generates an idempotent event identifier based on the command identifier, the expected version, the event name and its index in the set of produced events.
        /// </summary>
        /// <param name="command">The command identifier.</param>
        /// <param name="expectedVersion">The expected version.</param>
        /// <param name="eventName">The name associated with the event.</param>
        /// <param name="eventIndex">The index of the event in the set of produced events.</param>
        /// <returns>An event identifier.</returns>
        public Guid Generate(Guid command, long expectedVersion, string eventName, int eventIndex)
        {
            var eventIndexBuffer = BitConverter.GetBytes(eventIndex);
            var eventNameBuffer = Encoding.UTF8.GetBytes(eventName);
            var expectedVersionBuffer = BitConverter.GetBytes(expectedVersion);
            var operationIdBuffer = command.ToByteArray();
            var buffer = new byte[eventNameBuffer.Length + eventIndexBuffer.Length + expectedVersionBuffer.Length + operationIdBuffer.Length];
            Array.Copy(eventIndexBuffer, 0, buffer, 0, eventIndexBuffer.Length);
            Array.Copy(eventNameBuffer, 0, buffer, eventIndexBuffer.Length, eventNameBuffer.Length);
            Array.Copy(expectedVersionBuffer, 0, buffer, eventIndexBuffer.Length + eventNameBuffer.Length, expectedVersionBuffer.Length);
            Array.Copy(operationIdBuffer, 0, buffer, eventIndexBuffer.Length + eventNameBuffer.Length + expectedVersionBuffer.Length, operationIdBuffer.Length);
            return _generator.Create(buffer);
        }
    }
}