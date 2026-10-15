using System.Diagnostics;
using System.IO;
using System.Text;
using Telerik.Windows.Controls;

namespace SDSM_Surveyor_App.Helpers;

/// <summary>
/// 열 폭 감사 (Debug 빌드 전용).
///
/// design.md §7 : <b>판정은 「선언 폭 == 실제 폭」이다.</b>
/// <c>RadGridView</c> 는 열 폭 합이 뷰포트를 넘어도 가로 스크롤을 만들지 않고 **열을 균등하게 뭉갠다.**
/// 그래서 "가로 스크롤이 없다"를 기준으로 삼으면 아무것도 걸러내지 못한다(90_TECH_NOTES §2).
/// 눈으로는 3~4px 씩 줄어든 것을 못 알아채므로 실제 값을 파일로 받아 둔다.
///
/// 실행 폴더의 <c>column_widths.log</c> 에 쓴다. 창 폭이 바뀔 때마다 덮어쓰므로
/// 1280 으로 줄여 둔 상태에서 마지막으로 남은 내용이 그 폭의 판정이다.
/// </summary>
public static class LayoutAudit
{
    /// <summary>선언 폭과 실제 폭의 허용 오차(px). 렌더 반올림만 허용한다.</summary>
    private const double Tolerance = 0.6;

    [Conditional("DEBUG")]
    public static void CheckColumnWidths(RadGridView? grid, string screen)
    {
        if (grid is null) return;

        // ⚠ Loaded·SizeChanged 시점에는 열이 아직 측정되지 않아 ActualWidth 가 전부
        //    최소폭(20)으로 읽힌다. 레이아웃이 끝난 뒤(ContextIdle)에 다시 읽는다.
        grid.Dispatcher.BeginInvoke(new Action(() => Write(grid, screen)),
                                    System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private static void Write(RadGridView grid, string screen)
    {
        // 화면이 숨어 있으면 측정 자체가 없다 — 잘못된 FAIL 을 남기지 않는다.
        if (!grid.IsVisible || grid.ActualWidth <= 0) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# 열 폭 감사 · {screen} · {DateTime.Now:HH:mm:ss}");
            sb.AppendLine($"# 창 {System.Windows.Application.Current?.MainWindow?.ActualWidth:0} " +
                          $"· 그리드 뷰포트 {grid.ActualWidth:0}");
            sb.AppendLine($"{"열",-14}{"선언",8}{"실제",8}  판정");
            sb.AppendLine(new string('-', 44));

            double declared = 0;
            int fails = 0;

            foreach (var col in grid.Columns)
            {
                var header = col.Header?.ToString() ?? "(무제)";
                var actual = col.ActualWidth;

                if (col.Width.IsAbsolute)
                {
                    var want = col.Width.Value;
                    declared += want;
                    var ok = Math.Abs(actual - want) <= Tolerance;
                    if (!ok) fails++;
                    sb.AppendLine($"{header,-14}{want,8:0}{actual,8:0.0}  {(ok ? "OK" : "✗ 뭉갬")}");
                }
                else
                {
                    sb.AppendLine($"{header,-14}{"*",8}{actual,8:0.0}  (가변)");
                }
            }

            sb.AppendLine(new string('-', 44));
            sb.AppendLine($"고정폭 합 {declared:0}px · design.md §7 한도 1,104px " +
                          $"{(declared <= 1104 ? "이내" : "초과 ✗")}");
            sb.AppendLine(fails == 0
                ? "판정: PASS — 선언 열 폭이 그대로 유지됩니다"
                : $"판정: FAIL — {fails}개 열이 선언 폭보다 좁습니다(뷰포트 부족)");

            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "column_widths.log"), sb.ToString());
        }
        catch
        {
            // 감사 실패가 화면을 막아서는 안 된다.
        }
    }
}
