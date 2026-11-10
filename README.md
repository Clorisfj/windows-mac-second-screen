> **开发分支：CFlow 2** — 新的 Windows 控制面板与 Mac 菜单栏接收端正在联调。请看 [CFlow 文档](CFlow/README.md) 和 [Mac agent 交接](CFlow/Mac交接.md)。Mac 尚未实机编译，不是正式发行版。

# Mac 当 Windows 扩展屏

免费开源方案：**Sunshine + VDD + Moonlight**。窗口可以从 Windows 主屏拖到 Mac，两个屏幕显示不同内容。

## 让 agent 帮你装

把这一句发给能操作电脑的 agent：

> 请按 https://github.com/Clorisfj/windows-mac-second-screen/blob/main/AGENTS.md 自动部署 Windows→Mac 扩展屏，保留物理主屏设置，完成后验证。

agent 会读取详细流程，检查已有安装、下载官方组件、配置虚拟屏和串流，并验证结果。管理员授权、首次密码和配对可能需要你参与。只能控制一台电脑时，会交接另一端的最少操作。

**这是 agent 执行指南，不是独立的一键安装程序。** 需要 agent 具备本机安装、配置权限；仓库不分发驱动或第三方软件。

## 日常只需三步

1. Windows 开机并保持唤醒，两台在同一局域网。
2. Mac 打开 Moonlight，点击已配对的电脑 → Desktop。
3. 把 Windows 窗口拖到 Mac；结束时断开串流。

Sunshine 服务和 VDD 驱动会随 Windows 启动，不必手动打开，也不必重复配对。

## 已验证案例

Windows 11 / RX 9070 GRE → 2018 款 15 英寸 Intel MacBook Pro：**2880×1800 / 60FPS / HEVC**。一次动态画面采样中，接收、解码、渲染约 60FPS，Mac 平均解码约 5ms，网络丢帧 0%。不代表所有设备或长期稳定性保证。

推荐先试约 40Mbps；拉满码率不等于更清楚，Wi-Fi 波动时先降低码率或改有线网络。

## 文档

- [Agent 部署流程](docs/DEPLOY.md)
- [用户极简教程](docs/QUICKSTART.md)
- [上游组件](#上游组件)

## 上游组件

[Sunshine](https://github.com/LizardByte/Sunshine) · [Virtual Display Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) · [Moonlight](https://github.com/moonlight-stream/moonlight-qt)

本仓库的部署文档采用 [MIT](LICENSE) 许可证；上游软件各自遵循其许可证。与上游项目无官方隶属关系。
