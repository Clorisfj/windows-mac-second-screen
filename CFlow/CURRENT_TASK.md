# CFlow 当前接续任务

## 工程与目标

GitHub 仓库为 Clorisfj/windows-mac-second-screen，工作分支 cflow-v2-menu-bar。CFlow 是 Windows 主控与 Mac 菜单栏接收端，控制 Sunshine/VDD/Moonlight 扩展屏和 Deskflow 共用鼠标。

## 已确认进度

- 用户确认当前安装版本的扩展屏与共用鼠标均可连接。
- Windows 启动优化版已构建，配置与协议测试 36 项、助手通信测试 10 项、实际助手启动探针 3 项通过。
- IP 配对使用 Mac 本机短时允许、两端数字核对及本机批准；旧配对数据继续沿用。
- 用户已由 Mac agent 更新安装 Mac CFlow，并确认 Windows 可以控制连接。Mac 当前安装版本与本仓库源码的对应关系仍需 Mac 端核实。

## 2026-11-12 Windows 图标与最新用户实测

- 用户报告 Mac 更新后扩展屏约 15 秒出画面，共用鼠标正常。Windows 对应一轮日志为本机准备 12.472 秒、Mac 启动确认累计 14.369 秒，与体感接近；这些不是 Mac 首帧的自动测量。
- Windows 原构建把 1536×1024 横图直接缩成单帧 48×48 ICO，导致图案变窄。改为居中裁成正方形，生成 16/20/24/32/40/48/64/128/256 九个 PNG 编码 ICO 帧，并将裁方图片用于面板标志。窗口与托盘加载 32px 图标。
- Windows 构建及九个 ICO 帧读取检查通过，面板图片为 1024×1024。已实际打开主面板与设置核对图标和导航。
- 后续优先检查 Mac 关闭扩展屏前约 5 秒等待是否进入强退回退，再考虑启动阶段状态提示与其他参数变更复用。冷启动仍主要花在 Sunshine 自身初始化，尚未验证进一步优化。

## 2026-11-12 Windows 启动优化与待查问题

- 历史日志显示本机 Sunshine 启动约 27 秒。当前版本默认手柄探测会重复等待虚拟手柄授权查询。CFlow 在 controller 未显式配置时备份 Sunshine 配置并设置 controller = disabled；显式设置优先，键鼠仍沿用原配置。
- 同分辨率、帧率、布局、捕获 GUID 且串流服务就绪时，参数重连复用现有 Sunshine/VDD。冷启动等待 GameStream HTTP 和 RTSP 就绪，记录 Windows 与 Mac 各阶段耗时。
- 一轮实际 Coordinator 联调记录：Windows 准备 13.070 秒，含助手启动/授权和 Mac 连接的冷启动至 Sunshine CLIENT CONNECTED 20.524 秒；仅改码率后重连 5.210 秒，Sunshine PID 保持。普通模式 1.263 秒，共用鼠标启动确认 2.614 秒。
- 本机主屏保持 2560×1440 / 180Hz，停止后 VDD 已断开。监听和 CLIENT CONNECTED 仅确认传输会话，Mac 首帧和稳定性需实测。
- 用户在该轮快速切换后报告 Moonlight 意外退出，重新打开正常。报告时间为 2026-11-12 22:21:53.7004 UTC+08，Moonlight 6.2.0，EXC_CRASH (SIGABRT)，Termination SIGNAL 6，主线程栈含 std::terminate、libdispatch 与 SDL3 Cocoa_VideoQuit。完整报告只有 abort() called，没有 Last Exception Backtrace，尚未确认具体崩溃根因。
- Mac agent 已确认原 stopApplications 先发送 Process.terminate，再无等待地调用 NSRunningApplication.terminate，同 PID 可能接收重复退出请求。已修正源码为 GUI 应用先请求正常退出，独立核心进程单独发送 SIGTERM，按 PID 去重；最多等待 5 秒后仅对原目标强退，再最多等待 2 秒核实。退出未完成则报错并保留子进程引用。
- Mac 源码版本为 2.1.0 build 4。本轮只有 Windows 侧源码审查、plist 与差异检查；尚未在 Mac 编译安装。Mac agent 必须保留本机现有修复，合并这次停止函数修改，再按 Mac交接.md 构建并复测。连续串流切换测试待 Mac 安装后恢复，当前尚未通过稳定性验收。

## 接手执行

先读 AGENTS.md、README.md 和 Mac交接.md。Mac 已有可用版本和可能尚未提交的本机修复，先保存这些修改，再合并开发分支。保留既有配对、证书、权限与用户配置。

在 Mac 运行 `bash Mac/build.command` 和 `bash tests/run-mac.sh`。处理真实编译问题，退出旧 CFlow 后安装新版本，核实实际运行路径与版本号。检查菜单栏左键弹出、右键退出、点外部收起、设置页原面板返回、系统主题和应用图标比例，再验证两端模式切换及真实画面/键鼠。

Windows 对应检查为 `CFlow/Windows/build.ps1`、`CFlow/tests/run.ps1`、`CFlow/tests/run-admin.ps1`，这些路径相对于仓库根目录。实际设备测试结果应与云端构建结果分别记录。

## 跨设备接续

当前 Windows 项目目录为 D:/BOT/win和mac电脑联动，实际 Git 工作副本在其 windows-mac-second-screen 子目录。活动安装源码在同级 CFlow 目录，复制到 Git 工作副本后才会随提交发布。

用户已创建发布 windows-mac-second-screen 云环境，该环境初始仓库引用为 main。云端任务应检查后切到 cflow-v2-menu-bar，读取本文件继续。同账号另一台设备可以继续同一云端任务；Mac 本机编译与设备操作需要 Mac 执行环境及对应访问权限。
