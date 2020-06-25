﻿using Playnite.SDK;
using SuccessStory.Models;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;


namespace SuccessStory
{
    public partial class SuccessStorySettingsView : UserControl
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        IPlayniteAPI PlayniteApi;
        SuccessStorySettings settings;

        string PluginUserDataPath;
        AchievementsDatabase AchievementsDatabase;

        int SteamTotal;
        int SteamTotalAchievements;
        int GogTotal;
        int GogTotalAchievements;
        int OriginTotal;
        int OriginTotalAchievements;

        int LocalTotal;
        int LocalTotalAchievements;


        public SuccessStorySettingsView(IPlayniteAPI PlayniteApi, string PluginUserDataPath, SuccessStorySettings settings)
        {
            this.PlayniteApi = PlayniteApi;
            this.PluginUserDataPath = PluginUserDataPath;
            this.settings = settings;

            AchievementsDatabase = new AchievementsDatabase(PlayniteApi, PluginUserDataPath);

            InitializeComponent();

            SetTotal();

            SuccessStoryLoad.Visibility = Visibility.Hidden;
        }

        internal void SetTotal()
        {
            SteamTotal = 0;
            SteamTotalAchievements = 0;
            GogTotal = 0;
            GogTotalAchievements = 0;
            OriginTotal = 0;
            OriginTotalAchievements = 0;

            LocalTotal = 0;
            LocalTotalAchievements = 0;

            foreach (var game in PlayniteApi.Database.Games)
            {
                string GameSourceName = "";
                if (game.SourceId != Guid.Parse("00000000-0000-0000-0000-000000000000"))
                {
                    GameSourceName = game.Source.Name;
                }
                else
                {
                    GameSourceName = "Playnite";
                }

                switch (GameSourceName.ToLower())
                {
                    case "steam":
                        SteamTotal += 1;
                        if (AchievementsDatabase.VerifAchievementsLoad(game.Id))
                        {
                            SteamTotalAchievements += 1;
                        }
                        break;
                    case "gog":
                        GogTotal += 1;
                        if (AchievementsDatabase.VerifAchievementsLoad(game.Id))
                        {
                            GogTotalAchievements += 1;
                        }
                        break;
                    case "origin":
                        OriginTotal += 1;
                        if (AchievementsDatabase.VerifAchievementsLoad(game.Id))
                        {
                            OriginTotalAchievements += 1;
                        }
                        break;
                    case "playnite":
                        LocalTotal += 1;
                        if (AchievementsDatabase.VerifAchievementsLoad(game.Id))
                        {
                            LocalTotalAchievements += 1;
                        }
                        break;
                }
            }
            SteamLoad.Content = SteamTotalAchievements + "/" + SteamTotal;
            GogLoad.Content = GogTotalAchievements + "/" + GogTotal;
            OriginLoad.Content = OriginTotalAchievements + "/" + OriginTotal;
            LocalLoad.Content = LocalTotalAchievements + "/" + LocalTotal;
        }

        private void Button_Click_Get_All(object sender, RoutedEventArgs e)
        {
            SteamLoad.Content = 0 + "/" + SteamTotal;
            GogLoad.Content = 0 + "/" + GogTotal;
            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("All", true);
            SetTotal();
        }
        private void Button_Click_All(object sender, RoutedEventArgs e)
        {
            SteamLoad.Content = 0 + "/" + SteamTotal;
            GogLoad.Content = 0 + "/" + GogTotal;
            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("All");
            SetTotal();
        }

        private void Button_Click_Get_Installed(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings);

