using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace NexWin.Native;

public partial class MainWindow : Window
{
    private void ShowServices()
    {
        PreparePage(NexLocale.T("services_title"), NexLocale.T("services_subtitle"));

        AddActionRow(
            NexLocale.CurrentLanguage == AppLanguage.En ? "AI & Recall Removal Module" : "Modulul AI & Recall Removal",
            NexLocale.CurrentLanguage == AppLanguage.En ? "Disable Windows Copilot, Windows Recall snapshots, and AI search integrations." : "Dezactivează Windows Copilot, capturile automate Windows Recall și integrările AI din Windows 11.",
            NexIcon.Shield, "MODUL AI", CyanBrush, PurpleBrush, "DEBLOAT & AI",
            ActionBtn(NexLocale.CurrentLanguage == AppLanguage.En ? "Open AI & Recall Removal" : "Deschide AI & Recall Removal", NexIcon.Shield, CyanBrush, () => { ShowAi(); return Task.CompletedTask; })
        );

        var state = NativeTuning.DetectLiveTuningState();

        AddActionRow(
            NexLocale.T("services_diagtrack_title"),
            NexLocale.T("services_diagtrack_desc"),
            NexIcon.Sliders, state.DiagTrackDisabled ? NexLocale.T("status_disabled") : NexLocale.T("status_active"), state.DiagTrackDisabled ? GreenBrush : AmberBrush, PurpleBrush, NexLocale.T("services_diagtrack_badge"),
            ActionBtn(state.DiagTrackDisabled ? NexLocale.T("services_diagtrack_btn_stopped") : NexLocale.T("services_diagtrack_btn_stop"), NexIcon.Sliders, PurpleBrush, async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("services_diagtrack_title"), NexLocale.T("services_diagtrack_desc"), NexLocale.T("status_success"), async () => {
                    NativeTuning.SetServiceStartup("DiagTrack", 4);
                    await NativeTuning.StopServiceAsync("DiagTrack");
                    return new List<string> { NexLocale.T("services_log_diagtrack_off", "DiagTrack setat pe Dezactivat și oprit.") };
                });
                ShowServices();
            }),
            RevertBtn(NexLocale.T("services_diagtrack_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("services_diagtrack_title"), NexLocale.T("services_diagtrack_desc"), NexLocale.T("status_success"), async () => {
                    NativeTuning.SetServiceStartup("DiagTrack", 2);
                    await NativeTuning.StartServiceAsync("DiagTrack");
                    return new List<string> { NexLocale.T("services_log_diagtrack_on", "DiagTrack setat pe Automat și pornit.") };
                });
                ShowServices();
            })
        );

        AddActionRow(
            NexLocale.T("services_dmwappush_title"),
            NexLocale.T("services_dmwappush_desc"),
            NexIcon.Sliders, state.DmwappushDisabled ? NexLocale.T("status_disabled") : NexLocale.T("status_active"), state.DmwappushDisabled ? GreenBrush : AmberBrush, CyanBrush, NexLocale.T("services_dmwappush_badge"),
            ActionBtn(state.DmwappushDisabled ? NexLocale.T("services_dmwappush_btn_stopped") : NexLocale.T("services_dmwappush_btn_stop"), NexIcon.Sliders, CyanBrush, async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("services_dmwappush_title"), NexLocale.T("services_dmwappush_desc"), NexLocale.T("status_success"), async () => {
                    NativeTuning.SetServiceStartup("dmwappushservice", 4);
                    await NativeTuning.StopServiceAsync("dmwappushservice");
                    return new List<string> { NexLocale.T("services_log_dmwappush_off", "dmwappushservice oprit și dezactivat.") };
                });
                ShowServices();
            }),
            RevertBtn(NexLocale.T("services_dmwappush_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("services_dmwappush_title"), NexLocale.T("services_dmwappush_desc"), NexLocale.T("status_success"), async () => {
                    NativeTuning.SetServiceStartup("dmwappushservice", 2);
                    await NativeTuning.StartServiceAsync("dmwappushservice");
                    return new List<string> { NexLocale.T("services_log_dmwappush_on", "dmwappushservice setat pe Automat.") };
                });
                ShowServices();
            })
        );

        AddActionRow(
            NexLocale.T("services_wersvc_title"),
            NexLocale.T("services_wersvc_desc"),
            NexIcon.Sliders, state.WerSvcDisabled ? NexLocale.T("status_disabled") : NexLocale.T("status_active"), state.WerSvcDisabled ? GreenBrush : AmberBrush, AmberBrush, NexLocale.T("services_wersvc_badge"),
            ActionBtn(state.WerSvcDisabled ? NexLocale.T("services_wersvc_btn_stopped") : NexLocale.T("services_wersvc_btn_stop"), NexIcon.Sliders, AmberBrush, async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("services_wersvc_title"), NexLocale.T("services_wersvc_desc"), NexLocale.T("status_success"), async () => {
                    NativeTuning.SetServiceStartup("WerSvc", 4);
                    await NativeTuning.StopServiceAsync("WerSvc");
                    return new List<string> { NexLocale.T("services_log_wersvc_off", "WerSvc oprit și dezactivat.") };
                });
                ShowServices();
            }),
            RevertBtn(NexLocale.T("services_wersvc_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("services_wersvc_title"), NexLocale.T("services_wersvc_desc"), NexLocale.T("status_success"), async () => {
                    NativeTuning.SetServiceStartup("WerSvc", 3);
                    return new List<string> { NexLocale.T("services_log_wersvc_manual", "WerSvc setat pe Manual.") };
                });
                ShowServices();
            })
        );

        AddActionRow(
            NexLocale.T("services_maint_title"),
            NexLocale.T("services_maint_desc"),
            NexIcon.Clean, NexLocale.T("services_maint_badge"), GreenBrush, NexLocale.T("services_maint_chips"),
            ActionBtn(NexLocale.T("services_maint_btn"), NexIcon.Clean, GreenBrush, () => ExecuteNativeSuiteAsync(NexLocale.T("services_maint_title"), NexLocale.T("services_maint_desc"), NexLocale.T("status_success"), () => NativeTuning.ApplyMaintenanceNativeAsync()))
        );

        AddActionRow(
            NexLocale.T("services_visual_title"),
            NexLocale.T("services_visual_desc"),
            NexIcon.Gauge, NexLocale.T("services_visual_badge"), CyanBrush, NexLocale.T("services_visual_chips"),
            ActionBtn(NexLocale.T("services_visual_btn"), NexIcon.Gauge, CyanBrush, () => ExecuteNativeSuiteAsync(NexLocale.T("services_visual_modal_title", "Efecte Vizuale"), NexLocale.T("services_visual_modal_working", "Optimizare efecte vizuale Windows..."), NexLocale.T("services_visual_modal_done", "Efectele vizuale au fost optimizate."), () => NativeTuning.ApplyVisualEffectsNativeAsync(true)))
        );
    }
}