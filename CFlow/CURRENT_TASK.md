# CFlow 当前接续任务

## 工程与目标

GitHub 仓库为 Clorisfj/windows-mac-second-screen，工作分支 cflow-v2-menu-bar。CFlow 是 Windows 主控与 Mac 菜单栏接收端，控制 Sunshine/VDD/Moonlight 扩展屏和 Deskflow 共用鼠标。

## 已确认进度

- 用户确认当前安装版本的扩展屏与共用鼠标均可连接。
- Windows 最新源码已构建，协议测试 30 项、助手通信测试 10 项、实际助手启动探针 3 项通过。
- IP 配对使用 Mac 本机短时允许、两端数字核对及本机批准；旧配对数据继续沿用。
- 本次 Mac 界面源码标记为 2.1.0 build 3，尚未在真实 Mac 编译安装。包含同面板设置导航、与 PC 对齐的布局、菜单栏右键退出和应用图标比例修复。

## 接手执行

先读 AGENTS.md、README.md 和 Mac交接.md。Mac 已有可用版本和可能尚未提交的本机修复，先保存这些修改，再合并开发分支。保留既有配对、证书、权限与用户配置。

在 Mac 运行 `bash Mac/build.command` 和 `bash tests/run-mac.sh`。处理真实编译问题，退出旧 CFlow 后安装新版本，核实实际运行路径与版本号。检查菜单栏左键弹出、右键退出、点外部收起、设置页原面板返回、系统主题和应用图标比例，再验证两端模式切换及真实画面/键鼠。

Windows 对应检查为 `CFlow/Windows/build.ps1`、`CFlow/tests/run.ps1`、`CFlow/tests/run-admin.ps1`，这些路径相对于仓库根目录。实际设备测试结果应与云端构建结果分别记录。

## 跨设备接续

当前 Windows 项目目录为 D:/BOT/win和mac电脑联动，实际 Git 工作副本在其 windows-mac-second-screen 子目录。活动安装源码在同级 CFlow 目录，复制到 Git 工作副本后才会随提交发布。

使用云端任务时选择本仓库和开发分支，然后读取本文件继续。用户目前没有已保存的 Codex Cloud 环境。同账号另一台设备可以继续同一云端任务；Mac 本机编译与设备操作需要 Mac 执行环境及对应访问权限。
