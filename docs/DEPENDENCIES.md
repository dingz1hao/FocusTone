# 依赖调研与架构决策

核对日期：2026-09-27。版本号以项目文件与 NuGet 锁文件为准。

| 组件 | 采用方式 | 许可证 / 说明 |
|---|---|---|
| .NET 10 / WPF | Windows 原生桌面、事件消息循环 | MIT；运行时包含自己的第三方声明 |
| NAudio 2.2.1 | WASAPI 共享输出、Media Foundation 解码 | MIT，固定经过验证的 2.x API；3.x 有较大 API 调整 |
| NAudio.Vorbis 1.5.0 | OGG Vorbis 解码适配 | MIT |
| NVorbis 0.10.4 | 托管 Vorbis 解码 | MIT，包含其随包 LICENSE |
| TagLibSharp 2.3.0 | 标题与封面读取，独立 DLL | LGPL-2.1-only；未修改；可替换 DLL；提供源代码链接和分发源码 |
| Microsoft.Web.WebView2 | 仅官方网页导航与播放 | Microsoft WebView2 SDK 许可；Runtime 使用微软许可 |
| xUnit / .NET Test SDK | 开发和 CI 测试，不随应用发布 | Apache-2.0 / MIT，详见各 NuGet 包 |

## 为什么选择 WPF

应用仅面向 Windows。WPF 可直接处理 HWND 消息，整合 Raw Input、系统托盘、WASAPI 和 Media Foundation，避免额外常驻 Web UI 运行时。WebView2 仅在 Bilibili 页面打开时创建，离开时销毁；核心本地功能不依赖网络或网页组件。

## 输入与进程

参考微软 [Raw Input 文档](https://learn.microsoft.com/windows/win32/inputdev/using-raw-input)。注册键盘和鼠标 `RIDEV_INPUTSINK`，处理 `WM_INPUT`。只在匹配配置按键时查询 `GetForegroundWindow`，以 `QueryFullProcessImageName` 比较完整路径。

没有 `SetWindowsHookEx`、DLL 注入、驱动、游戏内存访问或前台高频轮询。有限进程查询权限只用于识别 EXE。程序不会阻止原始按键到达游戏。

## Bilibili 调研与最终取舍

- [yt-dlp Bilibili 实现](https://github.com/yt-dlp/yt-dlp/blob/master/yt_dlp/extractor/bilibili.py)：成熟项目仍在维护相关适配，但网页与接口会变化、可能触发访问限制。
- [Nemo2011/bilibili-api](https://github.com/Nemo2011/bilibili-api)：核对时仓库已归档并声明停止维护（2026-07-06），不适合作为新项目依赖。
- 本公开版本最终不调用平台非公开数据接口，不复用以上项目提取代码，也不提供平台音频下载。独立 Provider 只生成官方搜索 / 视频 URL，并验证导航域名。
- 搜索结果、标题、UP 主、封面、分 P 与试听由未修改的 Bilibili 官方页面呈现。没有脚本注入、网络拦截提取、广告移除或会员能力替代。

## 上游与对应源代码

- [NAudio v2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1)
- [NAudio.Vorbis](https://github.com/naudio/Vorbis)
- [NVorbis](https://github.com/NVorbis/NVorbis)
- [TagLibSharp 2.3.0.0](https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0)
- [WebView2 WPF 文档](https://learn.microsoft.com/microsoft-edge/webview2/get-started/wpf)

`scripts/Publish.ps1` 将许可证和 TagLibSharp 对应源码归档一并放入发布目录。TagLibSharp 动态链接，不使用单文件合并或裁剪；允许用户修改、重新构建并替换 DLL 来调试库的变更。软件许可证不授予任何第三方音视频内容的使用权。
