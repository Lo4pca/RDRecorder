# DOC.md

本文档面向开发者，详细说明 RDRecorder 插件的内部架构、模块职责和关键技术实现。

## 1. 整体架构

核心模块分布如下：

| 命名空间 | 职责 |
|----------|------|
| `RDRecorder` | 主入口（Plugin.cs），负责初始化、Harmony 补丁加载和全局对象创建。 |
| `RDRecorder.Core` | 核心状态管理（`GameManager`）和时间模拟（`TimeMockManager`）。 |
| `RDRecorder.Record` | 视频录制相关：`RecorderController`、`FrameCapturer`、`FFmpegEncoder`。 |
| `RDRecorder.Record.Audio` | 音频录制相关：`AudioRecorderController`、`AudioCapturer`、`FFmpegAudioEncoder`。 |
| `RDRecorder.Playback` | 视频播放相关：`PlaybackController`、`VideoRenderer`。 |
| `RDRecorder.Tools` | 辅助工具：`EventFilter`（事件过滤）、`PathInfo`（路径生成）。 |
| `RDRecorder.Config` | 配置管理（`PluginConfig`），基于 BepInEx Config。 |
| `RDRecorder.UI` | 配置面板 UI（`ConfigUI`），使用 Unity 的 `OnGUI` 系统。 |

## 2. 核心模块详解

### 2.1 GameManager（状态机）

- 维护当前应用状态：`Idle`、`Recording`、`PlayingBack`、`AudioRecording`。
- 提供 `StartRecording()` / `StopRecording()` / `StartPlayback(string)` / `StopPlayback()` / `StartAudioRecording()` / `StopAudioRecording()` 方法。
- 各方法会检查状态合法性，并动态添加/启用对应的控制器组件（`RecorderController`、`PlaybackController`、`AudioRecorderController`）。

### 2.2 TimeMockManager（时间模拟）

- **目的**：使游戏逻辑在录制时以固定的时间步长前进，而非实时时间。
- **实现**：通过 Harmony 补丁拦截 `AudioSettings.dspTime` 和 `Time.unscaledDeltaTime` 的 getter 方法，返回模拟值。
  - `_mockDspTime` 从真实的 `AudioSettings.dspTime` 初始化，之后每帧增加 `1.0 / TargetFPS`。
  - `Time.unscaledDeltaTime` 固定返回 `1.0 / TargetFPS`。
- 这样，所有依赖 `dspTime` 的节奏计算（如音符生成）和依赖 `unscaledDeltaTime` 的动画/物理均能以录制帧率运行。

### 2.3 视频录制流水线

1. **用户触发** → `GameManager.StartRecording()` → 添加/启用 `RecorderController`。
2. `RecorderController.OnEnable()` 添加 `FFmpegEncoder` 和 `FrameCapturer`，但尚未开始编码。
3. 监听 `LevelEvent_PlaySong.Run` Harmony 补丁，当关卡音乐开始时调用 `BeginRecording()`。
4. `BeginRecording()`:
   - 调用 `FFmpegEncoder.BeginEncoding()`，启动 FFmpeg 进程（`ffmpeg -f rawvideo -pix_fmt rgba ...`）。
   - 设置 `Time.captureFramerate = TargetFPS`（强制 Unity 主循环以该帧率运行）。
   - 调用 `TimeMockManager.StartMocking()`。
   - 调用 `FrameCapturer.BeginCapture()` 开始协程循环。
   - 将 `AudioListener.volume` 置 0（静音）。
5. `FrameCapturer` 的协程每帧：
   - `yield return new WaitForEndOfFrame()`。
   - 调用 `ScreenCapture.CaptureScreenshotIntoRenderTexture()` 捕获当前屏幕至 `RenderTexture`。
   - 通过 `AsyncGPUReadback.Request` 异步读取像素数据。
   - 回调中将 `byte[]` 数据入队到 `FFmpegEncoder`。
   - 调用 `TimeMockManager.AdvanceFrame()` 推进模拟时间。
6. `FFmpegEncoder` 的后台线程从队列取数据，写入 FFmpeg 的 stdin。
7. 监听 `LevelEvent_FinishLevel.Run`，自动调用 `GameManager.StopRecording()`。
8. `StopRecording()` → `RecorderController.OnDisable()` 停止捕获、终止 FFmpeg 进程、恢复时间（`Time.captureFramerate` 复原，`TimeMockManager.StopMocking()`），并通过协程延迟 3 秒恢复音量（避免恢复时产生噪声）。

