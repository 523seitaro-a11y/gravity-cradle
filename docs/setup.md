# Sensor Prototype Setup

## Hardware

AE-BNO055-BOは出荷時設定のI2Cモード、アドレス`0x28`を使用します。

| AE-BNO055-BO | XIAO ESP32S3 |
| --- | --- |
| VIN | 3V3 |
| GND | GND |
| SDA | SDA |
| SCL | SCL |

`RESET`、`INT`、`VOUT`は今回使用しません。

参考: [秋月電子 AE-BNO055-BO説明書](https://akizukidenshi.com/goodsaffix/AE-BNO055-BO_20220413.pdf)

## Firmware

1. Arduino IDEへXIAO ESP32S3のボード定義を追加します。
2. Library Managerから`Adafruit BNO055`と表示された依存ライブラリを導入します。
3. `firmware/GravityCradleSensor/GravityCradleSensor.ino`を開きます。
4. `WIFI_SSID`、`WIFI_PASSWORD`、`UNITY_PC_IP`を環境に合わせます。
5. XIAO ESP32S3へ書き込みます。
6. シリアルモニタを`115200 baud`で開き、I2C scanに`0x28`が表示されることを確認します。

PCとXIAO ESP32S3は相互通信できる同じネットワークへ接続してください。ゲストWi-Fiなど端末間通信を遮断するネットワークではUDPが届きません。

## Unity

1. Unity Hubからリポジトリ内の`unity/`をUnity 6000.1.0f1で開きます。
2. `Assets/Scenes/SampleScene.unity`を開きます。
3. PCのファイアウォールでUnity EditorのUDP `5005`受信を許可します。
4. Playします。`SensorRuntimeBootstrap`が受信機、ステージ制御、デバッグ表示を自動作成します。
5. 左上の`Status`、`Packets`、`Current face`、クォータニオンを確認します。

センサー接続中はセンサー入力が優先されます。パケットが1秒以上届かない場合は最後の向きを維持し、`waiting`表示になってマウスクリック操作へ戻ります。

## Inspector Configuration

標準値で面が合わない場合は、Sceneへ空のGameObjectを作成して次を追加します。

- `UdpSensorReceiver`
- `SensorGravityController`
- `SensorDebugOverlay`

`SensorGravityController`の6つの`Stage-local surface normals`を変更すると、BNO055の取り付け方向に合わせられます。Sceneにこれらがある場合、自動Bootstrapは重複して作成しません。

UDPポートを変更する場合は、`UdpSensorReceiver.listenPort`とファームウェアの`UNITY_UDP_PORT`を同じ値にします。

## Test Without Hardware

UnityをPlayした状態で別のPowerShellから次を実行すると、右面のテストパケットを送信できます。`seq`を増やし、`face`を`down`、`up`、`left`、`right`、`front`、`back`へ変更して6面を確認します。

```powershell
$udp = [Net.Sockets.UdpClient]::new()
$json = '{"seq":1,"qw":1,"qx":0,"qy":0,"qz":0,"face":"right","ms":1}'
$bytes = [Text.Encoding]::UTF8.GetBytes($json)
[void]$udp.Send($bytes, $bytes.Length, '127.0.0.1', 5005)
$udp.Dispose()
```

古い`seq`、不正JSON、未知の`face`は無視されます。送信側を再起動して`seq`が0へ戻った場合は、通信タイムアウト後の最初のパケットを新しい系列として受け入れます。

## Tuning

- 送信周期: `SEND_INTERVAL_MS`（標準33ms）
- 面切替しきい値: `FACE_SWITCH_THRESHOLD`
- 初回判定しきい値: `FACE_RELEASE_THRESHOLD`
- 切断判定: `UdpSensorReceiver.disconnectTimeoutSeconds`
- ステージ回転時間: `StageSurfaceRotator.rotationDuration`

## Acceptance Check

- I2C scanで`0x28`が見える
- UDP受信時に`Packets`と`seq`が増える
- 6面それぞれで意図したステージ面が床になる
- 小さな揺れで面が頻繁に切り替わらない
- 通信停止後にマウス操作へ戻る
- 10分間動かして通信断や表示ちらつきが許容範囲である

## 今後決める

- ゲーム本編のルール、主人公、ステージ、ギミック
- 90度固定と連続重力のどちらを採用するか
- 重力固定ボタン、キャリブレーション操作
- UDP、BLE、WebSocketの最終選択
- 筐体、電池、電源スイッチ、充電方式
- Unityの`Physics.gravity`を直接変更するか
