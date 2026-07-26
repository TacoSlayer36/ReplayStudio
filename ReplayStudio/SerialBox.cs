using Newtonsoft.Json;
using ReplayStudio.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ReplayStudio
{
    [Serializable]
    public class StudioData
    {
        public KeyframedObject CameraKeyframeComponent = new();
    }
}