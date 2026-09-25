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
    private void ShowSnapshot()
    {
        PreparePage(NexLocale.T("snapshot_title"), NexLocale.T("snapshot_subtitle"));

        // 1. Top Undo Everything / Red Banner
        var alertCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 12, 18)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(76, 29, 42)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 16, 18, 16),
            Margin = new Thickness(0, 0, 0, 16)
        };

        var aGrid = new Grid();
        aGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        aGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var aInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var aTitle = new StackPanel { Orientation = Orientation.Horizontal };
        aTitle.Children.Add(CreateVectorIcon(NexIcon.Warning, RedBrush, 18));
        aTitle.Children.Add(new TextBlock
        {
            Text = "  " + NexLocale.T("snap_undo_title", "UNDO EVERYTHING - Restaurare Completă la Starea din Fabrică"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = RedBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        aInfo.Children.Add(aTitle);
        aInfo.Children.Add(new TextBlock
        {
            Text = NexLocale.T("snap_undo_desc", "Anulează toate modificările aplicate de NexWin: reactivează AI/Recall, resetează profilurile de gaming, repornește serviciile Windows standard, restaurează schema de energie Balanced și resetează stiva TCP/IP."),
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(220, 180, 190)),
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(aInfo, 0);
        aGrid.Children.Add(aInfo);

        var aActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };

        var undoBtn = new Button
        {
            Style = (Style)FindResource("DangerActionButtonStyle"),
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        var uStack = new StackPanel { Orientation = Orientation.Horizontal };
        uStack.Children.Add(CreateVectorIcon(NexIcon.Revert, Brushes.White, 14));
        uStack.Children.Add(new TextBlock { Text = "  " + NexLocale.T("snap_btn_undo_all", "UNDO EVERYTHING"), FontWeight = FontWeights.Bold, Foreground = Brushes.White, FontSize = 12 });
        undoBtn.Content = uStack;

        undoBtn.Click += async (_, _) =>
        {
            bool ok = await ShowConfirmModalAsync(
                NexLocale.T("snap_undo_confirm_title", "Confirmare Restaurare Completă (UNDO EVERYTHING)"),
                NexLocale.T("snap_undo_confirm_msg", "Ești sigur că vrei să resetezi toate setările de sistem NexWin la starea standard?\n\n- AI & Recall vor fi reactivate\n- Setările de gaming și prioritate vor fi resetate\n- Serviciile Windows standard vor fi repornite\n- Schema de energie va reveni pe Balanced"),
                RedBrush, NexLocale.T("snap_btn_undo_confirm", "Da, Resetează Totul"), NexLocale.T("btn_cancel", "Anulează"));
            if (ok)
            {
                ShowNotification(NexLocale.T("snap_notif_rollback_title", "Rollback Complet"), NexLocale.T("snap_notif_rollback_msg", "Restaurare stări standard Windows..."), true);
                var logs = await NativeTuning.UndoEverythingAsync();
                foreach (var l in logs) AppendLog(NexLocale.Format("snap_log_undo_prefix_format", l), false);
                ShowNotification(NexLocale.T("snap_notif_rollback_done_title", "Rollback Finalizat"), NexLocale.T("snap_notif_rollback_done_msg", "Toate setările au fost readuse la starea inițială."), false, true);
                ShowToast(NexLocale.T("snap_toast_restored_title", "Sistem Restaurat"), NexLocale.T("snap_toast_restored_msg", "Windows a fost readus la configurarea standard."), NexIcon.Check, GreenBrush);
                ShowSnapshot();
            }
        };
        aActions.Children.Add(undoBtn);

        var createSnapBtn = MakeCardButton(NexLocale.T("snap_btn_new_snapshot", "+ Snapshot Nou"), NexIcon.Layers, CyanBrush, () =>
        {
            var snap = NativeTuning.CreateSnapshot(NexLocale.Format("snap_manual_name_format", DateTime.Now.ToString("dd MMM HH:mm")), false);
            AppendLog(NexLocale.Format("snap_log_created_format", snap.Id, snap.Name), false);
            ShowToast(NexLocale.T("snap_toast_saved_title", "Snapshot Salvat"), string.Format(NexLocale.T("snap_toast_saved_msg", "Punctul de control {0} a fost salvat cu succes."), snap.Id), NexIcon.Check, GreenBrush);
            ShowSnapshot();
            return Task.CompletedTask;
        }, true, 130);
        aActions.Children.Add(createSnapBtn);

        Grid.SetColumn(aActions, 1);
        aGrid.Children.Add(aActions);

        alertCard.Child = aGrid;
        PageRoot.Children.Add(alertCard);

        // 2. Snapshots History List
        var histCard = new Border
        {
            Background = CardBackground(),
            BorderBrush = new SolidColorBrush(Color.FromRgb(24, 38, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14)
        };

        var histStack = new StackPanel();
        var hTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        hTitle.Children.Add(CreateVectorIcon(NexIcon.Layers, CyanBrush, 16));
        hTitle.Children.Add(new TextBlock
        {
            Text = "  " + NexLocale.T("snap_history_header", "ISTORIC SNAPSHOT-URI & PUNCTE DE CONTROL"),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = CyanBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        histStack.Children.Add(hTitle);

        var snapshots = NativeTuning.GetSnapshots();
        if (snapshots.Count == 0)
        {
            var emptyBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 15, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 32, 46)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 4, 0, 0)
            };
            emptyBorder.Child = new TextBlock
            {
                Text = NexLocale.T("snap_empty_text", "Nu există snapshot-uri salvate în sistem.\nApasă pe '+ Snapshot Nou' pentru a crea un punct de control sau aplică un profil pentru auto-salvare."),
                FontSize = 12,
                Foreground = MutedBrush,
                TextAlignment = TextAlignment.Center
            };
            histStack.Children.Add(emptyBorder);
        }
        else
        {
            foreach (var snap in snapshots)
            {
                var sBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(9, 15, 24)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(20, 32, 46)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var sGrid = new Grid();
                sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                sGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var sInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var sTopRow = new StackPanel { Orientation = Orientation.Horizontal };
                sTopRow.Children.Add(new TextBlock
                {
                    Text = snap.Name,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextBrush
                });

                var sBadge = new Border
                {
                    Background = snap.IsAuto ? new SolidColorBrush(Color.FromArgb(40, 168, 85, 247)) : new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
                    BorderBrush = snap.IsAuto ? PurpleBrush : CyanBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(10, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = snap.IsAuto ? NexLocale.T("snap_badge_auto", "Auto-Snapshot") : NexLocale.T("snap_badge_manual", "Manual"),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = snap.IsAuto ? PurpleBrush : CyanBrush
                    }
                };
                sTopRow.Children.Add(sBadge);
                sInfo.Children.Add(sTopRow);

                sInfo.Children.Add(new TextBlock
                {
                    Text = string.Format(NexLocale.T("snap_info_format", "Creat la {0:dd.MM.yyyy HH:mm:ss} · {1} chei Registry salvate · Plan: {2}"), snap.Timestamp, snap.DwordsBackup.Count, snap.PowerPlan),
                    FontSize = 11,
                    Foreground = MutedBrush,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                Grid.SetColumn(sInfo, 0);
                sGrid.Children.Add(sInfo);

                var sActs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var curSnap = snap;
                var restBtn = MakeCardButton(NexLocale.T("snap_btn_restore", "Restaurează"), NexIcon.Revert, GreenBrush, () =>
                {
                    bool ok = NativeTuning.RestoreSnapshot(curSnap.Id);
                    if (ok)
                    {
                        AppendLog(NexLocale.Format("snap_log_restored_format", curSnap.Id, curSnap.Name), false);
                        ShowToast(NexLocale.T("snap_toast_single_restored_title", "Snapshot Restaurat"), string.Format(NexLocale.T("snap_toast_single_restored_msg", "Setările din {0} au fost aplicate cu succes!"), curSnap.Name), NexIcon.Check, GreenBrush);
                    }
                    else
                    {
                        ShowToast(NexLocale.T("snap_toast_restore_err_title", "Eroare Restaurare"), NexLocale.T("snap_toast_restore_err_msg", "Nu s-a putut restaura snapshot-ul selectat."), NexIcon.Warning, RedBrush);
                    }
                    ShowSnapshot();
                    return Task.CompletedTask;
                }, false, 110);
                sActs.Children.Add(restBtn);

                var delBtn = MakeCardButton(NexLocale.T("snap_btn_delete", "Șterge"), NexIcon.Trash, RedBrush, () =>
                {
                    NativeTuning.DeleteSnapshot(curSnap.Id);
                    ShowToast(NexLocale.T("snap_toast_deleted_title", "Snapshot Șters"), string.Format(NexLocale.T("snap_toast_deleted_msg", "Snapshot-ul {0} a fost eliminat."), curSnap.Id), NexIcon.Check, AmberBrush);
                    ShowSnapshot();
                    return Task.CompletedTask;
                }, false, 85);
                sActs.Children.Add(delBtn);

                Grid.SetColumn(sActs, 1);
                sGrid.Children.Add(sActs);

                sBorder.Child = sGrid;
                histStack.Children.Add(sBorder);
            }
        }

        histCard.Child = histStack;
        PageRoot.Children.Add(histCard);
    }

}