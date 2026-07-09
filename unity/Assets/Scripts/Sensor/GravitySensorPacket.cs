using System;
using UnityEngine;

[Serializable]
public sealed class GravitySensorPacket
{
    public uint seq;
    public float qw;
    public float qx;
    public float qy;
    public float qz;
    public string face;
    public uint ms;

    public Quaternion ToUnityQuaternion()
    {
        return new Quaternion(qx, qy, qz, qw);
    }
}
