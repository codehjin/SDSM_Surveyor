namespace SDSM_Surveyor_App.Messengers;

/// <summary>토스트 알림 메시지 (WeakReferenceMessenger 용).</summary>
public class NotifyMessage
{
    public NotifyMessage((string message, bool isSuccess) payload)
    {
        Message = payload.message;
        IsSuccess = payload.isSuccess;
    }

    public string Message { get; }
    public bool IsSuccess { get; }
}
