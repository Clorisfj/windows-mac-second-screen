# CFlow 2

Windows 主控 + Mac 原生菜单栏接收端。主面板仅保留两个互斥模式开关、分辨率、帧率和码率；外观跟随系统。

## Windows

打开 `CFlow.exe`。界面不要求管理员权限；首次执行模式操作时才请求控制助手授权。助手只在 CFlow 运行期间按需存在，没有新增后台服务或开机任务。

首次配对：Mac 菜单栏 CFlow → 设置 → 允许配对；Windows CFlow → 齿轮 → 双端控制配对，输入 Mac 页面显示的 IP 并搜索。核对两端相同的 6 位数字，在 Mac 点允许，Windows 自动保存连接。密钥使用 Windows 当前用户 DPAPI 保护。既有配对可继续使用。

共用鼠标也需要先完成 CFlow 双端控制配对；未配对时点击开关会进入配对页，不会启动两端软件。设置 → Deskflow 连接中可复制 Windows 地址、查看端口与当前监听状态。Deskflow 原应用的配对和权限继续沿用。

Moonlight 串流配对入口显示已有配对说明与首次配对步骤；Sunshine 停止时不打开网页。

两种模式均关闭即普通模式；扩展屏关闭会收回 VDD。窗口默认收起到托盘，真正退出在托盘菜单。开机启动默认关闭；开启也只启动面板，不自动开启模式。

源码构建：`Windows/build.ps1`。现有 Sunshine/VDD/Deskflow 引擎针对本机安装路径；旧工具保留在相邻 `ScreenDesk` 目录，新模式启动前会请求它退出。

## Mac：首次安装

1. 将本目录中的 `Mac` 和 `assets` 一起复制到 Mac；也可以解压准备好的 `CFlow-Mac-source.zip`。
2. 在 Mac 运行 `bash Mac/build.command`。需要 macOS 14 和 Apple Command Line Tools；缺少工具时脚本会提示安装方式，不绕过系统安全设置。
3. 将 `Mac/dist/CFlow.app` 放入 `/Applications` 后打开。只显示菜单栏小图标，点击图标弹出面板，点击外部收起。右键菜单提供打开、设置和退出。Mac 2.1.0 使用同一面板内的设置导航。
4. 点击面板内齿轮进入设置，查看 Mac IP 并开启“允许配对（两分钟）”。Windows 输入此 IP 搜索后，核对两端数字并在 Mac 允许连接。设置使用原菜单栏面板，点击返回回到主界面。

接收端在应用启动时运行，无需先点开弹窗。没有开启登录启动时，每次 Mac 开机后需要手动打开一次 CFlow。

## 既有应用与配对

- 必须已有 Sunshine/Moonlight 串流配对；CFlow 配对不替代它。
- Mac Moonlight 官方 CLI 用于启动 Desktop、HEVC、全屏及匹配分辨率/FPS/码率。
- Deskflow 沿用原来 Mac 的客户端主机、证书及系统辅助功能权限。CFlow 不重建这些数据。若 Windows 地址变化，需先在 Deskflow 原应用更新主机。
- 声音选项是“保留 Windows 本机声音”。它不会自动静音整个 Mac；不承诺仅主机声音与单应用静音同义。
- 参数变化会触发受控重连，存在短暂黑屏。已就绪且捕获目标正确时，码率、分辨率和帧率变更优先复用 Sunshine 与 VDD。驱动尚不支持目标模式或重配失败时回退完整启动。首次冷启动会为 VDD 补齐面板支持的三种分辨率各 30/60Hz 预设。
- Windows 启动扩展屏时，会先备份 Sunshine 配置，对未显式配置的 controller、system_tray 默认关闭手柄输入和 Sunshine 独立托盘，减少初始化与退出等待；由 CFlow 托盘提供控制。用户可在 Sunshine 设置中显式开启，CFlow 尊重这些设置。备份存放在 CFlow 的 backups 目录。

## 安全与设置

控制通道仅支持局域网 IPv4，使用 TLS 1.2 或更高；Windows 固定 Mac 证书 SHA256，并使用 256 位随机控制密钥。Mac 密钥保存在钥匙串。已配对控制指令包含状态、扩展屏、Deskflow、停止。首次配对使用受限的挑战与确认请求。

首次按 IP 配对需在 Mac 本机开启短时配对窗口，并核对验证码后批准。控制密钥和证书私钥不能公开。Mac 设置中可让旧控制配对失效；“恢复默认设置”只重置工具偏好，保留配对、证书、驱动和主屏布局。

诊断数据只在本机，不自动上传。网络短暂中断不会立即撤销模式；Windows 发现 Mac 超过约 20 秒失联时尝试恢复普通模式。

## 验收边界

Windows 已有本地构建与测试入口；Mac 源码必须在真实 Mac 上构建、授权并验证。菜单栏弹窗、登录项、证书导入、Deskflow 路径以及 Moonlight 实际视频连接不能仅凭 Windows 构建判为成功。完整双端验收必须在安装 Mac 端后进行。

异常关闭/断电不保证收尾；不要卸载驱动来恢复鼠标边界，可用原版工具恢复普通模式。

## 双端请求与检查

Windows 打开设置页时继续接收 Mac 请求。Mac 只有收到已认证的 Windows 连接才显示在线并启用模式开关。切换显示处理中和最终结果，Windows 本机失败会返回具体错误。助手初始化期间定期发送状态，短时断开后的恢复由原逻辑处理。

Windows 验证入口为 `tests/run.ps1` 和 `tests/run-admin.ps1`。Mac 更新后运行 `bash Mac/build.command` 和 `bash tests/run-mac.sh`，随后在真实 Mac 检查菜单栏内设置、IP 配对、拒绝/取消、两端模式切换与实际画面和跨屏输入。
