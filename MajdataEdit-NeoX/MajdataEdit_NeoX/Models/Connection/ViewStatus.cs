namespace MajdataEdit_NeoX.Models.Connection;

/// <summary>
/// 播放器（MajdataViewX 端）的界面状态，供编辑器侧的状态机与 Inspector 使用。
///
/// MajProtocol 2.0 只用一个 <c>StateResponse.isPlaying</c> 表达播放态，
/// 不再有 Idle/Loaded/Error 这些细分；这些细分是编辑器侧自己的关注点，
/// 因此由本地维护，不参与线格式。
///
/// 没有 Busy：请求在 <see cref="PlayerConnection"/> 的 endpoint 内部串行排队，
/// 调用方 await 命令本身就是在等上一条结束。
/// </summary>
public enum ViewStatus
{
    /// <summary>未加载任何谱面。</summary>
    Idle,
    /// <summary>已加载谱面与资源，等待播放。</summary>
    Loaded,
    /// <summary>播放中。</summary>
    Playing,
    /// <summary>已暂停，可继续播放。</summary>
    Paused,
    /// <summary>播放器侧出错。</summary>
    Error,
}

/// <summary>
/// 编辑器的播放模式。MajProtocol 2.0 的 <c>PlayRequest</c> 用两个布尔量表达：
/// <c>showIntroAnimation</c>（是否播放曲目信息入场动画）与 <c>record</c>（是否录制视频）。
/// </summary>
public enum PlaybackMode
{
    /// <summary>普通播放，直接进入谱面。</summary>
    Normal,
    /// <summary>带入场动画播放（录制/展示用）。</summary>
    IncludeOp,
    /// <summary>带入场动画播放并录制视频。</summary>
    Record,
}
