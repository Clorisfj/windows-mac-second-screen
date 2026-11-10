# 给 Mac 端 agent

目标：在这台 Mac 构建并验证 CFlow 菜单栏接收端，与现有 Windows CFlow 配对。先读取 README.md 和 Mac/Sources，不重装 Sunshine、不覆盖 Moonlight/Deskflow 既有配对和权限。

1. 检查 macOS 14、Apple Command Line Tools、Moonlight.app、Deskflow.app，以及原 Deskflow 是否已配置为 Windows 客户端。
2. 在本目录运行 `bash Mac/build.command`，修复真实编译错误并重新构建；Windows 环境没有验证 SwiftUI/Network/Security 代码，不要直接宣称它们可用。
3. 将应用放到 /Applications，按用户授权正常打开，不关闭 Gatekeeper、系统防护或签名检查。
4. 验证 Dock 无图标、菜单栏有 C/双屏缩略标志，点击弹出五行控制面板，点外部收起；不点击弹窗也应启动 TLS 接收端。
5. 核对设置中配对码生成、钥匙串、接收端端口 47999、系统浅深色、登录项默认关闭。不要把完整配对码、私钥或密钥贴入对话/报告；由用户在两端本机界面复制。
6. Sunshine/Moonlight 和 Deskflow 原配对独立。检查 Moonlight CLI 参数与 Deskflow core 的实际包内路径、Mac 权限和主机地址；只修正当前安装对应的最小差异。
7. 与 Windows 配合测试普通 → 扩展屏 → 普通 → 共用鼠标 → 普通；核对分辨率/FPS/码率、退出收尾和错误回退。不用“进程存活”代替“画面和鼠标可用”。
8. 检查错误证书/无效控制密钥被拒绝；配对撤销后旧 Windows 密钥应失效；恢复默认保留证书/配对。
9. 报告真实测试结果与未完成项，不上传凭据，不替用户自动开启登录项。
