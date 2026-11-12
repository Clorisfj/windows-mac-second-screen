# 给 Mac 端 agent

目标：在这台 Mac 构建并验证 CFlow 菜单栏接收端，与现有 Windows CFlow 配对。先读取 README.md 和 Mac/Sources，不重装 Sunshine、不覆盖 Moonlight/Deskflow 既有配对和权限。

1. 检查 macOS 14、Apple Command Line Tools、Moonlight.app、Deskflow.app，以及原 Deskflow 是否已配置为 Windows 客户端。
2. 先保存本机未提交修复，再拉取 `cflow-v2-menu-bar` 最新提交并合并。运行 `bash Mac/build.command` 和 `bash tests/run-mac.sh`，修复真实编译错误并重新构建；Windows 环境没有验证 SwiftUI/Network/Security 代码，不要直接宣称它们可用。
3. 将应用放到 /Applications，按用户授权正常打开，不关闭 Gatekeeper、系统防护或签名检查。
4. 验证 Dock 无图标、菜单栏有 C/双屏缩略标志，点击弹出五行控制面板，点外部收起；不点击弹窗也应启动 TLS 接收端。
5. 核对齿轮在同一菜单栏面板内打开设置、返回主页面。设置显示 Mac IP；先点击允许配对，再由 Windows 输入此 IP 搜索，核对两端相同的 6 位数字后在 Mac 允许。验证取消、拒绝、配对窗口到期、错误来源地址与证书更换均被拒绝。保留钥匙串与原控制配对，不把密钥或私钥贴入对话/报告。
6. Sunshine/Moonlight 和 Deskflow 原配对独立。检查 Moonlight CLI 参数与 Deskflow core 的实际包内路径、Mac 权限和主机地址；只修正当前安装对应的最小差异。
7. Windows 与 Mac 必须均更新至此协议版本。Windows 保持设置页打开，从 Mac 发起模式请求，验证处理中和最终结果。Windows 失败时 Mac 显示具体错误，Mac 离线时禁止排队请求。与 Windows 配合测试普通 → 扩展屏 → 普通 → 共用鼠标 → 普通；核对分辨率/FPS/码率、退出收尾和错误回退。不用“进程存活”代替“画面和鼠标可用”。
8. 检查错误证书/无效控制密钥被拒绝；配对撤销后旧 Windows 密钥应失效；恢复默认保留证书/配对。
9. 报告真实测试结果与未完成项，不上传凭据，不替用户自动开启登录项。


## 2026-11-13 退出加速与参数重连，build 5

保留 Mac 本机已有修改，再合并本次 Model.swift 停止逻辑及 Info.plist 构建号，构建安装 2.1.0 build 5。

- 官方 Moonlight 6.2.0 的 app/main.cpp 在 Q_OS_UNIX 下注册 SIGTERM，信号处理线程先中断 Session 并请求 Qt 退出；第二个信号会立即 _Exit。现在对 Moonlight 发送一次 SIGTERM，不再同时发送 AppKit 退出。其他 GUI 与核心进程各自去重，保留超时强退和退出验证。
- 停止日志改写入 CFlow control.log，包含信号路径、超时回退和总耗时。确认正常停止不再固定等待约 5 秒，且无新崩溃。先正常退出旧 Moonlight/CFlow，再替换构建。
- 在 CFlow 目录执行 bash Mac/build.command 和 bash tests/run-mac.sh，核实运行路径与 build 5。
- Windows 已支持保持 Sunshine/VDD 进程进行分辨率与帧率重配。与 Windows 测试 2880×1800/60 → 2560×1600/60 → 1920×1200/30 → 2880×1800/60，并测试码率变化、关闭和共用鼠标。每次确认实际画面和参数后继续，报告完整重连和退出耗时、是否强退、是否新增崩溃。
- Windows 本机六组模式实测重配约 0.16～0.34 秒，未重启 Sunshine；这不包含 Mac 停止/启动和视频出画面时间。

## 2026-11-12 Moonlight 退出修复，build 4

用户已确认原停止函数先调用 Process.terminate，再立即对同一个 GUI 进程请求 NSRunningApplication.terminate。Moonlight 6.2.0 于 22:21:53.7004 UTC+08 在 SDL3 Cocoa_VideoQuit 路径 SIGABRT；报告只有 abort() called，没有 Last Exception Backtrace，尚未证实具体崩溃触发原因。

本轮修改 Mac/Sources/Model.swift 的 stopApplications、quit 和 Mac/Info.plist 构建号。保留 Mac 本机已经验证可用的编译、连接和其他修复，再合并这些改动。

- 对匹配的 GUI 应用先发正常退出请求；同 PID 的跟踪子进程不会再立即收到 SIGTERM。未列为 GUI 目标的核心子进程单独使用 SIGTERM。
- 正常退出最多等待 5 秒；超时才对原快照中的目标强退，随后最多等待 2 秒验证退出。未退出时报错并保留子进程引用，禁止立即启动下一个模式。等待过程中重新打开的应用不纳入原快照的强退目标。
- CFlow 自身退出时若停止失败，会显示错误并继续运行，避免吞掉错误后再次进入紧急子进程终止路径。
- 在真实 Mac 执行 bash Mac/build.command 和 bash tests/run-mac.sh，安装并核实 2.1.0 build 4。先手动正常退出 Moonlight，再退出旧 CFlow 并替换。保留原配对与权限。
- 与 Windows 配合测试开启扩展屏、画面稳定后关闭、修改码率重连、切换共用鼠标、退出 CFlow。重复三轮，核对 stop 回复成功前旧进程确实退出、Windows 随后收回 VDD，以及 DiagnosticReports 是否新增 Moonlight 崩溃。检查 CFlow stop 日志，正常退出路径应不出现 timed out。
- 对超时路径使用独立测试进程验证，确认旧进程仍存活时不会回复成功。报告实际构建结果、退出耗时、是否走强退、是否新增崩溃。Windows 无法编译 AppKit 或验证 Mac 退出行为。

## Mac 2.1.0 界面更新验收

本次主要改动在 Mac/Sources/CFlowApp.swift、MenuBarController.swift、build.command 与 Info.plist。先保留 Mac 本机已经验证过的编译和连接修复，再合并本次界面变更。

- 构建并检查版本号为 2.1.0、构建号 3。退出旧进程后替换 /Applications/CFlow.app，从该路径启动并核实正在运行的程序路径。
- 原生 NSStatusItem 承载 438×500 的 NSPopover。左键切换面板，点外部收起；右键和 Control+点按显示打开、设置、退出。验证右键退出实际结束 CFlow。
- 主面板与 Windows 使用相同的分组、卡片、两种模式、参数顺序和齿轮位置。设置、双端配对、Moonlight 说明、Deskflow 状态在同一面板内逐级返回。
- 原图为 1536×1024。构建先居中裁成 1024×1024，再生成 icns，验证应用程序目录、启动台及面板中的图标比例。
- 浅色/深色跟随系统；窗口隐藏时仍能接收 Windows 请求。已有连接可用的前提下，重测扩展屏与共用鼠标的开启、关闭。
- Windows 环境只检查了脚本语法和 plist，尚未在 Mac 编译此 AppKit/SwiftUI 界面。本机真实结果应单独报告。
