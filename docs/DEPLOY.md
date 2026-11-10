# Agent 部署流程

目标：免费将 Mac 作为 Windows 真正扩展屏，优先稳定 60FPS、清晰文字和低延迟。实际执行，尽可能自动处理；遵守所在工具环境的权限和适用技能。

## 1. 检查与备份

- 识别当前操作系统及能控制的另一端；不具备 Mac/Windows 权限时完成可控端并生成简短交接，不假装能跨机操作。
- 检查 Windows 系统/架构/GPU、显示设备、已有 Sunshine/VDD；Mac 系统/架构、实际面板像素尺寸、Moonlight 和硬件解码支持。
- 记录物理主屏实际分辨率、刷新率、缩放、HDR、主屏标记与排列；后续不改。标称 240Hz 不代表当前一定是 240Hz。
- 检查网络、现存配对和活动串流。沿用有效配置，不重置；已有安装不为统一路径而重装。修改前备份相关配置。
- Deskflow 等输入共享工具仅在冲突时临时暂停，告知用户并记录恢复方式，不删除配置。
- 普通配置无需反复询问；UAC、凭据、首次配对或无法代办的权限才交给用户。驱动刷新和服务重启前告知可能断流。

## 2. 官方下载安装

执行当天核对最新兼容版本、包内容与架构，不固定资产名：

| 组件 | 官方入口 |
|---|---|
| Sunshine | https://github.com/LizardByte/Sunshine/releases/latest |
| VDD | https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/latest |
| Moonlight | https://github.com/moonlight-stream/moonlight-qt/releases/latest |

核验签名及官方哈希（如有）。自算哈希仅用于记录，不是独立真实性证明。公开包不需要 GitHub 账户；API 限流时用官方发布页，不索要 token。未知版本差异先查上游文档，不猜测。

**Sunshine / Windows**

- 默认沿用现有安装路径；新装且有 D 盘可用 `D:\moonlight`，否则用官方默认目录。遵守工具写入权限。
- 优先官方安装器静默安装、保留日志、禁止自动重启。实测 2026.914.233613 的 MSI 目录属性为 `INSTALL_ROOT`，未来版本先查属性。
- 沿用官方服务及必要的程序网络规则，设为开机自动启动。不开 UPnP、端口映射或公网管理。
- 检查服务、进程、管理端口和实际启动日志。通常管理地址 `https://localhost:47990`；已设置账号时 HTTP 401 是认证要求，不是启动失败。
- 管理密码由用户在本机设置，不写进脚本、仓库或报告。自签名证书的浏览器拦截按所在工具规则交接，不能擅自绕过。

**VDD / Windows**

- 已有正常实例就沿用；只创建一个 VDD，不动 MuMu 等其他虚拟设备或物理显卡。
- 控制程序可放 `D:\moonlight\VDD`；只装显示驱动，不顺带安装音频/手柄驱动。
- 优先官方安装流程；GUI 无效时查当前包和官方说明，再使用包内签名工具创建根设备。单纯把 INF 加入驱动仓库不算完成。
- 历史 x64 包 `SignedDrivers\x86\VDD\MttVDD.inf` 实际声明 `NTamd64`；不能仅看目录名，也不能套用到 ARM64。
- 历史 x64 安装方法：包内 `Dependencies\devcon.exe install "完整路径\MttVDD.inf" "Root\MttVDD"`。只在当前包仍匹配且实例不存在时使用，避免重复设备。
- 历史默认配置在 `C:\VirtualDisplayDriver\vdd_settings.xml`，独立于控制程序目录。先核对当前版本实际路径；只增补模式，不覆盖已有设置。
- 不开启测试签名，不改安全启动。综合核对驱动包签名、安装日志、设备/monitor 状态，不用单个 WMI 字段作唯一结论。

**Moonlight / Mac**

- 沿用兼容版本，否则从官方 DMG 安装到 `/Applications`，核对 Intel/Apple Silicon 和 macOS 支持。
- 不为此安装 Homebrew，不改 Gatekeeper。已有配对直接用；没有有效配对才首次输入 PIN。凭据及配对数据库不进报告或仓库。

## 3. 自动选择起点

- 优先可用的硬件 HEVC、SDR、60FPS；不默认开启 AV1、HDR、4:4:4 或软件解码。HEVC 不支持时回退硬件 H.264，并解释原因。
- 按 Mac 真实面板像素尺寸和比例匹配 VDD 与 Moonlight，避免把 macOS“看起来像”的逻辑分辨率当作面板像素数。
- 已验证案例：2018 款 15 英寸 MacBook Pro 用 **2880×1800/60Hz、虚拟屏200%缩放、Moonlight60FPS、约40Mbps** 起测。
- 其他设备按实际支持选择；不照搬该案例。性能不足时按屏幕比例降低分辨率，本例可退到 2560×1600，再退 1920×1200。
- “最优”由实测决定；码率拉满可能增加波动。对本例先试30～40Mbps，其他分辨率/编码另选合理起点。

