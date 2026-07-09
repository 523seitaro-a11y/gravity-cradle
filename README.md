# Gravity Cradle

立方体デバイスを傾けて、Unity内のステージと重力方向を90度単位で切り替えるプロトタイプです。

XIAO ESP32S3に接続したAE-BNO055-BOの姿勢をWi-Fi UDPで送り、既存のUnityゲームプロトタイプへ反映します。センサーが切断されている間は、従来のマウスクリック操作を使用できます。

## Repository

- `unity/`: Unity 6000.1.0f1プロジェクト
- `firmware/GravityCradleSensor/`: XIAO ESP32S3用Arduinoスケッチ
- `docs/`: 配線、起動、確認手順

## Quick Start

1. Unity Hubから`unity/`を開きます。
2. `firmware/GravityCradleSensor/GravityCradleSensor.ino`のWi-Fi情報とPCのIPアドレスを設定してXIAO ESP32S3へ書き込みます。
3. Unityで`Assets/Scenes/SampleScene.unity`を開いてPlayします。
4. 画面左上の表示が`connected`になったら、デバイスを6方向へ傾けます。

詳しい手順は[`docs/setup.md`](docs/setup.md)を参照してください。

## UDP Packet

```json
{"seq":128,"qw":0.99,"qx":0.01,"qy":0.03,"qz":0.02,"face":"left","ms":123456}
```

## Prototype Scope

今回の対象はBNO055入力、UDP通信、90度スナップ、既存ステージ回転への接続です。ゲーム本編のルール、連続姿勢同期、重力固定ボタン、筐体と電源方式は今後決めます。
