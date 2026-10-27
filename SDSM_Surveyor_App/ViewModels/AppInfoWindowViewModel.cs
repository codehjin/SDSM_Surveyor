using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.Helpers;
using SDSM_Surveyor_App.InjectableServices;

namespace SDSM_Surveyor_App.ViewModels;

/// <summary>
/// 프로그램 정보 창(v0.5.0 · §21-5) — 글은 <see cref="AppInfo"/> 한 곳, 종목록 판 · 종 수 · 출처는 <see cref="ISpeciesListProvider"/>.
/// [지침 원본 보기] · [사용설명서](v0.5.2 · §26-3)는 앱에 함께 넣은 PDF 를 기본 PDF 프로그램으로 연다.
/// </summary>
public partial class AppInfoWindowViewModel : ObservableObject, ITransientService
{
    private readonly ISpeciesListProvider _species;

    public AppInfoWindowViewModel(ISpeciesListProvider species)
    {
        _species = species;
        SpeciesParts = species.Parts
            .Select(p => new SpeciesPartLine(p.Name, $"{p.Count:N0}종", p.Version, p.Origin))
            .ToList();
    }

    public string ProgramName => AppInfo.ProgramName;
    public string ProgramKind => AppInfo.ProgramKind;
    public string VersionLine => $"{AppInfo.VersionText}  (커밋 {AppInfo.Commit})";
    public string BuildDate => AppInfo.BuildDate;

    /// <summary>화면 그리기 — 그래픽카드 / 소프트웨어(software_render 파일)(§25).</summary>
    public string RenderLine => RenderModeSwitch.Description;

    public string GuidelineTitle => AppInfo.GuidelineTitle;
    public string GuidelineNotice => AppInfo.GuidelineNotice;

    public string SpeciesBasis => AppInfo.SpeciesListBasis;
    public string SpeciesVersion => _species.Version ?? "-";

    /// <summary>분류군별 종 수 · 판 · 출처(교체본 · 번들).</summary>
    public IReadOnlyList<SpeciesPartLine> SpeciesParts { get; }

    public string Developer => AppInfo.Developer;

    /// <summary>연락처 — ' · ' 마다 한 줄(관리자 정보 창과 같게).</summary>
    public IReadOnlyList<string> ContactLines =>
        AppInfo.Contact.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>[지침 원본 보기] · [사용설명서] 결과 안내(열지 못했을 때 까닭).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenStatus))]
    private string? _openStatus;

    public bool HasOpenStatus => !string.IsNullOrEmpty(OpenStatus);

    /// <summary>마지막으로 연 PDF 경로(시험이 확인한다).</summary>
    public string? LastOpenedPath { get; private set; }

    [RelayCommand]
    private void OpenGuideline() =>
        Open(AppInfo.GuidelinePdfPath, $"지침 원본 파일을 찾지 못했습니다 — {AppInfo.GuidelinePdfPath}");

    [RelayCommand]
    private void OpenManual() => Open(AppInfo.ManualPdfPath, AppInfo.ManualMissingText);

    private void Open(string path, string missingText)
    {
        OpenStatus = AppInfo.OpenPdf(path, missingText);
        if (OpenStatus is null) LastOpenedPath = path;
    }
}

/// <summary>정보 창 종목록 한 줄.</summary>
public sealed record SpeciesPartLine(string Taxon, string Count, string Version, string Origin);
