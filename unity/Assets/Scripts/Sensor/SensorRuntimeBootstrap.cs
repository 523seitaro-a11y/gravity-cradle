using UnityEngine;

public static class SensorRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSensorRuntime()
    {
        UdpSensorReceiver receiver = Object.FindObjectOfType<UdpSensorReceiver>();
        GameObject runtime = receiver != null
            ? receiver.gameObject
            : new GameObject("Gravity Sensor Runtime");

        if (receiver == null) runtime.AddComponent<UdpSensorReceiver>();
        if (Object.FindObjectOfType<SensorGravityController>() == null) runtime.AddComponent<SensorGravityController>();
        if (Object.FindObjectOfType<SensorDebugOverlay>() == null) runtime.AddComponent<SensorDebugOverlay>();
    }
}
