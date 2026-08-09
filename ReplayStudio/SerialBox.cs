using Il2CppRUMBLE.Players;
using MelonLoader;
using Newtonsoft.Json;
using ReplayStudio.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using static ReplayStudio.Components.KeyframedObject;
using Channels = System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.SortedList<ReplayStudio.Components.KeyframedObject.Keyframe.Snap, ReplayStudio.Components.KeyframedObject.Keyframe>>;

namespace ReplayStudio
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StudioData
    {
        [JsonProperty]
        public Channels CameraKeyframes
        {
            get
            {
                return CameraController.KeyframeComponent.Channels;
            }
            set
            {
                CameraController.KeyframeComponent.Channels = value;
            }
        }

        [JsonIgnore]
        public static Dictionary<PlayerController, KeyframedObject> playerComponents = new();
        [JsonProperty]
        public Dictionary<int, Channels> PlayerKeyframes
        {
            get
            {
                Dictionary<int, Channels> newDict = new();
                for (int i = 0; i < ReplayMod.Core.Main.Playback.PlaybackPlayers.Length; i++)
                {
                    PlayerController player = ReplayMod.Core.Main.Playback.PlaybackPlayers[i].Controller;
                    KeyframedObject component = player.GetComponent<KeyframedObject>();
                    if (component != null && component.Channels.Count > 0)
                        newDict[i] = component.Channels;
                }
                return newDict;
            }
            set
            {
                playerComponents.Clear();
                foreach (var (id, channels) in value)
                {
                    PlayerController player = ReplayMod.Core.Main.Playback.PlaybackPlayers[id]?.Controller;
                    if (player == null) continue;

                    KeyframedObject component = player.GetComponent<KeyframedObject>() ?? player.gameObject.AddComponent<KeyframedObject>();
                    playerComponents[player] = component;

                    component.Channels = channels;
                }
            }
        }

        public class Vector3Converter : JsonConverter<Vector3>
        {
            public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, new float[] { value.x, value.y, value.z });
            }

            public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var v = serializer.Deserialize<float[]>(reader);
                return new Vector3(v[0], v[1], v[2]);
            }
        }
    }
}