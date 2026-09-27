# 架构与生命周期

```text
WPF Views ── MainWindow composition root
  ├─ HotkeyManager ── WM_INPUT / key-down edge tracking
  ├─ ProcessMonitor ── foreground EXE + async running process list
  ├─ RuleEngine ── enabled + key + exact EXE path → playback policy
  ├─ AudioEngine ── SegmentStream → WASAPI shared output
  ├─ AudioLibrary ── owned copies + TagLib metadata
  ├─ WaveformService ── async streaming decode → bounded peak cache
  ├─ BilibiliProvider ── official URL navigation → disposable WebView2
  ├─ Storage ── versioned JSON + atomic replace + backup recovery
  ├─ TrayService ── open / pause keys / stop / exit
  └─ Log ── bounded daily diagnostic files
```

## 线程和性能

Raw Input 在 WPF 消息线程接收，不进行媒体解码。无匹配按键时不枚举进程。匹配后检查前台路径，音频打开转移到后台任务；使用递增请求代号防止晚完成的旧播放覆盖新请求。

波形按流读取，以固定 2400 个峰值表示整段音频；峰值数组不足 10 KB，取消令牌可中止分析。解码与导入在后台执行。界面播放指针每 50 ms 更新，仅在音频编辑页面可见时运行。后台没有前台进程轮询定时器。

## 持久化

`settings.json` 包含 schema version、设置、媒体元数据、独立规则时间范围。临时文件写完后用 `File.Replace` 原子替换并保留 `.bak`。读取损坏文件先保留带时间的副本，再尝试恢复备份；不静默用空配置覆盖用户数据。

音频以 GUID 名称复制到 library，原始文件不更改。路径读取要求单个文件名，避免配置中的相对路径逃逸。删除媒体同时移除引用规则，先持久化元数据再清理媒体副本。

## 片段与行为

`SegmentStream` 在 PCM 帧边界返回 EOF，循环时只回到片段起点。边界精度不依赖 UI 定时器；压缩格式的寻址精度也受底层解码器影响。WASAPI 采用约 35 ms 缓冲；实际按键到声音的总延迟还包括磁盘读取、解码初始化和音频设备。

- Restart：重新播放同一条规则；其他规则正在播放或暂停时忽略。
- IgnoreWhilePlaying：任意音频正在播放或暂停时忽略。
- ReplaceCurrent：停止现有音频并播放新规则。

## 隐私和边界

没有遥测、音频上传、游戏状态访问。日志不记录按键流水、网页 URL 查询串、Cookie 或令牌。网站自身的网络请求与 Cookie 属于 WebView2 独立用户目录；本程序不读取这些 Cookie。相机、麦克风、定位等网页权限默认拒绝。

关闭到托盘保留 HWND 和输入注册。真正退出时注销 Raw Input、释放托盘图标、取消页面任务、关闭解码器和 WASAPI。单实例互斥锁避免重复触发。
