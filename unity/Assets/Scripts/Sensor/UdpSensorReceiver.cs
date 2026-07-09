using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public sealed class UdpSensorReceiver : MonoBehaviour
{
    [SerializeField] private int listenPort = 5005;
    [SerializeField] private float disconnectTimeoutSeconds = 1.0f;

    private readonly object packetLock = new object();
    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;
    private GravitySensorPacket latestPacket;
    private string latestRawJson = "";
    private string latestError = "";
    private long lastPacketUtcTicks;
    private uint packetCount;
    private bool hasSequence;
    private uint latestSequence;

    public int ListenPort => listenPort;
    public uint PacketCount { get { lock (packetLock) { return packetCount; } } }
    public string LatestRawJson { get { lock (packetLock) { return latestRawJson; } } }
    public string LatestError { get { lock (packetLock) { return latestError; } } }
    public float SecondsSinceLastPacket
    {
        get
        {
            long packetTicks = Interlocked.Read(ref lastPacketUtcTicks);
            if (packetTicks == 0) return float.PositiveInfinity;
            TimeSpan elapsed = DateTime.UtcNow - new DateTime(packetTicks, DateTimeKind.Utc);
            return (float)elapsed.TotalSeconds;
        }
    }
    public bool IsConnected => TryGetLatestPacket(out _) && SecondsSinceLastPacket <= disconnectTimeoutSeconds;

    public bool TryGetLatestPacket(out GravitySensorPacket packet)
    {
        lock (packetLock)
        {
            packet = latestPacket;
            return packet != null;
        }
    }

    private void OnEnable() => StartReceiver();
    private void OnDisable() => StopReceiver();
    private void OnApplicationQuit() => StopReceiver();

    private void StartReceiver()
    {
        if (running) return;
        running = true;
        try
        {
            udpClient = new UdpClient(listenPort);
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Gravity Cradle UDP Receiver"
            };
            receiveThread.Start();
        }
        catch (Exception ex)
        {
            running = false;
            SetError(ex.Message);
        }
    }

    private void StopReceiver()
    {
        running = false;
        try { udpClient?.Close(); } catch (ObjectDisposedException) { }
        udpClient = null;
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(250);
        receiveThread = null;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] bytes = udpClient.Receive(ref remoteEndPoint);
                string json = Encoding.UTF8.GetString(bytes);
                GravitySensorPacket packet = JsonUtility.FromJson<GravitySensorPacket>(json);
                if (!IsValid(packet))
                {
                    SetError("Invalid sensor packet");
                    continue;
                }

                lock (packetLock)
                {
                    if (hasSequence && !IsNewerSequence(packet.seq, latestSequence) && !HasTimedOut()) continue;
                    hasSequence = true;
                    latestSequence = packet.seq;
                    latestPacket = packet;
                    latestRawJson = json;
                    latestError = "";
                    packetCount++;
                    Interlocked.Exchange(ref lastPacketUtcTicks, DateTime.UtcNow.Ticks);
                }
            }
            catch (SocketException) { if (running) SetError("UDP socket error"); }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { SetError(ex.Message); }
        }
    }

    private static bool IsValid(GravitySensorPacket packet)
    {
        return packet != null
            && GravityFaceMapper.ParseFace(packet.face) != GravityFace.Unknown
            && IsFinite(packet.qw) && IsFinite(packet.qx)
            && IsFinite(packet.qy) && IsFinite(packet.qz);
    }

    private bool HasTimedOut()
    {
        long packetTicks = Interlocked.Read(ref lastPacketUtcTicks);
        if (packetTicks == 0) return true;
        return (DateTime.UtcNow - new DateTime(packetTicks, DateTimeKind.Utc)).TotalSeconds > disconnectTimeoutSeconds;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsNewerSequence(uint candidate, uint previous) => unchecked((int)(candidate - previous)) > 0;

    private void SetError(string message)
    {
        lock (packetLock) { latestError = message; }
    }
}
