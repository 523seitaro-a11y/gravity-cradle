using UnityEngine;

public sealed class SensorGravityController : MonoBehaviour
{
    [SerializeField] private UdpSensorReceiver receiver;
    [SerializeField] private StageSurfaceRotator stageRotator;
    [Header("Stage-local surface normals")]
    [SerializeField] private Vector3 downFaceNormal = Vector3.up;
    [SerializeField] private Vector3 upFaceNormal = Vector3.down;
    [SerializeField] private Vector3 leftFaceNormal = Vector3.left;
    [SerializeField] private Vector3 rightFaceNormal = Vector3.right;
    [SerializeField] private Vector3 frontFaceNormal = Vector3.forward;
    [SerializeField] private Vector3 backFaceNormal = Vector3.back;

    private GravityFace currentFace = GravityFace.Unknown;
    private GravityFace appliedFace = GravityFace.Unknown;

    public GravityFace CurrentFace => currentFace;
    public bool SensorHasControl => receiver != null && receiver.IsConnected;

    private void Reset()
    {
        receiver = FindObjectOfType<UdpSensorReceiver>();
        stageRotator = FindObjectOfType<StageSurfaceRotator>();
    }

    private void Awake()
    {
        if (receiver == null) receiver = FindObjectOfType<UdpSensorReceiver>();
        if (stageRotator == null) stageRotator = FindObjectOfType<StageSurfaceRotator>();
    }

    private void Update()
    {
        bool sensorConnected = SensorHasControl;
        if (stageRotator != null) stageRotator.SetExternalInputActive(sensorConnected);
        if (!sensorConnected)
        {
            appliedFace = GravityFace.Unknown;
            return;
        }

        if (stageRotator == null || stageRotator.IsRotating) return;
        if (!receiver.TryGetLatestPacket(out GravitySensorPacket packet)) return;

        currentFace = GravityFaceMapper.ParseFace(packet.face);
        if (currentFace == GravityFace.Unknown || currentFace == appliedFace) return;

        Vector3 localNormal = GetLocalNormal(currentFace);
        Transform root = stageRotator.StageRoot;
        Vector3 worldNormal = root != null ? root.TransformDirection(localNormal) : localNormal;
        if (stageRotator.TryRotateSurfaceToFloor(worldNormal)) appliedFace = currentFace;
    }

    private void OnDisable()
    {
        if (stageRotator != null) stageRotator.SetExternalInputActive(false);
    }

    private Vector3 GetLocalNormal(GravityFace face)
    {
        switch (face)
        {
            case GravityFace.Down: return downFaceNormal.normalized;
            case GravityFace.Up: return upFaceNormal.normalized;
            case GravityFace.Left: return leftFaceNormal.normalized;
            case GravityFace.Right: return rightFaceNormal.normalized;
            case GravityFace.Front: return frontFaceNormal.normalized;
            case GravityFace.Back: return backFaceNormal.normalized;
            default: return Vector3.up;
        }
    }
}
