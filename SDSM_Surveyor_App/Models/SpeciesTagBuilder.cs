using System.Text;
using SDSM_Models;

namespace SDSM_Surveyor_App.Models;

/// <summary>
/// 관찰형 종의 보호종·교란종 표기 문자열을 만든다(그리드 '구분' 열 공용).
/// 생태계교란생물은 보호종과 의미가 반대이므로 문구를 구분해 붙인다.
/// </summary>
public static class SpeciesTagBuilder
{
    public static string Build(ObservedSpecies? sp)
    {
        if (sp is null) return string.Empty;

        var sb = new StringBuilder();
        void Add(string tag)
        {
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(tag);
        }

        if (!string.IsNullOrWhiteSpace(sp.Endangered1)) Add("멸종위기Ⅰ급");
        if (!string.IsNullOrWhiteSpace(sp.Endangered2)) Add("멸종위기Ⅱ급");
        if (!string.IsNullOrWhiteSpace(sp.NaturalMonument)) Add("천연기념물");
        if (!string.IsNullOrWhiteSpace(sp.Invasive)) Add("생태계교란생물");

        return sb.ToString();
    }
}
