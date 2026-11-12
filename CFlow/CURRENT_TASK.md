# CFlow 当前接续任务

## 工程与目标

GitHub 仓库为 Clorisfj/windows-mac-second-screen，工作分支 cflow-v2-menu-bar。CFlow 是 Windows 主控与 Mac 菜单栏接收端，控制 Sunshine/VDD/Moonlight 扩展屏和 Deskflow 共用鼠标。

## 已确认进度

- 用户确认当前安装版本的扩展屏与共用鼠标均可连接。
- Windows 启动优化版已构建，配置与协议测试 36 项、助手通信测试 10 项、实际助手启动探针 3 项通过。
- IP 配对使用 Mac 本机短时允许、两端数字核对及本机批准；旧配对数据继续沿用。
- 用户已由 Mac agent 更新安装 Mac CFlow，并确认 Windows 可以控制连接。Mac 当前安装版本与本仓库源码的对应关系仍需 Mac 端核实。

## 2026-11-12 Windows 启动优化与待查问题

- 历史日志显示本机 Sunshine 启动约 27 秒。当前版本默认手柄探测会重复等待虚拟手柄授权查询。CFlow 在 controller 未显式配置时备份 Sunshine 配置并设置 controller = disabled；显式设置优先，键鼠仍沿用原配置。
- 同分辨率、帧率、布局、捕获 GUID 且串流服务就绪时，参数重连复用现有 Sunshine/VDD。冷启动等待 GameStream HTTP 和 RTSP 就绪，记录 Windows 与 Mac 各阶段耗时。
- 一轮实际 Coordinator 联调记录：Windows 准备 13.070 秒，含助手启动/授权和 Mac 连接的冷启动至 Sunshine CLIENT CONNECTED 20.524 秒；仅改码率后重连 5.210 秒，Sunshine PID 保持。普通模式 1.263 秒，共用鼠标启动确认 2.614 秒。
- 本机主屏保持 2560×1440 / 180Hz，停止后 VDD 已断开。监听和 CLIENT CONNECTED 仅确认传输会话，Mac 首帧和稳定性需实测。
- 用户在该轮快速切换后报告 Moonlight 意外退出，重新打开正常。已暂停连续串流切换测试，等待 Mac 最新崩溃报告中的异常、终止原因和线程调用栈。不能将本轮联调视为稳定性验收通过。

## 接手执行

先读 AGENTS.md、README.md 和 Mac交接.md。Mac 已有可用版本和可能尚未提交的本机修复，先保存这些修改，再合并开发分支。保留既有配对、证书、权限与用户配置。

在 Mac 运行 `bash Mac/build.command` 和 `bash tests/run-mac.sh`。处理真实编译问题，退出旧 CFlow 后安装新版本，核实实际运行路径与版本号。检查菜单栏左键弹出、右键退出、点外部收起、设置页原面板返回、系统主题和应用图标比例，再验证两端模式切换及真实画面/键鼠。

Windows 对应检查为 `CFlow/Windows/build.ps1`、`CFlow/tests/run.ps1`、`CFlow/tests/run-admin.ps1`，这些路径相对于仓库根目录。实际设备测试结果应与云端构建结果分别记录。

## 跨设备接续

当前 Windows 项目目录为 D:/BOT/win和mac电脑联动，实际 Git 工作副本在其 windows-mac-second-screen 子目录。活动安装源码在同级 CFlow 目录，复制到 Git 工作副本后才会随提交发布。

用户已创建发布 windows-mac-second-screen 云环境，该环境初始仓库引用为 main。云端任务应检查后切到 cflow-v2-menu-bar，读取本文件继续。同账号另一台设备可以继续同一云端任务；Mac 本机编译与设备操作需要 Mac 执行环境及对应访问权限。
