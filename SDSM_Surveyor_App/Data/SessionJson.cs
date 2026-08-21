using System.Text.Json;

namespace SDSM_Surveyor_App.Data;

/// <summary>세션 저장/복원에 쓰는 공통 JSON 설정. 구버전 임시저장 파일도 같은 규칙으로 읽는다.</summary>
public static class SessionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
