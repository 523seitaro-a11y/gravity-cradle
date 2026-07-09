using UnityEngine;

public sealed class SensorDebugOverlay : MonoBehaviour
{
    [SerializeField] private UdpSensorReceiver receiver;
    [SerializeField] private SensorGravityController sensorController;
    [SerializeField] private int fontSize = 18;
    private GUIStyle style;

    private void Reset()
    {
        receiver = FindObjectOfType<UdpSensorReceiver>();
        sensorController = FindObjectOfType<SensorGravityController>();
    }

    private void Awake()
    {
        if (receiver == null) receiver = FindObjectOfType<UdpSensorReceiver>();
        if (sensorController == null) sensorController = FindObjectOfType<SensorGravityController>();
    }

    private void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = Color.white }
            };
        }

        GUILayout.BeginArea(new Rect(16, 16, 620, 260), GUI.skin.box);
        GUILayout.Label("Gravity Cradle Sensor Prototype", style);
        if (receiver == null)
        {
            GUILayout.Label("Receiver: missing", style);
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"UDP port: {receiver.ListenPort}", style);
        GUILayout.Label($"Status: {(receiver.IsConnected ? "connected" : "waiting")}", style);
        GUILayout.Label($"Packets: {receiver.PacketCount}", style);
        GUILayout.Label($"Last packet: {receiver.SecondsSinceLastPacket:0.000}s ago", style);
        if (sensorController != null)
        {
            GUILayout.Label($"Current face: {sensorController.CurrentFace}", style);
            GUILayout.Label($"Input: {(sensorController.SensorHasControl ? "sensor" : "mouse fallback")}", style);
        }

        if (receiver.TryGetLatestPacket(out GravitySensorPacket packet))
        {
            GUILayout.Label($"seq={packet.seq} face={packet.face} ms={packet.ms}", style);
            GUILayout.Label($"q=({packet.qw:0.000}, {packet.qx:0.000}, {packet.qy:0.000}, {packet.qz:0.000})", style);
        }

        if (!string.IsNullOrEmpty(receiver.LatestError)) GUILayout.Label($"Error: {receiver.LatestError}", style);
        GUILayout.EndArea();
    }
}