### 2.4 音频录制流水线

- 类似视频录制，但使用 `AudioCapturer` 挂载到 `AudioListener` 对象上，利用 `OnAudioFilterRead` 回调获取 PCM 数据。
- `AudioRecorderController` 在 `PlaySong` 事件触发时：
  - 在 `AudioListener` 上添加 `AudioCapturer` 并关联 `FFmpegAudioEncoder`。
  - 启动 FFmpeg 进程：`ffmpeg -f f32le -ar {sampleRate} -ac 2 -i - -c:a aac -b:a 192k output.m4a`。
  - 设置 `EventFilter` 过滤非必要事件，减少 CPU 负载。
- 数据入队至 `FFmpegAudioEncoder` 后台线程写入管道。
- 关卡结束时自动停止，输出 M4A 文件。

### 2.5 视频播放

- `PlaybackController` 启用后，添加 `VideoRenderer` 并启用 `EventFilter`（抑制视觉事件）。
- `VideoRenderer.OnEnable()` 根据当前场景类型选择渲染方式：
  - **游戏场景**：创建一个 `ScreenSpaceOverlay` Canvas，覆盖全屏的 `RawImage`，将 `VideoPlayer` 的输出渲染到 `RenderTexture` 并显示。
  - **编辑器场景（scnEditor）**：直接劫持 `scnEditor.instance.gameView`（一个 `RawImage`），将 `VideoPlayer` 的 `RenderTexture` 赋值给它，覆盖编辑器预览窗口。
- `VideoPlayer` 设置为 `playOnAwake = false`，在 `LevelEvent_PlaySong.Run` 补丁中调用 `Play()`，确保与音乐同步。
- `LevelEvent_FinishLevel.Run` 补丁中调用 `GameManager.StopPlayback()`，自动清理资源并恢复场景渲染。

### 2.6 EventFilter（事件过滤）

- 用于录制音频或播放视频时，过滤掉不必要的关卡事件（如视觉特效、UI 事件等），仅保留节奏控制、音乐播放、计分等关键事件。
- 实现方式：在 `LevelBase.levelEventsPerBar` 的每个列表元素上，替换为过滤后的列表，并备份原始列表以便恢复。
- 过滤规则定义在 `FilterEvents` 方法中，允许列表硬编码。

### 2.7 配置系统（PluginConfig）

- 使用 BepInEx `ConfigFile` 存储设置，支持热重载。
- 配置项：
  - `TargetFPS`（int，默认 60）
  - `OutputFolder`（string，默认 `GameRoot/Recordings`）
  - `MenuHotkey`、`RecordHotkey`、`PlaybackHotkey`、`AudioRecordHotkey`（均为 `KeyCode`）
- 提供 `TryEnsureOutputFolder()` 方法，确保输出目录存在，并在启动时和保存配置时验证。

### 2.8 UI 面板（ConfigUI）

- 使用 `OnGUI` + `GUILayout.Window` 实现。
- 包含 FPS 输入框（录制/播放时禁用）、输出文件夹路径（带“Browse”按钮，调用文件对话框）、保存配置按钮、三个模式切换按钮。
- 快捷键响应通过 `Update` 中的 `Input.GetKeyDown` 实现，调用对应的 `Toggle` 方法。

## 3. 依赖与构建

- **目标框架**：.NET Standard 2.1
- **引用**：
  - BepInEx 5
  - Harmony
  - UnityEngine（版本 6000.3.10，即 Unity 2023+）
  - 游戏自定义 DLL（`RDLevelEditor`、`RDClass` 等）位于 `lib/` 文件夹（未包含在仓库中）
- **构建**：
  - 环境变量 `RDRECORDER_PLUGIN_OUTPUT` 必须指定输出目录（用于将 DLL 拷贝到游戏插件目录）。
  - Release 配置下自动生成 ZIP 压缩包（`RDRecorder_vX.X.X.zip`）至项目根目录。
- **FFmpeg 依赖**：运行时需要 `ffmpeg.exe` 在PATH，用于编码和解码。

## 4. 已知限制与注意事项

- 视频录制时游戏画面会变慢（因为强制帧率且可能低于实时），但录制的视频是正常速度。
- 录制结束后的 3 秒静音延迟是必要的，以消除恢复实时音频时产生的爆音。
- 编辑器模式下播放视频会覆盖编辑器 Game 视图的显示，退出后恢复原样。
- 音频录制只支持立体声（2 通道），采样率使用 `AudioSettings.outputSampleRate`。