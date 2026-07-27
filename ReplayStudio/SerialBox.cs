using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using static ReplayStudio.Components.KeyframedObject;

namespace ReplayStudio
{
    [JsonObject(MemberSerialization.OptIn)]
    public class StudioData
    {
        [JsonProperty]
        public Dictionary<Type, SortedList<Keyframe.Snap, Keyframe>> CameraKeyframes
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
    }
}