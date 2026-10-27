using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SDSM_Surveyor_App.InjectableServices;
using SDSM_Surveyor_App.Messengers;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>
/// 화면 아래 알림 띠(지시서 §27-2 · v0.5.2) — <see cref="NotifyMessage"/> 를 받는 곳.
/// 종전에는 보내는 곳만 있고 받는 곳이 없어 저장 · 내보내기 · 붙여넣기 알림이 화면에 뜨지 않았다.
/// <list type="bullet">
/// <item>한 번에 하나. 여러 개면 차례로(같은 글이 이미 떠 있거나 기다리면 다시 쌓지 않는다).</item>
/// <item>성공은 <see cref="SuccessTime"/>, 주의는 <see cref="WarnTime"/> 뒤 사라진다. [×] 로 바로 닫는다.</item>
/// <item>포커스를 가져가지 않는다 — 띠는 상태 바 위 한 줄이고 단추 하나([×])만 있다.</item>
/// </list>
/// 앱 시작 때(분류군 고르기 창보다 먼저) 만들어 둔다 — 고르기 창 → 자료함에서 보낸 알림도 야장 창이 뜨면 보인다.
/// </summary>
public partial class NoticeBandViewModel : ObservableObject, ISingletonService, IRecipient<NotifyMessage>
{
    public static readonly TimeSpan SuccessTime = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan WarnTime = TimeSpan.FromSeconds(8);

    private readonly Queue<NotifyMessage> _queue = new();
    private readonly DispatcherTimer _timer;

    public NoticeBandViewModel()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal);
        _timer.Tick += (_, _) => ShowNext();
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>지금 띠에 보이는 알림(없으면 null — 띠가 접힌다).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown), nameof(Text), nameof(FullText), nameof(IsSuccess))]
    private NotifyMessage? _current;

    public bool IsShown => Current is not null;

    /// <summary>띠 글자 — 한 줄(줄바꿈은 ' · '). 넘치면 말줄임, 전체는 툴팁.</summary>
    public string Text => Current is null ? string.Empty : string.Join(" · ", Lines(Current.Message));

    /// <summary>툴팁 — 여러 줄 그대로.</summary>
    public string FullText => Current?.Message ?? string.Empty;

    public bool IsSuccess => Current?.IsSuccess == true;

    /// <summary>뒤에 기다리는 알림 수.</summary>
    public int Pending => _queue.Count;

    private static IEnumerable<string> Lines(string s) =>
        s.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

    public void Receive(NotifyMessage message)
    {
        var d = Application.Current?.Dispatcher;
        if (d is not null && !d.CheckAccess()) { d.BeginInvoke(() => Receive(message)); return; }
        if (string.IsNullOrWhiteSpace(message.Message)) return;

        // 같은 글이 이미 떠 있으면 시간만 새로 · 기다리는 줄에 있으면 다시 쌓지 않는다
        if (Current is { } c && c.Message == message.Message && c.IsSuccess == message.IsSuccess) { Restart(); return; }
        if (_queue.Any(q => q.Message == message.Message && q.IsSuccess == message.IsSuccess)) return;

        _queue.Enqueue(message);
        OnPropertyChanged(nameof(Pending));
        if (Current is null) ShowNext();
    }

    /// <summary>[×] — 지금 알림을 닫고 다음 것을 보인다.</summary>
    [RelayCommand]
    private void Dismiss() => ShowNext();

    private void ShowNext()
    {
        _timer.Stop();
        Current = _queue.Count > 0 ? _queue.Dequeue() : null;
        OnPropertyChanged(nameof(Pending));
        if (Current is not null) Restart();
    }

    private void Restart()
    {
        _timer.Stop();
        _timer.Interval = Current?.IsSuccess == true ? SuccessTime : WarnTime;
        _timer.Start();
    }
}
