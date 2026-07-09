#include <Adafruit_BNO055.h>
#include <Adafruit_Sensor.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <Wire.h>
#include <math.h>
#include <utility/imumaths.h>

// Fill these in before uploading.
const char* WIFI_SSID = "YOUR_WIFI_SSID";
const char* WIFI_PASSWORD = "YOUR_WIFI_PASSWORD";
const char* UNITY_PC_IP = "192.168.0.10";
const uint16_t UNITY_UDP_PORT = 5005;

// AE-BNO055-BO ships as I2C address 0x28.
constexpr uint8_t BNO055_ADDRESS = 0x28;
constexpr uint16_t SEND_INTERVAL_MS = 33;
constexpr float FACE_SWITCH_THRESHOLD = 0.72f;
constexpr float FACE_RELEASE_THRESHOLD = 0.55f;

Adafruit_BNO055 bno = Adafruit_BNO055(55, BNO055_ADDRESS, &Wire);
WiFiUDP udp;

uint32_t sequenceNumber = 0;
uint32_t lastSendMs = 0;
String currentFace = "unknown";

void setup() {
  Serial.begin(115200);
  delay(250);

  Wire.begin();
  Wire.setClock(400000);

  Serial.println();
  Serial.println("Gravity Cradle Sensor Prototype");
  scanI2c();

  if (!bno.begin()) {
    Serial.println("BNO055 not detected at 0x28. Check VIN/GND/SDA/SCL.");
    while (true) {
      delay(1000);
    }
  }

  bno.setExtCrystalUse(true);
  connectWiFi();
}

void loop() {
  if (WiFi.status() != WL_CONNECTED) {
    connectWiFi();
  }

  const uint32_t now = millis();
  if (now - lastSendMs < SEND_INTERVAL_MS) {
    return;
  }
  lastSendMs = now;

  imu::Quaternion q = bno.getQuat();
  currentFace = detectFaceWithHysteresis(q, currentFace);
  sendPacket(q, currentFace, now);
}

void connectWiFi() {
  Serial.print("Connecting Wi-Fi: ");
  Serial.println(WIFI_SSID);

  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println();
  Serial.print("ESP32 IP: ");
  Serial.println(WiFi.localIP());
  Serial.print("Sending UDP to ");
  Serial.print(UNITY_PC_IP);
  Serial.print(":");
  Serial.println(UNITY_UDP_PORT);
}

void scanI2c() {
  Serial.println("I2C scan:");
  bool foundAny = false;

  for (uint8_t address = 1; address < 127; address++) {
    Wire.beginTransmission(address);
    if (Wire.endTransmission() == 0) {
      Serial.print("  0x");
      if (address < 16) {
        Serial.print("0");
      }
      Serial.println(address, HEX);
      foundAny = true;
    }
  }

  if (!foundAny) {
    Serial.println("  No I2C devices found.");
  }
}

String detectFaceWithHysteresis(const imu::Quaternion& q, const String& previousFace) {
  // Approximate local gravity direction by rotating world-down into sensor space.
  const float qw = q.w();
  const float qx = q.x();
  const float qy = q.y();
  const float qz = q.z();

  const float gx = 2.0f * (qx * qz + qw * qy);
  const float gy = 2.0f * (qy * qz - qw * qx);
  const float gz = qw * qw - qx * qx - qy * qy + qz * qz;

  const float absGx = fabsf(gx);
  const float absGy = fabsf(gy);
  const float absGz = fabsf(gz);

  String candidate = "unknown";
  float magnitude = 0.0f;

  if (absGx >= absGy && absGx >= absGz) {
    candidate = gx > 0.0f ? "right" : "left";
    magnitude = absGx;
  } else if (absGy >= absGx && absGy >= absGz) {
    candidate = gy > 0.0f ? "up" : "down";
    magnitude = absGy;
  } else {
    candidate = gz > 0.0f ? "back" : "front";
    magnitude = absGz;
  }

  if (previousFace != "unknown" && candidate != previousFace && magnitude < FACE_SWITCH_THRESHOLD) {
    return previousFace;
  }

  if (previousFace == "unknown" && magnitude < FACE_RELEASE_THRESHOLD) {
    return "unknown";
  }

  return candidate;
}

void sendPacket(const imu::Quaternion& q, const String& face, uint32_t nowMs) {
  char json[192];
  snprintf(
    json,
    sizeof(json),
    "{\"seq\":%lu,\"qw\":%.6f,\"qx\":%.6f,\"qy\":%.6f,\"qz\":%.6f,\"face\":\"%s\",\"ms\":%lu}",
    static_cast<unsigned long>(sequenceNumber++),
    q.w(),
    q.x(),
    q.y(),
    q.z(),
    face.c_str(),
    static_cast<unsigned long>(nowMs)
  );

  udp.beginPacket(UNITY_PC_IP, UNITY_UDP_PORT);
  udp.write(reinterpret_cast<const uint8_t*>(json), strlen(json));
  udp.endPacket();

  Serial.println(json);
}
