using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using SDSM_Surveyor_App.Data;
using SDSM_Surveyor_App.ViewModels;

namespace SDSM_Surveyor_App.Views.Windows;

/// <summary>자료함 창 — 조사 세션 목록 보기·불러오기·삭제·복제.</summary>
public partial class SessionBrowserWindow : Window
{
    private readonly SessionBrowserViewModel _vm;

    public SessionBrowserWindow()
    {
        InitializeComponent();
        _vm = App.Current.Services.GetRequiredService<SessionBrowserViewModel>();
        DataContext = _vm;

        // 삭제는 되돌릴 수 없으므로 반드시 확인을 받는다.
        _vm.ConfirmDelete = e => MessageBox.Show(
            $"'{e.Site} · {e.YearChsu}' 세션을 삭제할까요?\n입력한 7개 분류군 자료가 함께 지워집니다.",
            "세션 삭제", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;

        // 지금 화면을 덮어쓰는 동작([불러오기]·[지점 복사]·[차수 복제]) 전에 저장 안 한 변경을 묻는다(B-5).
        var sessions = App.Current.Services.GetRequiredService<ISessionService>();
        _vm.ConfirmUnsaved = consequence => UnsavedChangesPrompt.ConfirmAsync(this, sessions, consequence);

        // 불러오기가 끝나면 창을 닫는다. 두 호출처가 모두 ShowDialog 라 DialogResult 를 세워도 안전하다 —
        // 첫 실행 화면의 「자료함에서 이어하기」가 이 값으로 '세션을 실제로 열었는지'를 판단한다.
        _vm.LoadCompleted += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        // C-9 : 내보내기·가져오기 — 파일 고르기·같은 세션 확인·결과 요약
        _vm.PickExportPath = count =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "자료함 내보내기",
                Filter = "자료함 묶음 (*.zip)|*.zip",
                FileName = $"자료함_{DateTime.Now:yyyyMMdd_HHmm}_{count}건.zip",
            };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        };
        _vm.PickImportPath = () =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "자료함 가져오기", Filter = "자료함 묶음 (*.zip)|*.zip" };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        };
        _vm.AskConflict = s => ChoiceDialog.Ask(this, "같은 세션이 있습니다",
                $"「{s.Meta.Site} · {s.Meta.YearChsu}」({s.Meta.Project}) 세션이 자료함에 이미 있습니다.\n가져온 것으로 덮어쓰면 지금 자료함의 그 세션 내용은 사라집니다.",
                new ChoiceDialog.Option("나머지도 모두 덮어쓰기"),
                new ChoiceDialog.Option("덮어쓰기"),
                new ChoiceDialog.Option("나머지도 모두 건너뜀"),
                new ChoiceDialog.Option("건너뜀", IsPrimary: true, IsCancel: true)) switch
            {
                0 => SessionArchive.Conflict.OverwriteAll,
                1 => SessionArchive.Conflict.Overwrite,
                2 => SessionArchive.Conflict.SkipAll,
                _ => SessionArchive.Conflict.Skip,
            };
        _vm.ShowSummary = (title, text) => ChoiceDialog.Ask(this, title, text, new ChoiceDialog.Option("확인", IsPrimary: true, IsCancel: true));

        // C-8 : 일괄 출력 — 폴더 고르기 · 있는 파일 덮어쓰기 확인 · 결과([폴더 열기])
        _vm.PickFolder = () =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "일괄 출력 폴더 고르기" };
            return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
        };
        _vm.ConfirmOverwriteFiles = files => ChoiceDialog.Ask(this, "같은 이름 파일이 있습니다",
                $"고른 폴더에 같은 이름 파일이 {files.Count}개 있습니다.\n{string.Join("\n", files.Take(8))}{(files.Count > 8 ? $"\n… 외 {files.Count - 8}개" : "")}\n\n덮어쓰면 그 파일의 내용은 사라집니다.",
                new ChoiceDialog.Option("취소", IsPrimary: true, IsCancel: true),
                new ChoiceDialog.Option("덮어쓰기")) == 1;
        _vm.PickReportPath = defaultName =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "보고서 일괄 — 통합문서 하나로 저장",
                Filter = "Excel 통합 문서 (*.xlsx)|*.xlsx",
                FileName = defaultName,
            };
            return dlg.ShowDialog(this) == true ? dlg.FileName : null;
        };
        // 저장 뒤 안내는 단건 저장과 같다 — [닫기] · [폴더 열기] · [파일 열기](기본 · C-1)
        _vm.ShowReportResult = (path, text) => Export.ReportExporterBase.OfferToOpen(path, text);
        // 문제 5 : 일괄입력용 일괄에서 빠지는 종(개체수 0·빈칸) 요약을 한 번만 묻는다
        _vm.ConfirmSkippedSpecies = text => Export.BulkSkippedSpecies.Ask(this, text);
        _vm.ShowBatchResult = (folder, text) =>
        {
            if (ChoiceDialog.Ask(this, "일괄 출력 완료", $"{folder}\n\n{text}",
                    new ChoiceDialog.Option("닫기", IsCancel: true),
                    new ChoiceDialog.Option("폴더 열기", IsPrimary: true)) == 1)
                Export.ReportExporterBase.Shell(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        };

        Loaded += async (_, _) => await _vm.RefreshAsync();
    }

    // 여러 세션 고르기(Ctrl·Shift) → VM 에 넘긴다(C-9 내보내기 · C-8 일괄 출력)
    private void Grid_SelectionChanged(object sender, Telerik.Windows.Controls.SelectionChangeEventArgs e) =>
        _vm.SelectedSessions = SessionGrid.SelectedItems.Cast<SDSM_Surveyor_App.Models.SessionIndexEntry>().ToList();

    // 행 더블클릭 → 불러오기(그리드 빈 영역 더블클릭은 무시)
    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.LoadSessionCommand.CanExecute(null)) _vm.LoadSessionCommand.Execute(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
