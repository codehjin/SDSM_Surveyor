using System.Text.Json.Serialization;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 지점 지도 카탈로그(<c>maps.json</c>). 앱 데이터 폴더의 <c>maps\</c> 에 png 와 함께 둔다.
///
/// 지도는 <b>참고 자료</b>다. 조사자앱·관리자앱 어느 쪽에서도 자유롭게 고칠 수 있다
/// (승인·잠금 없음 · 부사장 확정 2026-09-09). 매년 갱신본은 메일로 주고받는다.
///
/// ⚠ JSON 키는 camelCase 다(시드 파일 기준). 읽을 때 <c>PropertyNameCaseInsensitive</c> 를 켜서
///   PascalCase 로 손으로 고친 파일도 받아 준다.
/// </summary>
public class MapCatalog
{
    public string? Version { get; set; }
    public string? Note { get; set; }
    public List<SiteMap> Maps { get; set; } = new();
}

/// <summary>지도 한 장.</summary>
public class SiteMap
{
    /// <summary>고유 키. 파일을 바꿔도 이 값은 유지한다.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>화면에 보이는 이름(곡교천 권역 2026 (기준)).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>파일명만 담는다. 폴더는 앱이 붙인다(경로 탈출 방지).</summary>
    public string File { get; set; } = string.Empty;

    /// <summary>제작 당시 픽셀 크기. 실제 파일과 다르면 보정을 믿지 않는다.</summary>
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>같은 기간에 여러 장일 때 구분(상류 · 하류 · 지점번호안 …).</summary>
    public string? Variant { get; set; }

    public string? Note { get; set; }

    /// <summary>조사자앱이 기본으로 여는 지도. 권역당 하나만 켠다.</summary>
    public bool IsLatest { get; set; }

    /// <summary>기본 탑재인지 사용자가 추가한 것인지(표시용 · 잠그는 데 쓰지 않는다).</summary>
    public string? Source { get; set; }

    /// <summary>좌표→픽셀 변환 계수. 없으면 그림만 보여 주고 마커를 찍지 않는다.</summary>
    public MapCalibration? Calibration { get; set; }

    /// <summary>지도가 덮는 경위도 범위. 없으면 보정에서 역산한다.</summary>
    public MapBounds? Bounds { get; set; }

    /// <summary>보정 적합 잔차(픽셀). 지도 자체의 인쇄 오차이지 계산 오류가 아니다.</summary>
    public double? FitMaxResidualPx { get; set; }

    /// <summary>이 지도를 어느 대분류·어느 지점에 보여 줄지.</summary>
    public MapAppliesTo? AppliesTo { get; set; }

    /// <summary>목록 표시용 — `곡교천 권역 2026 (기준) · 상류`.</summary>
    [JsonIgnore]
    public string Display => string.IsNullOrWhiteSpace(Variant) ? Name : $"{Name} · {Variant}";

    /// <summary>목록 표시용 크기 — `521×387`. 가로만 보이면 [교체] 뒤 세로가 달라진 걸 못 본다.</summary>
    [JsonIgnore]
    public string SizeText => $"{Width}×{Height}";

    /// <summary>보정이 쓸 수 있는 상태인지(계수 배열 길이까지 확인).</summary>
    [JsonIgnore]
    public bool HasCalibration =>
        Calibration?.X is { Length: 3 } && Calibration.Y is { Length: 3 };
}

/// <summary><c>px = X[0]*lon + X[1]*lat + X[2]</c> · <c>py = Y[0]*lon + Y[1]*lat + Y[2]</c>.</summary>
public class MapCalibration
{
    public double[]? X { get; set; }
    public double[]? Y { get; set; }
}

public class MapBounds
{
    public double LonMin { get; set; }
    public double LonMax { get; set; }
    public double LatMin { get; set; }
    public double LatMax { get; set; }
}

/// <summary>비어 있으면 모든 지점에 후보로 뜬다.</summary>
public class MapAppliesTo
{
    public string? Project { get; set; }

    // maps.json 은 사람이 손으로 고치는 파일이다. "sites": null 이 들어와도 터지지 않게 막는다.
    private List<string> _sites = new();
    public List<string> Sites
    {
        get => _sites;
        set => _sites = value ?? new List<string>();
    }
}
