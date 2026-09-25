using Newtonsoft.Json;
using ValleyRail.Core;
namespace ValleyRail
{
    public sealed class JsonSnapshotCodec : ISnapshotCodec
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, ObjectCreationHandling = ObjectCreationHandling.Replace, MaxDepth = 48, Formatting = Formatting.None };
        public string Encode<T>(T value) => JsonConvert.SerializeObject(value, Settings);
        public T Decode<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
