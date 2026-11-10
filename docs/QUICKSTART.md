# 用户极简教程

## 第一次

1. 两台电脑接同一局域网。
2. 将 [README 的一句话指令](../README.md#让-agent-帮你装)发给 agent。
3. 按提示处理管理员授权、Sunshine初始密码和首次配对。
4. Mac打开Moonlight → Windows电脑 → Desktop，把一个Windows窗口拖到Mac验证。

## 每天使用

**Windows开机保持唤醒 → Mac打开Moonlight → Desktop。**

不用手动打开VDD或Sunshine，不用重复配对。结束常用快捷键：**Control＋Option＋Shift＋Q**。

## 参数与排障

- 本例2018款15英寸Mac：2880×1800、60FPS、HEVC、40Mbps；VDD200%缩放。其他Mac让agent自动匹配。
- 频繁Slow connection：先降到约30Mbps，保留分辨率和60FPS，再比较有线网络。
- 统计常用快捷键：**Control＋Option＋Shift＋S**。播放60FPS动态画面看数据，静态30FPS不一定异常。快捷键按实际版本核对。
- 主机无声音：取消Moonlight“串流时将主机扬声器静音”。这不一定关闭Mac声音；Mac单独静音由版本和你的选择决定。
- 连接不上：检查Windows是否睡眠、是否同一局域网及Sunshine服务；换网后让agent核对主机新地址，不先重装。
- 断开后找不到窗口：虚拟屏可能仍启用，重连把窗口拖回。

网线优先改善稳定性，不自动改善颜色。Mac可用USB-C千兆网卡；普通USB-C线直连不能直接传这套串流。

不要关闭驱动签名或系统防护。长期是否适合运行，结合半小时后的统计、Mac活动监视器和风扇判断。
