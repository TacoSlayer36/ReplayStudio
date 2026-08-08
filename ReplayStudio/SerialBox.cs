using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using static ReplayStudio.Components.KeyframedObject;
using Channel = System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.SortedList<ReplayStudio.Components.KeyframedObject.Keyframe.Snap, ReplayStudio.Components.KeyframedObject.Keyframe>>;

namespace ReplayStudio
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StudioData
    {
        [JsonProperty]
        public Channel CameraKeyframes
        {
            get
            {
                return CameraController.KeyframeComponent.Channels;
            }
            set
            {
                CameraController.KeyframeComponent.Channels = value;
            }
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, new float[]{ value.x, value.y, value.z });
        }
        
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var v = serializer.Deserialize<float[]>(reader);
            return new Vector3(v[0], v[1], v[2]);
        }
    }
}