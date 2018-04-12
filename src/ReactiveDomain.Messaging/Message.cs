
// Based on InMemoryBus from EventStore LLP
// Added support for updating registered types and handlers from dynamically loaded assemblies
// Registered an event handler for the AssemblyLoad event.
// New assemblies are restricted to sub folders of the working directory to avoid loading system assemblies and throwing errors on reflection
// Added cross look-ups for type by typeId 
// Added Types Updated event 
// See also changes in InMemoryBus.cs 
// Key test cases include when a assembly containing types derived from Message is loaded after the InMemoryBus is created and  top level handler (i.e. for type Message) was previously added
// A simple example is a test fixture that sets up the bus and a top level listener in a constructor prior to executing a test cases based on types in a related project (see domain and domain.tests)
// Chris Condron 3-4-2014


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using ReactiveDomain.Logging;


// ReSharper disable  MemberCanBePrivate.Global
// ReSharper disable  PossibleNullReferenceException
// ReSharper disable  AssignNullToNotNullAttribute
namespace ReactiveDomain.Messaging
{
    public abstract class Message
    {
        [JsonProperty(Required = Required.Always)]
        public Guid MsgId { get; private set; }

        protected Message()
        {
            MsgId = Guid.NewGuid();
        }
    }
}
