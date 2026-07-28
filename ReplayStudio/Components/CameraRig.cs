using MelonLoader;
using UnityEngine;
namespace ReplayStudio.Components;

[RegisterTypeInIl2Cpp]
public class CameraRig : MonoBehaviour
{
    public Vector3 Focus;
    public Quaternion Rotation;
    public float Distance = 5;
}