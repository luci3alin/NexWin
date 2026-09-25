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
    private void ShowAi()
    {
        PreparePage(NexLocale.T("ai_title"), NexLocale.T("ai_subtitle"));

        AddActionRow(
            NexLocale.CurrentLanguage == AppLanguage.En ? "Windows Services & Debloat" : "Servicii Windows & Telemetrie",
            NexLocale.CurrentLanguage == AppLanguage.En ? "Return to Windows background telemetry and service debloating controls." : "Înapoi la optimizarea serviciilor de fundal și dezactivarea telemetriei Windows.",
            NexIcon.Sliders, "SERVICII", PurpleBrush, CyanBrush, "DEBLOAT & AI",
            ActionBtn(NexLocale.CurrentLanguage == AppLanguage.En ? "Back to Services & Debloat" : "Înapoi la Servicii & Debloat", NexIcon.Sliders, PurpleBrush, () => { ShowServices(); return Task.CompletedTask; })
        );

        var state = NativeTuning.DetectLiveTuningState();

        AddActionRow(
            NexLocale.T("ai_copilot_title"),
            NexLocale.T("ai_copilot_desc"),
            NexIcon.Shield, state.CopilotDisabled ? NexLocale.T("status_disabled") : NexLocale.T("status_active"), state.CopilotDisabled ? GreenBrush : AmberBrush, CyanBrush, NexLocale.T("ai_copilot_badge"),
            ActionBtn(state.CopilotDisabled ? NexLocale.T("ai_copilot_btn_blocked") : NexLocale.T("ai_copilot_btn_disable"), NexIcon.Shield, CyanBrush, async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_copilot_title"), NexLocale.T("ai_copilot_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                    NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1) ? "Politica utilizator: TurnOffWindowsCopilot = 1" : "",
                    NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1) ? "Politica sistem: TurnOffWindowsCopilot = 1" : "",
                    NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0) ? "Buton taskbar ascuns (ShowCopilotButton = 0)" : ""
                }));
                ShowAi();
            }),
            RevertBtn(NexLocale.T("ai_copilot_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_copilot_title"), NexLocale.T("ai_copilot_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                    NativeTuning.DeleteRegistryValue(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot") ? "Politica utilizator Copilot restaurată." : "",
                    NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 1) ? "Buton taskbar vizibil." : ""
                }));
                ShowAi();
            })
        );

        AddActionRow(
            NexLocale.T("ai_recall_title"),
            NexLocale.T("ai_recall_desc"),
            NexIcon.Shield, state.RecallDisabled ? NexLocale.T("status_disabled") : NexLocale.T("status_active"), state.RecallDisabled ? GreenBrush : AmberBrush, PurpleBrush, NexLocale.T("ai_recall_badge"),
            ActionBtn(state.RecallDisabled ? NexLocale.T("ai_recall_btn_blocked") : NexLocale.T("ai_recall_btn_disable"), NexIcon.Shield, PurpleBrush, async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_recall_title"), NexLocale.T("ai_recall_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                    NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1) ? "Analiză date utilizator dezactivată." : "",
                    NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1) ? "Analiză date sistem dezactivată." : "",
                    NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "AllowRecallEnablement", 0) ? "Activare Recall blocată (AllowRecallEnablement = 0)." : ""
                }));
                ShowAi();
            }),
            RevertBtn(NexLocale.T("ai_recall_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_recall_title"), NexLocale.T("ai_recall_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                    NativeTuning.DeleteRegistryValue(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis") ? "Setare Recall restaurată." : ""
                }));
                ShowAi();
            })
        );

        AddActionRow(
            NexLocale.T("ai_edge_title"),
            NexLocale.T("ai_edge_desc"),
            NexIcon.Window, state.EdgeCopilotDisabled ? NexLocale.T("status_disabled") : NexLocale.T("status_active"), state.EdgeCopilotDisabled ? GreenBrush : AmberBrush, AmberBrush, NexLocale.T("ai_edge_badge"),
            ActionBtn(state.EdgeCopilotDisabled ? NexLocale.T("ai_edge_btn_blocked") : NexLocale.T("ai_edge_btn_disable"), NexIcon.Window, AmberBrush, async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_edge_title"), NexLocale.T("ai_edge_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                    NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "CopilotPageContext", 0) ? "Transmitere context pagină: Dezactivată" : "",
                    NativeTuning.SetRegistryDword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0) ? "Bara laterală Edge Hubs: Dezactivată" : ""
                }));
                ShowAi();
            }),
            RevertBtn(NexLocale.T("ai_edge_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_edge_title"), NexLocale.T("ai_edge_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                    NativeTuning.DeleteRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "CopilotPageContext") ? "Context pagină restaurat." : "",
                    NativeTuning.DeleteRegistryValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled") ? "Bara laterală restaurată." : ""
                }));
                ShowAi();
            })
        );

        var inkVal = NativeTuning.GetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection");
        bool inkDisabled = inkVal == 1;

        AddActionRow(
            NexLocale.T("ai_ink_title"),
            NexLocale.T("ai_ink_desc"),
            NexIcon.Shield, inkDisabled ? NexLocale.T("status_disabled") : NexLocale.T("status_active"), inkDisabled ? GreenBrush : AmberBrush, GreenBrush, NexLocale.T("ai_ink_badge"),
            inkDisabled
                ? ActionBtn(NexLocale.T("ai_ink_btn_enable"), NexIcon.Shield, CyanBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("ai_ink_title"), NexLocale.T("ai_ink_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                        NativeTuning.DeleteRegistryValue(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection") ? "Scriere de mână restaurată." : "",
                        NativeTuning.DeleteRegistryValue(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection") ? "Tastare restaurată." : ""
                    }));
                    ShowAi();
                })
                : ActionBtn(NexLocale.T("ai_ink_btn_disable"), NexIcon.Shield, RedBrush, async () => {
                    await ExecuteNativeSuiteAsync(NexLocale.T("ai_ink_title"), NexLocale.T("ai_ink_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                        NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1) ? "Eșantioane scriere de mână blocate." : "",
                        NativeTuning.SetRegistryDword(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1) ? "Eșantioane tastare blocate." : ""
                    }));
                    ShowAi();
                }),
            RevertBtn(NexLocale.T("ai_ink_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_ink_title"), NexLocale.T("ai_ink_desc"), NexLocale.T("status_success"), () => Task.FromResult(new List<string> {
                    NativeTuning.DeleteRegistryValue(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection") ? "Scriere de mână restaurată." : "",
                    NativeTuning.DeleteRegistryValue(RegistryHive.CurrentUser, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection") ? "Tastare restaurată." : ""
                }));
                ShowAi();
            })
        );

        AddActionRow(
            NexLocale.T("ai_all_title"),
            NexLocale.T("ai_all_desc"),
            NexIcon.Bolt, (state.CopilotDisabled && state.RecallDisabled && state.EdgeCopilotDisabled) ? NexLocale.T("status_clean") : NexLocale.T("status_warning"), (state.CopilotDisabled && state.RecallDisabled && state.EdgeCopilotDisabled) ? GreenBrush : PinkBrush, PinkBrush, NexLocale.T("ai_all_badge"),
            ActionBtn(NexLocale.T("ai_all_btn_remove"), NexIcon.Bolt, PinkBrush, async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_all_title"), NexLocale.T("ai_all_desc"), NexLocale.T("status_success"), () => NativeTuning.ApplyAiRemovalNativeAsync(false));
                ShowAi();
            }),
            RevertBtn(NexLocale.T("ai_all_btn_revert"), async () => {
                await ExecuteNativeSuiteAsync(NexLocale.T("ai_all_title"), NexLocale.T("ai_all_desc"), NexLocale.T("status_success"), () => NativeTuning.ApplyAiRemovalNativeAsync(true));
                ShowAi();
            })
        );
    }
}