using System.Text.Json;

namespace SDSM_Surveyor_App.Data;

/// <summary>
/// 세션(자료함)에 담기는 분류군 하나. 7개 EntryViewModel이 모두 구현한다.
/// 조사개황은 세션이 한 벌만 갖고 있으므로, 여기서 다루는 것은 분류군 고유 입력뿐이다.
/// </summary>
public interface ITaxonSession
{
    /// <summary>세션 파일의 분류군 키(Fish·Benthos·Bird…).</summary>
    string Key { get; }

    /// <summary>입력된 자료가 있는지(자료함 목록의 '입력 분류군' 판단).</summary>
    bool HasData { get; }

    /// <summary>
    /// 입력된 자료의 분량. 캡션 바 분류군 재선택 팝오버의 「n종 입력됨」에 쓴다.
    /// 종 입력형·관찰형은 종 행 수, 폼형은 값이 들어간 항목 수다
    /// (단위 문구는 <c>TaxonCatalog</c> 의 <c>CountUnit</c> 이 정한다).
    /// </summary>
    int EntryCount { get; }

    /// <summary>현재 화면 상태를 JSON 직렬화 가능한 스냅샷으로.</summary>
    object CaptureState();

    /// <summary>스냅샷을 화면에 되돌린다. 형식이 맞지 않으면 아무것도 바꾸지 않는다.</summary>
    void RestoreState(JsonElement json);

    /// <summary>분류군 고유 입력을 모두 비운다(지점 간 값 복사에서 사용).</summary>
    void ClearData();
}
