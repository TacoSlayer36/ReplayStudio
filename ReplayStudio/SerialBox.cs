using System;
using System.Collections.Generic;
using static ReplayStudio.Components.KeyframedObject;

namespace ReplayStudio
{
    [Serializable]
    public class StudioData
    {
        Dictionary<Type, SortedList<float, Keyframe>> CameraKeyframes
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