## 4. 配置真正扩展屏

1. 备份后，在 VDD XML 的 `resolutions` 下补充所需模式。以本例为例：

```xml
<resolution>
  <width>2880</width>
  <height>1800</height>
  <refresh_rate>60</refresh_rate>
</resolution>
```

2. 告知短暂断流，必要时先停止 Sunshine，再刷新准确识别到的 VDD 设备。优先使用运行时实例ID，不能误动 AMD/NVIDIA/Intel 或其他虚拟适配器。
3. 等待设备正常后重新枚举显示器；刷新后 `DISPLAYxx` 编号可能变化，不复用旧编号。
4. 仅在 VDD 应用目标模式、60Hz、合适缩放和扩展拓扑；先验证系统接受模式再应用。不要设为主屏。已有排列尽量保留，新建可在左侧同顶对齐，核对边界不重叠。
5. 通过安全可验证的系统设置设置虚拟屏独立缩放；不为省事改全局缩放或未知注册表。无法自动完成时只交接这一个动作。
6. 启动 Sunshine，等待新启动日志的显示设备列表，核对 VDD 的 friendly_name、设备信息、当前映射、非主屏标记和分辨率。
7. 将日志中 VDD 的稳定 **`device_id` GUID** 写入 Sunshine **`output_name`**。不填写编号 `2`，不首选动态 `DISPLAYxx` 做持久配置。
8. 保留其他配置，重启 Sunshine，核实实际捕获目标和 `Capture size`。检查主屏基线没有改变。

## 5. Mac 连接、声音与验收

- Moonlight 采用相同像素分辨率、60FPS、所选codec和码率；需要时添加自定义分辨率。全屏并核对实际统计尺寸；高DPI设置以当前界面为准。
- 用户将一个 Windows 窗口拖到 Mac，主屏保留另一窗口，才能确认用户端真正扩展成功。没看到实际反馈时标记“待验收”。
- 默认保留 Windows 声音：取消“串流时将主机扬声器静音”，或启用同义的“在主机播放音频”。
- 保留主机声音不一定关闭 Mac 播放。优先核对 Moonlight 是否有独立静音；没有就向用户说明可选方案，不自动静音整台 Mac、不添加付费音频工具。
- 默认SDR。灰蒙检查色彩/范围，文字模糊检查分辨率/缩放与采样；不靠拉满码率解决。原彩/夜览只作告知后的可逆对照。
- 用确定60FPS的动态内容看接收、解码、渲染是否接近60FPS，网络丢帧/抖动是否增加，队列是否持续积压。静态约30FPS可能是按需送帧，不能单独判异常。
- 60FPS帧周期16.7ms，平均解码应明显低于它；这不能换算CPU占用，也不能排除长尾卡顿。常用统计快捷键 macOS `Control+Option+Shift+S`，无效则查当前版本。
- 接收丢帧/Slow connection：先降码率重测，再比较有线；接收60但解码/渲染落后：检查硬解后降分辨率；主机报错：按时间定位捕获/编码问题。每次只改一项。
- 最后报告实测参数、服务/捕获验证、用户端待办、声音状态、被暂停的工具、备份及恢复入口。通过后停止，不额外折腾。
- 30～60分钟稳定性、Mac CPU/内存压力/能耗及风扇作为后续观察，不声称已经证明全天稳定。

## 6. 出错与交接

- 日志只保留问题所需时间段，避免泄露凭据。启动编码器自测的失败不一定是最终会话失败。
- Mac临时日志常见在 `/tmp`，也查 `~/Library/Logs`，按实际版本定位；日志不能替代CPU/温度/能耗测量。
- 失败优先恢复本次配置、模式并启动 Sunshine；不卸载既有驱动，不动主显卡，不自动重启整机。等UAC时明确尚未完成。
- 只有一端可控时，交接另一端agent所需的已验证状态、参数、必要地址和剩余动作。地址/设备映射需重查；不交接密码、token或配对数据库。
- 日常说明交付 [QUICKSTART.md](QUICKSTART.md)。VDD和Sunshine自动随Windows启动；虚拟屏可能在断流后仍启用，不承诺自动收屏。

官方文档：[Sunshine](https://docs.lizardbyte.dev/projects/sunshine/latest/) · [VDD](https://github.com/VirtualDrivers/Virtual-Display-Driver/wiki) · [Moonlight](https://github.com/moonlight-stream/moonlight-docs/wiki)。

方法基于2026-11-10的实测部署：Sunshine 2026.914.233613、VDD 25.7.23（发布标题标为Beta），Moonlight公开入口当日为6.2.0。不是跨所有设备验证的通用安装器，未来版本以官方说明和实际状态为准。
