using Newtonsoft.Json;
using ReactiveDomain.Messaging;
using System;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
{
    public class TestEvent : Event { }
    public class ParentTestEvent : Message { }
    public class ChildTestEvent : ParentTestEvent { }
    public class GrandChildTestEvent : ChildTestEvent { }


}