            SteamLoad.Content = 0 + "/" + SteamTotal;
            GogLoad.Content = 0 + "/" + GogTotal;
            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("AllInstalled", true);
            SetTotal();
        }
        private void Button_Click_All_Installed(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings);

            SteamLoad.Content = 0 + "/" + SteamTotal;
            GogLoad.Content = 0 + "/" + GogTotal;
            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("AllInstalled");
            SetTotal();
        }

        private void Button_Click_Get_Recent(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings);

            SteamLoad.Content = 0 + "/" + SteamTotal;
            GogLoad.Content = 0 + "/" + GogTotal;
            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("AllRecent", true);
            SetTotal();
        }
        private void Button_Click_All_Recent(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings);

            SteamLoad.Content = 0 + "/" + SteamTotal;
            GogLoad.Content = 0 + "/" + GogTotal;
            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("AllRecent");
            SetTotal();
        }

        private void Button_Click_Get_Steam(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "Steam");

            SteamLoad.Content = 0 + "/" + SteamTotal;
            RefreshData("Steam", true);
            SetTotal();
        }
        private void Button_Click_Steam(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "Steam");

            SteamLoad.Content = 0 + "/" + SteamTotal;
            RefreshData("Steam");
            SetTotal();
        }

        private void Button_Click_Get_Gog(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "GOG");

            GogLoad.Content = 0 + "/" + GogTotal;
            RefreshData("GOG", true);
            SetTotal();
        }
        private void Button_Click_Gog(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "GOG");

            GogLoad.Content = 0 + "/" + GogTotal;
            RefreshData("GOG");
            SetTotal();
        }

        private void Button_Click_Get_Origin(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "Origin");

            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("Origin", true);
            SetTotal();
        }
        private void Button_Click_Origin(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "Origin");

            OriginLoad.Content = 0 + "/" + OriginTotal;
            RefreshData("Origin");
            SetTotal();
        }

        private void Button_Click_Get_Local(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "Playnite");

            LocalLoad.Content = 0 + "/" + LocalTotal;
            RefreshData("Playnite", true);
            SetTotal();
        }
        private void Button_Click_Local(object sender, RoutedEventArgs e)
        {
            AchievementsDatabase.InitializeMultipleAdd(settings, "Playnite");

            LocalLoad.Content = 0 + "/" + LocalTotal;
            RefreshData("Playnite");
            SetTotal();
        }

        internal void RefreshData(string SourceName, bool IsGet = false)
        {
            // ProgressBar
            SuccessStoryLoad.Visibility = Visibility.Visible;
            SuccessStoryLoad.Value = 0;
            SuccessStoryLoad.Maximum = PlayniteApi.Database.Games.Count;

            SuccessStorySettings.IsEnabled = false;


            foreach (var game in PlayniteApi.Database.Games)
            {
                string GameSourceName = "";
                if (game.SourceId != Guid.Parse("00000000-0000-0000-0000-000000000000"))
                {
                    GameSourceName = game.Source.Name;
                }
                else
                {
                    GameSourceName = "Playnite";
                }

                if (GameSourceName.ToLower() == SourceName.ToLower() || SourceName.ToLower() == "all" || SourceName.ToLower() == "allrecent" || SourceName.ToLower() == "allinstalled")
                {
                    bool isOK = true;
                    if (SourceName.ToLower() == "allrecent")
                    {
                        if ((game.LastActivity != null && game.LastActivity > DateTime.Now.AddMonths(-1)) || (game.Added != null && game.Added > DateTime.Now.AddMonths(-1)))
                        {
                            isOK = true;
                        }
                        else
                        {
                            isOK = false;
                        }
                    }
                    if (SourceName.ToLower() == "allinstalled")
                    {
                        if (game.IsInstalled)
                        {
                            isOK = true;
                        }
                        else
                        {
                            isOK = false;
                        }
                    }

                    if (isOK)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            // Prevent HTTP 429 with limit request per minutes.
                            Thread.Sleep(1000);

                            if (IsGet)
                            {
                                // Add only it's not loaded
                                if (!AchievementsDatabase.VerifAchievementsLoad(game.Id))
                                {
                                    AchievementsDatabase.Add(game, settings);
                                }
                            }
                            else
                            {
                                AchievementsDatabase.Remove(game);
                                AchievementsDatabase.Add(game, settings);
                            }
                        }), DispatcherPriority.ContextIdle, null);
                    }
                }
                SuccessStoryLoad.Value += 1;
            }


            if (AchievementsDatabase.ListErrors.Get() != "")
            {
                PlayniteApi.Dialogs.ShowErrorMessage(AchievementsDatabase.ListErrors.Get(), "SuccesStory errors");
            }
            else
            {
                PlayniteApi.Dialogs.ShowMessage((string)ResourceProvider.GetResource("LOCSucessStoryRefreshDataMessage"), "Success Story");
            }


            SuccessStoryLoad.Visibility = Visibility.Hidden;
            SuccessStorySettings.IsEnabled = true;
        }


        private void Checkbox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;

            if ((cb.Name == "Sc_IntegrationInDescription") && (bool)cb.IsChecked)
            {
                Sc_IntegrationInCustomTheme.IsChecked = false;
                Sc_IntegrationInDescriptionWithToggle.IsChecked = false;
                //Sc_IntegrationInButton.IsChecked = false;
                //Sc_IntegrationInButtonDetails.IsChecked = false;
            }
            if ((cb.Name == "Sc_IntegrationInDescriptionWithToggle") && (bool)cb.IsChecked)
            {
                Sc_IntegrationInCustomTheme.IsChecked = false;
                Sc_IntegrationInDescription.IsChecked = false;
                Sc_IntegrationInButton.IsChecked = false;
                Sc_IntegrationInButtonDetails.IsChecked = false;
            }


            if ((cb.Name == "Sc_IntegrationInButton") && (bool)cb.IsChecked)
            {
                Sc_IntegrationInCustomTheme.IsChecked = false;
                //Sc_IntegrationInDescription.IsChecked = false;
                Sc_IntegrationInDescriptionWithToggle.IsChecked = false;
                Sc_IntegrationInButtonDetails.IsChecked = false;
            }

            if ((cb.Name == "Sc_IntegrationInButtonDetails") && (bool)cb.IsChecked)
            {
                Sc_IntegrationInCustomTheme.IsChecked = false;
                //Sc_IntegrationInDescription.IsChecked = false;
                Sc_IntegrationInDescriptionWithToggle.IsChecked = false;
                Sc_IntegrationInButton.IsChecked = false;
            }

            if ((cb.Name == "Sc_IntegrationInCustomTheme") && (bool)cb.IsChecked)
            {
                Sc_IntegrationInDescription.IsChecked = false;
                Sc_IntegrationInDescriptionWithToggle.IsChecked = false;
                Sc_IntegrationInButton.IsChecked = false;
                Sc_IntegrationInButtonDetails.IsChecked = false;
            }
        }
    }
}