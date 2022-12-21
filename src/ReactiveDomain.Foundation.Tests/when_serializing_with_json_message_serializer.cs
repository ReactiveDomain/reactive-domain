using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Audit;
using ReactiveDomain.Foundation.StreamStore;
using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Foundation.Tests
{
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
    public class TestObject
    {
        public string Data1;
        public string Data2 { get; set; }
        public override bool Equals(object obj)
        {
            return Equals(obj as TestObject);
        }
        public bool Equals(TestObject other)
        {
            if (other == null) return false;
            return string.CompareOrdinal(Data1, other.Data1) == 0 &&
                   string.CompareOrdinal(Data2, other.Data2) == 0;
        }
    }
    public class TestObject2
    {
        public string Data2 { get; set; }
        public string Data3;
        public override bool Equals(object obj)
        {
            return Equals(obj as TestObject2);
        }
        public bool Equals(TestObject2 other)
        {
            if (other == null) return false;
            return string.CompareOrdinal(Data3, other.Data3) == 0 &&
                   string.CompareOrdinal(Data2, other.Data2) == 0;
        }
    }
    public class MetadataTestObject : Event
    {
        public string Data { get; set; }
    }
    public class CustomMetadata { 
        public string Metadatum { get; set; }
    }
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()

    public class when_serializing_with_json_message_serializer
    {
        TestObject testObject = new TestObject { Data1 = "blaa", Data2 = "more blaa" };
        [Fact]
        public void can_serialize_objects()
        {

            var serializer = new JsonMessageSerializer();
            var eventData = serializer.Serialize(testObject);
            var deserialized = serializer.Deserialize(eventData);
            Assert.IsType<TestObject>(deserialized);
            var testObject2 = (TestObject)deserialized;
            Assert.True(testObject.Equals(testObject2));

            serializer.FullyQualify = true;
            eventData = serializer.Serialize(testObject);
            deserialized = serializer.Deserialize(eventData);
            Assert.IsType<TestObject>(deserialized);
            var testObject3 = (TestObject)deserialized;
            Assert.True(testObject.Equals(testObject3));

        }

        [Fact]
        public void can_write_qualified_name_header()
        {
            var serializer = new JsonMessageSerializer();

            var eventData = serializer.Serialize(testObject);
            var clrQualifiedName = (string)JObject.Parse(Encoding.UTF8.GetString(eventData.Metadata))
                .Property(serializer.EventClrQualifiedTypeHeader).Value;
            var partialName = $"{testObject.GetType().FullName},{testObject.GetType().Assembly.GetName()}";
            Assert.True(string.CompareOrdinal(clrQualifiedName, partialName) == 0);

            serializer.FullyQualify = true;
            eventData = serializer.Serialize(testObject);
            clrQualifiedName = (string)JObject.Parse(Encoding.UTF8.GetString(eventData.Metadata))
               .Property(serializer.EventClrQualifiedTypeHeader).Value;
            Assert.True(string.CompareOrdinal(clrQualifiedName, testObject.GetType().AssemblyQualifiedName) == 0);
        }

        [Fact]
        public void can_write_typename_header()
        {
            var serializer = new JsonMessageSerializer();

            var eventData = serializer.Serialize(testObject);
            var typeName = (string)JObject.Parse(Encoding.UTF8.GetString(eventData.Metadata))
                .Property(serializer.EventClrTypeHeader).Value;
            Assert.True(string.CompareOrdinal(typeName, testObject.GetType().Name) == 0);
        }
        [Fact]
        public void can_write_custom_header()
        {
            var serializer = new JsonMessageSerializer();
            var headerName = "MyHeader";
            var HeaderData = "my header data";
            var headers = new Dictionary<string, object> { { headerName, HeaderData } };
            var eventData = serializer.Serialize(testObject, headers);
            var customHeaderData = (string)JObject.Parse(Encoding.UTF8.GetString(eventData.Metadata))
                .Property(headerName).Value;
            Assert.True(string.CompareOrdinal(HeaderData, customHeaderData) == 0);
        }

        [Fact]
        public void can_overwrite_standard_headers()
        {
            var serializer = new JsonMessageSerializer();
            var headerName = serializer.EventClrTypeHeader;
            var headerData = "my clr type header";
            var headers = new Dictionary<string, object> { { headerName, headerData } };
            var eventData = serializer.Serialize(testObject, headers);
            var headerValue = (string)JObject.Parse(Encoding.UTF8.GetString(eventData.Metadata))
                .Property(headerName).Value;
            Assert.True(string.CompareOrdinal(headerData, headerValue) == 0);

            headerName = serializer.EventClrQualifiedTypeHeader;
            headerData = typeof(TestObject2).AssemblyQualifiedName;
            headers = new Dictionary<string, object> { { headerName, headerData } };
            eventData = serializer.Serialize(testObject, headers);
            headerValue = (string)JObject.Parse(Encoding.UTF8.GetString(eventData.Metadata))
                .Property(headerName).Value;
            Assert.True(string.CompareOrdinal(headerData, headerValue) == 0);
        }
        [Fact]
        public void serializer_will_use_overriden_headers_on_deserialize()
        {
            var serializer = new JsonMessageSerializer();

            //n.b. setting header to different type than testObject
            var headerName = serializer.EventClrQualifiedTypeHeader;
            var headerData = $"{typeof(TestObject2).FullName},{typeof(TestObject2).Assembly.GetName()}";
            var headers = new Dictionary<string, object> { { headerName, headerData } };
            var eventData = serializer.Serialize(testObject, headers);
            var deserialized = serializer.Deserialize(eventData);
            Assert.IsType<TestObject2>(deserialized);
            var testObject2 = (TestObject2)deserialized;
            Assert.True(string.CompareOrdinal(testObject.Data2, testObject2.Data2) == 0);
        }

        [Fact]
        public void can_deserialize_to_a_specified_type()
        {
            var serializer = new JsonMessageSerializer();
            var eventData = serializer.Serialize(testObject);
            //n.b. testObject is type TestObject
            var deserialized = serializer.Deserialize(eventData, typeof(TestObject2));
            Assert.IsType<TestObject2>(deserialized);
            var testObject2 = (TestObject2)deserialized;
            Assert.True(string.CompareOrdinal(testObject.Data2, testObject2.Data2) == 0);

            var newTestObject2 = serializer.Deserialize<TestObject2>(eventData);
            Assert.True(string.CompareOrdinal(testObject.Data2, newTestObject2.Data2) == 0);
        }
        [Fact]
        public void can_deserialize_with_metadata()
        {
            var serializer = new JsonMessageSerializer();
            var sourceOject = new MetadataTestObject { Data = "foo" };
            var policyUserId = Guid.NewGuid();
            var headers = new Dictionary<string, object> { { StreamStoreRepository.PolicyUserIdHeader, policyUserId } };
            var customMetadata = new CustomMetadata { Metadatum = "bar" };
            sourceOject.WriteMetadatum<CustomMetadata>(customMetadata);
            var eventData = serializer.Serialize(sourceOject, headers);

            var deserialized = serializer.Deserialize(eventData, typeof(MetadataTestObject));
            Assert.IsType<MetadataTestObject>(deserialized);
            var newOject = (MetadataTestObject)deserialized;
            Assert.True(string.CompareOrdinal(sourceOject.Data, newOject.Data) == 0);
            var commonMd = newOject.ReadMetadatum<CommonMetadata>();
            Assert.Equal(typeof(MetadataTestObject).FullName, commonMd.EventName);
            var ar = newOject.ReadMetadatum<AuditRecord>();
            Assert.Equal(policyUserId, ar.PolicyUserId);
            var customMD = newOject.ReadMetadatum<CustomMetadata>();
            Assert.Equal(customMetadata.Metadatum, customMD.Metadatum);

            var newOject2 = serializer.Deserialize<MetadataTestObject>(eventData);
            Assert.True(string.CompareOrdinal(sourceOject.Data, newOject2.Data) == 0);
            commonMd = newOject.ReadMetadatum<CommonMetadata>();
            Assert.Equal(typeof(MetadataTestObject).FullName, commonMd.EventName);
            ar = newOject2.ReadMetadatum<AuditRecord>();
            Assert.Equal(policyUserId, ar.PolicyUserId);
            customMD = newOject2.ReadMetadatum<CustomMetadata>();
            Assert.Equal(customMetadata.Metadatum, customMD.Metadatum);
        }

        [Fact]
        public void can_throw_if_type_not_found()
        {
            var serializer = new JsonMessageSerializer();

            //n.b. setting header to non existent assembly
            var headerName = serializer.EventClrQualifiedTypeHeader;
            var headerData = $"{typeof(TestObject2).FullName},dne-assembly";
            var headers = new Dictionary<string, object> { { headerName, headerData } };
            var eventData = serializer.Serialize(testObject, headers);
            //confirm type not found
            var deserialized = serializer.Deserialize(eventData);
            Assert.IsType<JObject>(deserialized);
            //request throw on type not found
            serializer.ThrowOnTypeNotFound = true;
            Assert.Throws<InvalidOperationException>(() => serializer.Deserialize(eventData));
        }

        [Fact]
        public void can_override_target_assembly()
        {
            var serializer = new JsonMessageSerializer();

            //n.b. setting header to non existent assembly
            var headerName = serializer.EventClrQualifiedTypeHeader;
            var headerData = $"{typeof(TestObject2).FullName},dne-assembly";
            var headers = new Dictionary<string, object> { { headerName, headerData } };
            var eventData = serializer.Serialize(testObject, headers);
            //confirm type not found
            var deserialized = serializer.Deserialize(eventData);
            Assert.IsType<JObject>(deserialized);
            //override assembly
            serializer.AssemblyOverride = Assembly.GetExecutingAssembly();
            deserialized = serializer.Deserialize(eventData);
            var testObject2 = (TestObject2)deserialized;
            Assert.True(string.CompareOrdinal(testObject.Data2, testObject2.Data2) == 0);
        }

        [Fact]
        public void can_deserialize_from_either_fully_or_partially_qualified_names()
        {
            //n.b. the previous version of the JsonMessageSerializer was fully qualified, and
            //this test demonstrates cross compability with that version
            var obj1 = new TestObject { Data1 = Guid.NewGuid().ToString("N") };
            var obj2 = new TestObject { Data1 = Guid.NewGuid().ToString("N") };
            var fullyQualifiedSerializer = new JsonMessageSerializer { FullyQualify = true };
            var partiallyQualifiedSerializer = new JsonMessageSerializer();

            //serialize with and without fully qualified names
            var eventData1 = fullyQualifiedSerializer.Serialize(obj1);
            var eventData2 = partiallyQualifiedSerializer.Serialize(obj2);
            //switch serializers
            var deserialized1 = partiallyQualifiedSerializer.Deserialize(eventData1);
            var deserialized2 = partiallyQualifiedSerializer.Deserialize(eventData2);

            Assert.IsType<TestObject>(deserialized1);
            Assert.IsType<TestObject>(deserialized2);

            var result1 = (TestObject)deserialized1;
            var result2 = (TestObject)deserialized2;

            Assert.True(string.CompareOrdinal(result1.Data1, obj1.Data1) == 0);
            Assert.True(string.CompareOrdinal(result2.Data1, obj2.Data1) == 0);

        }

    }
}
