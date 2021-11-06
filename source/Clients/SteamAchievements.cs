﻿using Playnite.SDK;
using Playnite.SDK.Data;
using CommonPluginsShared;
using SuccessStory.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Playnite.SDK.Models;
using SteamKit2;
using System.Globalization;
using CommonPlayniteShared.Common.Web;
using System.Text.RegularExpressions;
using CommonPluginsStores;
using CommonPluginsShared.Models;
using PlayniteTools = CommonPluginsShared.PlayniteTools;
using CommonPluginsShared.Extensions;
using System.Threading;

namespace SuccessStory.Clients
{
    class SteamAchievements : GenericAchievements
    {
        protected static SteamApi _steamApi;
        internal static SteamApi steamApi
        {
            get
            {
                if (_steamApi == null)
                {
                    _steamApi = new SteamApi();
                }
                return _steamApi;
            }

            set
            {
                _steamApi = value;
            }
        }

        private IHtmlDocument HtmlDocument { get; set; } = null;

        private bool IsLocal { get; set; } = false;
        private bool IsManual { get; set; } = false;

        private string SteamId { get; set; } = string.Empty;
        private string SteamApiKey { get; set; } = string.Empty;
        private string SteamUser { get; set; } = string.Empty;

        private const string UrlProfilById = @"https://steamcommunity.com/profiles/{0}/stats/{1}?tab=achievements&l={2}";
        private const string UrlProfilByName = @"https://steamcommunity.com/id/{0}/stats/{1}?tab=achievements&l={2}";

        private const string UrlAchievements = @"https://steamcommunity.com/stats/{0}/achievements/?l={1}";

        private const string UrlSearch = @"https://store.steampowered.com/search/?term={0}";


        public SteamAchievements() : base("Steam", CodeLang.GetSteamLang(PluginDatabase.PlayniteApi.ApplicationSettings.Language))
        {

        }


        public override GameAchievements GetAchievements(Game game)
        {
            GameAchievements gameAchievements = SuccessStory.PluginDatabase.GetDefault(game);
            List<Achievements> AllAchievements = new List<Achievements>();
            List<GameStats> AllStats = new List<GameStats>();

            bool GetByWeb = false;
            int AppId = 0;


            // Get Steam configuration if exist.
            if (!IsConfigured())
            {
                return gameAchievements;
            }


            if (!IsLocal)
            {
                int.TryParse(game.GameId, out AppId);

                if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi || PluginDatabase.PluginSettings.Settings.SteamIsPrivate)
                {
                    AllAchievements = GetAchievementsByWeb(AppId);
                    GetByWeb = true;
                }
                else
                {
                    VerifSteamUser();
                    if (SteamUser.IsNullOrEmpty())
                    {
                        logger.Warn("No Steam user");
                    }

                    AllAchievements = GetPlayerAchievements(AppId);
                    AllStats = GetUsersStats(AppId);
                }

                if (AllAchievements.Count > 0)
                {
                    var DataCompleted = GetSchemaForGame(AppId, AllAchievements, AllStats);

                    bool IsOK = GetByWeb ? GetByWeb : Web.DownloadFileImageTest(AllAchievements[0].UrlLocked).GetAwaiter().GetResult();
                    if (IsOK)
                    {
                        AllAchievements = DataCompleted.Item1;
                        AllStats = DataCompleted.Item2;


                        gameAchievements.Items = AllAchievements;
                        gameAchievements.ItemsStats = AllStats;


                        // Set source link
                        if (gameAchievements.HasAchivements)
                        {
                            gameAchievements.SourcesLink = new SourceLink
                            {
                                GameName = steamApi.GetGameName(AppId),
                                Name = "Steam",
                                Url = string.Format(UrlProfilById, SteamId, AppId, LocalLang)
                            };
                        }
                    }
                }
            }
            else
            {
                Common.LogDebug(true, $"GetAchievementsLocal()");

                if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
                {
                    logger.Warn($"Option without API key is enbaled");
                }
                else if (SteamApiKey.IsNullOrEmpty())
                {
                    logger.Warn($"No Steam API key");
                }
                else
                {
                    SteamEmulators se = new SteamEmulators(PluginDatabase.PluginSettings.Settings.LocalPath);
                    var temp = se.GetAchievementsLocal(game, SteamApiKey, 0, IsManual);
                    AppId = se.GetSteamId();

                    if (temp.Items.Count > 0)
                    {
                        for (int i = 0; i < temp.Items.Count; i++)
                        {
                            AllAchievements.Add(new Achievements
                            {
                                Name = temp.Items[i].Name,
                                ApiName = temp.Items[i].ApiName,
                                Description = temp.Items[i].Description,
                                UrlUnlocked = temp.Items[i].UrlUnlocked,
                                UrlLocked = temp.Items[i].UrlLocked,
                                DateUnlocked = temp.Items[i].DateUnlocked
                            });
                        }


                        gameAchievements.Items = AllAchievements;


                        // Set source link
                        if (gameAchievements.HasAchivements)
                        {
                            gameAchievements.SourcesLink = new SourceLink
                            {
                                GameName = steamApi.GetGameName(AppId),
                                Name = "Steam",
                                Url = $"https://steamcommunity.com/stats/{AppId}/achievements"
                            };
                        }
                    }
                }
            }


            // Set rarity
            if (gameAchievements.HasAchivements)
            {
                if (!IsLocal && (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi || PluginDatabase.PluginSettings.Settings.SteamIsPrivate))
                {
                    try
                    {
                        gameAchievements.Items = GetGlobalAchievementPercentagesForAppByWeb(AppId, gameAchievements.Items);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }
                else
                {
                    try
                    {
                        gameAchievements.Items = GetGlobalAchievementPercentagesForApp(AppId, gameAchievements.Items);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }
            }

            return gameAchievements;
        }

        public GameAchievements GetAchievements(Game game, int AppId)
        {
            GameAchievements gameAchievements = SuccessStory.PluginDatabase.GetDefault(game);
            List<Achievements> AllAchievements = new List<Achievements>();


            // Get Steam configuration if exist.
            if (!IsConfigured())
            {
                return gameAchievements;
            }


            if (IsLocal)
            {
                Common.LogDebug(true, $"GetAchievementsLocal()");

                if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
                {
                    logger.Warn($"Option without API key is enbaled");
                }
                else if (SteamApiKey.IsNullOrEmpty())
                {
                    logger.Warn($"No Steam API key");
                }
                else
                {
                    SteamEmulators se = new SteamEmulators(PluginDatabase.PluginSettings.Settings.LocalPath);
                    var temp = se.GetAchievementsLocal(game, SteamApiKey, AppId, IsManual);

                    if (temp.Items.Count > 0)
                    {
                        for (int i = 0; i < temp.Items.Count; i++)
                        {
                            AllAchievements.Add(new Achievements
                            {
                                Name = temp.Items[i].Name,
                                ApiName = temp.Items[i].ApiName,
                                Description = temp.Items[i].Description,
                                UrlUnlocked = temp.Items[i].UrlUnlocked,
                                UrlLocked = temp.Items[i].UrlLocked,
                                DateUnlocked = temp.Items[i].DateUnlocked
                            });
                        }


                        gameAchievements.Items = AllAchievements;


                        // Set source link
                        gameAchievements.SourcesLink = new SourceLink
                        {
                            GameName = steamApi.GetGameName(AppId),
                            Name = "Steam",
                            Url = $"https://steamcommunity.com/stats/{AppId}/achievements"
                        };
                    }
                }
            }


            if (gameAchievements.Items.Count > 0)
            {
                gameAchievements.Items = GetGlobalAchievementPercentagesForApp(AppId, gameAchievements.Items);
            }

            return gameAchievements;
        }



        #region Configuration
        // TODO Rewrite
        public override bool ValidateConfiguration()
        {
            if (PlayniteTools.IsDisabledPlaynitePlugins("SteamLibrary"))
            {
                ShowNotificationPluginDisable(resources.GetString("LOCSuccessStoryNotificationsSteamDisabled"));
                return false;
            }
            else
            {
                if (CachedConfigurationValidationResult == null)
                {
                    if (!IsConfigured())
                    {
                        ShowNotificationPluginNoConfiguration(resources.GetString("LOCSuccessStoryNotificationsSteamBadConfig"));
                        CachedConfigurationValidationResult = false;
                    }

                    if (!PluginDatabase.PluginSettings.Settings.SteamIsPrivate && !CheckIsPublic())
                    {
                        ShowNotificationPluginNoPublic(resources.GetString("LOCSuccessStoryNotificationsSteamPrivate"));
                        CachedConfigurationValidationResult = false;
                    }

                    if (PluginDatabase.PluginSettings.Settings.SteamIsPrivate && !IsConnected())
                    {
                        Thread.Sleep(2000);
                        if (PluginDatabase.PluginSettings.Settings.SteamIsPrivate && !IsConnected())
                        {
                            ShowNotificationPluginNoPublic(resources.GetString("LOCSuccessStoryNotificationsSteamNoAuthenticate"));
                            CachedConfigurationValidationResult = false;
                        }
                    }


                    
                    if (CachedConfigurationValidationResult == null)
                    {
                        CachedConfigurationValidationResult = true;
                    }

                    if (!(bool)CachedConfigurationValidationResult)
                    {
                        ShowNotificationPluginErrorMessage();
                    }
                }
                else if (!(bool)CachedConfigurationValidationResult)
                {
                    ShowNotificationPluginErrorMessage();
                }

                return (bool)CachedConfigurationValidationResult;
            }
        }

        public override bool IsConnected()
        {
            if (IsConfigured())
            {
                string ProfileById = $"https://steamcommunity.com/profiles/{SteamId}";
                string ProfileByName = $"https://steamcommunity.com/id/{SteamUser}";

                return IsProfileConnected(ProfileById) || IsProfileConnected(ProfileByName);
            }

            return false;
        }

        public override bool IsConfigured()
        {
            try
            {
                if (File.Exists(PluginDatabase.Paths.PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json"))
                {
                    dynamic SteamConfig = Serialization.FromJsonFile<dynamic>(PluginDatabase.Paths.PluginUserDataPath + "\\..\\CB91DFC9-B977-43BF-8E70-55F46E410FAB\\config.json");
                    SteamId = (string)SteamConfig["UserId"];
                    SteamApiKey = (string)SteamConfig["ApiKey"];
                    SteamUser = (string)SteamConfig["UserName"];
                }
                else
                {
                    ShowNotificationPluginNoConfiguration(resources.GetString("LOCSuccessStoryNotificationsSteamBadConfig1"));
                    return false;
                }
            }
            catch (Exception ex)
            {
                ShowNotificationPluginError(ex);
                return false;
            }


            if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
            {
                if (SteamUser.IsNullOrEmpty())
                {
                    ShowNotificationPluginNoConfiguration(resources.GetString("Error on SteamAchievements: no Steam user in settings menu for Steam Library."));
                    return false;
                }
            }
            else
            {
                if (SteamId.IsNullOrEmpty() || SteamApiKey.IsNullOrEmpty())
                {
                    ShowNotificationPluginNoConfiguration(resources.GetString("LOCSuccessStoryNotificationsSteamBadConfig1"));
                    return false;
                }
            }

            return true;
        }

        public override bool EnabledInSettings()
        {
            if (IsLocal)
            {
                return PluginDatabase.PluginSettings.Settings.EnableLocal;
            }
            else
            {
                return PluginDatabase.PluginSettings.Settings.EnableSteam;
            }
        }
        #endregion


        public void SetLocal()
        {
            IsLocal = true;
        }

        public void SetManual()
        {
            IsManual = true;
        }


        #region Steam
        public List<SearchResult> SearchGame(string Name)
        {
            List<SearchResult> ListSearchGames = new List<SearchResult>();

            try
            {
                var DataSteamSearch = Web.DownloadStringData(string.Format(UrlSearch, WebUtility.UrlEncode(Name))).GetAwaiter().GetResult();

                HtmlParser parser = new HtmlParser();
                IHtmlDocument htmlDocument = parser.Parse(DataSteamSearch);

                int index = 0;
                foreach (var gameElem in htmlDocument.QuerySelectorAll(".search_result_row"))
                {
                    if (index == 10)
                    {
                        break;
                    }

                    var url = gameElem.GetAttribute("href");
                    var title = gameElem.QuerySelector(".title").InnerHtml;
                    var img = gameElem.QuerySelector(".search_capsule img").GetAttribute("src");
                    var releaseDate = gameElem.QuerySelector(".search_released").InnerHtml;
                    if (gameElem.HasAttribute("data-ds-packageid"))
                    {
                        continue;
                    }

                    int.TryParse(gameElem.GetAttribute("data-ds-appid"), out int gameId);

                    int AchievementsCount = 0;
                    if (!PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi & IsConfigured())
                    {
                        if (gameId != 0)
                        {
                            using (dynamic steamWebAPI = WebAPI.GetInterface("ISteamUserStats", SteamApiKey))
                            {
                                KeyValue SchemaForGame = steamWebAPI.GetSchemaForGame(appid: gameId, l: LocalLang);
                                AchievementsCount = SchemaForGame.Children?.Find(x => x.Name == "availableGameStats")?.Children?.Find(x => x.Name == "achievements")?.Children?.Count ?? 0;
                            }
                        }
                    }
                    else
                    {
                        DataSteamSearch = Web.DownloadStringData(string.Format(url, WebUtility.UrlEncode(Name))).GetAwaiter().GetResult();
                        IHtmlDocument htmlDocumentDetails = parser.Parse(DataSteamSearch);

                        var AchievementsInfo = htmlDocumentDetails.QuerySelector("#achievement_block .block_title");
                        if (AchievementsInfo != null)
                        {
                            int.TryParse(Regex.Replace(AchievementsInfo.InnerHtml, "[^0-9]", ""), out AchievementsCount);
                        }
                    }

                    if (gameId != 0)
                    {
                        ListSearchGames.Add(new SearchResult
                        {
                            Name = WebUtility.HtmlDecode(title),
                            Url = url,
                            UrlImage = img,
                            AppId = gameId,
                            AchievementsCount = AchievementsCount
                        });
                    }

                    index++;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return ListSearchGames;
        }


        private void VerifSteamUser()
        {
            if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
            {
                return;
            }

            if (SteamApiKey.IsNullOrEmpty())
            {
                logger.Warn($"No Steam API key");
                return;
            }

            try
            {
                using (dynamic steamWebAPI = WebAPI.GetInterface("ISteamUser", SteamApiKey))
                {
                    KeyValue PlayerSummaries = steamWebAPI.GetPlayerSummaries(steamids: SteamId);
                    string personaname = (string)PlayerSummaries["players"]["player"].Children[0].Children.Find(x => x.Name == "personaname").Value;

                    if (personaname != SteamUser)
                    {
                        logger.Warn($"Steam user is different {SteamUser} != {personaname}");
                        SteamUser = personaname;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error on VerifSteamUser()");
            }
        }


        public bool CheckIsPublic()
        {
            if (IsConfigured())
            {
                string ProfileById = $"https://steamcommunity.com/profiles/{SteamId}";
                string ProfileByName = $"https://steamcommunity.com/id/{SteamUser}";

                return IsProfilePublic(ProfileById) || IsProfilePublic(ProfileByName);
            }

            return false;
        }

        private static bool IsProfilePublic(string profilePageUrl)
        {
            try
            {
                string ResultWeb = HttpDownloader.DownloadString(profilePageUrl);
                IHtmlDocument HtmlDoc = new HtmlParser().Parse(ResultWeb);

                //this finds the Games link on the right side of the profile page. If that's public then so are achievements.
                var gamesPageLink = HtmlDoc.QuerySelector(@".profile_item_links a[href$=""/games/?tab=all""]");
                return gamesPageLink != null;
            }
            catch (WebException ex)
            {
                Common.LogError(ex, false);
                return false;
            }
        }

        private static bool IsProfileConnected(string profilePageUrl)
        {
            try
            {
                WebViewOffscreen.NavigateAndWait(profilePageUrl);
                IHtmlDocument HtmlDoc = new HtmlParser().Parse(WebViewOffscreen.GetPageSource());

                //this finds the Games link on the right side of the profile page. If that's public then so are achievements.
                var gamesPageLink = HtmlDoc.QuerySelector(@".profile_item_links a[href$=""/games/?tab=all""]");
                return gamesPageLink != null;
            }
            catch (WebException ex)
            {
                Common.LogError(ex, false);
                return false;
            }
        }


        private List<GameStats> GetUsersStats(int AppId)
        {
            List<GameStats> AllStats = new List<GameStats>();

            if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
            {
                return AllStats;
            }

            if (SteamApiKey.IsNullOrEmpty())
            {
                logger.Warn($"No Steam API key");
                return AllStats;
            }

            try
            {
                using (dynamic steamWebAPI = WebAPI.GetInterface("ISteamUserStats", SteamApiKey))
                {
                    KeyValue UserStats = steamWebAPI.GetUserStatsForGame(steamid: SteamId, appid: AppId, l: LocalLang);

                    if (UserStats != null && UserStats.Children != null)
                    {
                        var UserStatsData = UserStats.Children.Find(x => x.Name == "stats");
                        if (UserStatsData != null)
                        {
                            foreach (KeyValue StatsData in UserStatsData.Children)
                            {
                                double.TryParse(StatsData.Children.First().Value.Replace(".", CultureInfo.CurrentUICulture.NumberFormat.NumberDecimalSeparator), out double ValueStats);

                                AllStats.Add(new GameStats
                                {
                                    Name = StatsData.Name,
                                    DisplayName = string.Empty,
                                    Value = ValueStats
                                });
                            }
                        }
                    }
                }
            }
            // TODO With recent SteamKit
            //catch (WebAPIRequestException wex)
            //{
            //    if (wex.StatusCode == HttpStatusCode.Forbidden)
            //    {
            //        _PlayniteApi.Notifications.Add(new NotificationMessage(
            //            $"SuccessStory-Steam-PrivateProfil",
            //            "SuccessStory - Steam profil is private",
            //            NotificationType.Error
            //        ));
            //        logger.Warn("Steam profil is private");
            //    }
            //    else
            //    {
            //        Common.LogError(wex, false, $"Error on GetUsersStats({SteamId}, {AppId}, {LocalLang})");
            //    }
            //}
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    if (ex.Response is HttpWebResponse response)
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            PluginDatabase.PlayniteApi.Notifications.Add(new NotificationMessage(
                                "SuccessStory-Steam-PrivateProfil",
                                $"SuccessStory\r\n{resources.GetString("LOCSuccessStoryNotificationsSteamPrivate")}",
                                NotificationType.Error,
                                () => Process.Start(@"https://steamcommunity.com/my/edit/settings")
                            ));
                            logger.Warn("Steam profil is private");

                            // TODO https://github.com/Lacro59/playnite-successstory-plugin/issues/76
                            Common.LogError(ex, false, $"Error on GetUsersStats({SteamId}, {AppId}, {LocalLang})");
                        }
                    }
                    else
                    {
                        // no http status code available
                        Common.LogError(ex, false, $"Error on GetUsersStats({SteamId}, {AppId}, {LocalLang})");
                    }
                }
                else
                {
                    // no http status code available
                    Common.LogError(ex, false, $"Error on GetUsersStats({SteamId}, {AppId}, {LocalLang})");
                }
            }

            return AllStats;
        }

        private List<Achievements> GetPlayerAchievements(int AppId)
        {
            List<Achievements> AllAchievements = new List<Achievements>();

            if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
            {
                return AllAchievements;
            }

            if (SteamApiKey.IsNullOrEmpty())
            {
                logger.Warn($"No Steam API key");
                return AllAchievements;
            }

            try
            {
                using (dynamic steamWebAPI = WebAPI.GetInterface("ISteamUserStats", SteamApiKey))
                {
                    KeyValue PlayerAchievements = steamWebAPI.GetPlayerAchievements(steamid: SteamId, appid: AppId, l: LocalLang);

                    if (PlayerAchievements != null && PlayerAchievements.Children != null)
                    {
                        var PlayerAchievementsData = PlayerAchievements.Children.Find(x => x.Name == "achievements");
                        if (PlayerAchievementsData != null)
                        {
                            foreach (KeyValue AchievementsData in PlayerAchievementsData.Children)
                            {
                                int.TryParse(AchievementsData.Children.Find(x => x.Name == "unlocktime").Value, out int unlocktime);
                                bool achieved = int.Parse(AchievementsData.Children.Find(x => x.Name == "achieved").Value) == 1;

                                AllAchievements.Add(new Achievements
                                {
                                    ApiName = AchievementsData.Children.Find(x => x.Name == "apiname").Value,
                                    Name = AchievementsData.Children.Find(x => x.Name == "name").Value,
                                    Description = AchievementsData.Children.Find(x => x.Name == "description").Value,
                                    DateUnlocked = achieved ? new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(unlocktime) : default(DateTime)
                                });
                            }
                        }
                    }
                }
            }
            // TODO With recent SteamKit
            //catch (WebAPIRequestException wex)
            //{
            //    if (wex.StatusCode == HttpStatusCode.Forbidden)
            //    {
            //        _PlayniteApi.Notifications.Add(new NotificationMessage(
            //            $"SuccessStory-Steam-PrivateProfil",
            //            "SuccessStory - Steam profil is private",
            //            NotificationType.Error
            //        ));
            //        logger.Warn("Steam profil is private");
            //    }
            //    else
            //    {
            //        Common.LogError(wex, false, $"Error on GetPlayerAchievements({SteamId}, {AppId}, {LocalLang})");
            //    }
            //}
            catch (WebException ex)
            {
                if (ex != null && ex.Status == WebExceptionStatus.ProtocolError)
                {
                    if (ex.Response is HttpWebResponse response)
                    {
                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            PluginDatabase.PlayniteApi.Notifications.Add(new NotificationMessage(
                                "SuccessStory-Steam-PrivateProfil",
                                $"SuccessStory\r\n{resources.GetString("LOCSuccessStoryNotificationsSteamPrivate")}",
                                NotificationType.Error,
                                () => Process.Start(@"https://steamcommunity.com/my/edit/settings")
                            ));
                            logger.Warn("Steam profil is private");

                            // TODO https://github.com/Lacro59/playnite-successstory-plugin/issues/76
                            Common.LogError(ex, false, $"Error on GetPlayerAchievements({SteamId}, {AppId}, {LocalLang})");
                        }
                    }
                    else
                    {
                        // no http status code available
                        Common.LogError(ex, false, $"Error on GetPlayerAchievements({SteamId}, {AppId}, {LocalLang})");
                    }
                }
                else
                {
                    // no http status code available
                    Common.LogError(ex, false, $"Error on GetPlayerAchievements({SteamId}, {AppId}, {LocalLang})");
                }
            }

            return AllAchievements;
        }

        private Tuple<List<Achievements>, List<GameStats>> GetSchemaForGame(int AppId, List<Achievements> AllAchievements, List<GameStats> AllStats)
        {
            try
            {
                if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
                {
                    return Tuple.Create(AllAchievements, AllStats);
                }

                if (SteamApiKey.IsNullOrEmpty())
                {
                    logger.Warn($"No Steam API key");
                    return Tuple.Create(AllAchievements, AllStats);
                }

                using (dynamic steamWebAPI = WebAPI.GetInterface("ISteamUserStats", SteamApiKey))
                {
                    KeyValue SchemaForGame = steamWebAPI.GetSchemaForGame(appid: AppId, l: LocalLang);

                    try
                    {
                        foreach (KeyValue AchievementsData in SchemaForGame.Children?.Find(x => x.Name == "availableGameStats").Children?.Find(x => x.Name == "achievements").Children)
                        {
                            var Achievement = AllAchievements.Find(x => x.ApiName.IsEqual(AchievementsData.Name));

                            Achievement.IsHidden = AchievementsData.Children?.Find(x => x.Name.IsEqual("hidden")).Value == "1";
                            Achievement.UrlUnlocked = AchievementsData.Children?.Find(x => x.Name.IsEqual("icon")).Value;
                            Achievement.UrlLocked = AchievementsData.Children?.Find(x => x.Name.IsEqual("icongray")).Value;

                            if (Achievement.IsHidden)
                            {
                                Achievement.Description = FindHiddenDescription(AppId, Achievement.Name);
                            }

                            Tuple<int, int> Progress = FindProgress(AppId, Achievement.Name);
                            Achievement.ProgressCurrent = Progress.Item1;
                            Achievement.ProgressMax = Progress.Item2;
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, $"Error on AchievementsData({AppId}, {LocalLang})");
                    }

                    try
                    {
                        var availableGameStats = SchemaForGame.Children.Find(x => x.Name.IsEqual("availableGameStats"));

                        if (availableGameStats != null)
                        {
                            var stats = availableGameStats.Children.Find(x => x.Name.IsEqual("stats"));

                            if (stats != null)
                            {
                                var ListStatsData = stats.Children;
                                foreach (KeyValue StatsData in ListStatsData)
                                {
                                    if (AllStats.Find(x => x.Name.IsEqual(StatsData.Name)) == null)
                                    {
                                        double.TryParse(StatsData.Children.Find(x => x.Name.IsEqual("defaultvalue")).Value, out double ValueStats);

                                        AllStats.Add(new GameStats
                                        {
                                            Name = StatsData.Name,
                                            DisplayName = StatsData.Children.Find(x => x.Name.IsEqual("displayName")).Value,
                                            Value = ValueStats
                                        });
                                    }
                                    else
                                    {
                                        AllStats.Find(x => x.Name.IsEqual(StatsData.Name)).DisplayName = StatsData.Children.Find(x => x.Name.IsEqual("displayName")).Value;
                                    }
                                }
                            }
                            else
                            {
                                logger.Info($"No Steam stats for {AppId}");
                            }
                        }
                        else
                        {
                            logger.Info($"No Steam stats for {AppId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, $"Error on AvailableGameStats({AppId}, {LocalLang})");
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on GetSchemaForGame({AppId}, {LocalLang})");
            }

            return Tuple.Create(AllAchievements, AllStats);
        }


        // TODO Use "profileurl" in "ISteamUser"
        // TODO Utility after updated GetAchievementsByWeb() 
        private string FindHiddenDescription(int AppId, string DisplayName, bool TryByName = false)
        {
            string url = string.Empty;
            string ResultWeb = string.Empty;
            bool noData = true;

            // Get data
            if (HtmlDocument == null)
            {
                if (!TryByName)
                {
                    Common.LogDebug(true, $"FindHiddenDescription() for {SteamId} - {AppId}");

                    url = string.Format(UrlProfilById, SteamId, AppId, LocalLang);
                    try
                    {
                        WebViewOffscreen.NavigateAndWait(url);
                        ResultWeb = WebViewOffscreen.GetPageSource();
                    }
                    catch (WebException ex)
                    {
                        Common.LogError(ex, false, $"Error on FindHiddenDescription()");
                    }
                }
                else
                {
                    Common.LogDebug(true, $"FindHiddenDescription() for {SteamUser} - {AppId}");

                    url = string.Format(UrlProfilByName, SteamUser, AppId, LocalLang);
                    try
                    {
                        WebViewOffscreen.NavigateAndWait(url);
                        ResultWeb = WebViewOffscreen.GetPageSource();
                    }
                    catch (WebException ex)
                    {
                        Common.LogError(ex, false, $"Error on FindHiddenDescription()");
                    }
                }

                if (!ResultWeb.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    HtmlDocument = parser.Parse(ResultWeb);

                    if (HtmlDocument.QuerySelectorAll("div.achieveRow").Length != 0)
                    {
                        noData = false;
                    }
                }

                if (!TryByName && noData)
                {
                    HtmlDocument = null;
                    return FindHiddenDescription(AppId, DisplayName, TryByName = true);
                }
                else if (noData)
                {
                    return string.Empty;
                }
            }

            // Find the achievement description
            if (HtmlDocument != null)
            {
                foreach (var achieveRow in HtmlDocument.QuerySelectorAll("div.achieveRow"))
                {
                    try
                    {
                        if (achieveRow.QuerySelector("h3").InnerHtml.IsEqual(DisplayName))
                        {
                            string TempDescription = achieveRow.QuerySelector("h5").InnerHtml;

                            if (TempDescription.Contains("steamdb_achievement_spoiler"))
                            {
                                TempDescription = achieveRow.QuerySelector("h5 span").InnerHtml;
                                return WebUtility.HtmlDecode(TempDescription.Trim());
                            }
                            else
                            {
                                return WebUtility.HtmlDecode(TempDescription.Trim());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }
            }

            return string.Empty;
        }
        private Tuple<int, int> FindProgress(int AppId, string DisplayName, bool TryByName = false)
        {
            string url = string.Empty;
            string ResultWeb = string.Empty;
            bool noData = true;

            // Get data
            if (HtmlDocument == null)
            {
                if (!TryByName)
                {
                    Common.LogDebug(true, $"FindProgress() for {SteamId} - {AppId}");

                    url = string.Format(UrlProfilById, SteamId, AppId, LocalLang);
                    try
                    {
                        WebViewOffscreen.NavigateAndWait(url);
                        ResultWeb = WebViewOffscreen.GetPageSource();
                    }
                    catch (WebException ex)
                    {
                        Common.LogError(ex, false, $"Error on FindProgress()");
                    }
                }
                else
                {
                    Common.LogDebug(true, $"FindProgress() for {SteamUser} - {AppId}");

                    url = string.Format(UrlProfilByName, SteamUser, AppId, LocalLang);
                    try
                    {
                        WebViewOffscreen.NavigateAndWait(url);
                        ResultWeb = WebViewOffscreen.GetPageSource();
                    }
                    catch (WebException ex)
                    {
                        Common.LogError(ex, false, $"Error on FindProgress()");
                    }
                }

                if (!ResultWeb.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    HtmlDocument = parser.Parse(ResultWeb);

                    if (HtmlDocument.QuerySelectorAll("div.achieveRow").Length != 0)
                    {
                        noData = false;
                    }
                }

                if (!TryByName && noData)
                {
                    HtmlDocument = null;
                    return FindProgress(AppId, DisplayName, TryByName = true);
                }
                else if (noData)
                {
                    return Tuple.Create(0, 0);
                }
            }

            // Find the achievement progress
            if (HtmlDocument != null)
            {
                foreach (var achieveRow in HtmlDocument.QuerySelectorAll("div.achieveRow"))
                {
                    try
                    {
                        if (achieveRow.QuerySelector("h3").InnerHtml.IsEqual(DisplayName))
                        {
                            string Progress = achieveRow.QuerySelector(".progressText").InnerHtml.Trim();
                            Match ProgressMatch = Regex.Match(Progress, @"(\d+)\s*/\s*(\d+)");

                            int.TryParse(ProgressMatch.Groups[1].Value, out int ProgressCurrent);
                            int.TryParse(ProgressMatch.Groups[2].Value, out int ProgressMax);

                            return Tuple.Create(ProgressCurrent, ProgressMax);
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }
            }

            return Tuple.Create(0, 0);
        }

        public List<Achievements> GetGlobalAchievementPercentagesForApp(int AppId, List<Achievements> AllAchievements)
        {
            if (PluginDatabase.PluginSettings.Settings.EnableSteamWithoutWebApi)
            {
                return AllAchievements;
            }

            if (SteamApiKey.IsNullOrEmpty())
            {
                logger.Warn($"No Steam API key");
                return AllAchievements;
            }

            try
            {
                using (dynamic steamWebAPI = WebAPI.GetInterface("ISteamUserStats", SteamApiKey))
                {
                    KeyValue GlobalAchievementPercentagesForApp = steamWebAPI.GetGlobalAchievementPercentagesForApp(gameid: AppId);
                    foreach (KeyValue AchievementPercentagesData in GlobalAchievementPercentagesForApp["achievements"]["achievement"].Children)
                    {
                        string ApiName = AchievementPercentagesData.Children.Find(x => x.Name == "name")?.Value;
                        float.TryParse(AchievementPercentagesData.Children.Find(x => x.Name == "percent")?.Value?.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), out float Percent);

                        Common.LogDebug(true, $"{AppId} - ApiName: {ApiName} - Percent: {Percent}");

                        if (AllAchievements.Find(x => x.ApiName == ApiName) != null)
                        {
                            AllAchievements.Find(x => x.ApiName == ApiName).Percent = Percent;
                        }
                        else
                        {
                            logger.Warn($"not find for {AppId} - ApiName: {ApiName} - Percent: {Percent}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on GetGlobalAchievementPercentagesForApp({SteamId}, {AppId}, {LocalLang})");
            }

            return AllAchievements;
        }

        private List<Achievements> GetGlobalAchievementPercentagesForAppByWeb(int AppId, List<Achievements> AllAchievements)
        {
            string url = string.Empty;
            string ResultWeb = string.Empty;
            bool noData = true;
            HtmlDocument = null;

            // Get data
            if (HtmlDocument == null)
            {
                Common.LogDebug(true, $"GetGlobalAchievementPercentagesForAppByWeb() for {SteamId} - {AppId}");

                url = string.Format(UrlAchievements, AppId, LocalLang);
                try
                {
                    ResultWeb = HttpDownloader.DownloadString(url, Encoding.UTF8);
                }
                catch (WebException ex)
                {
                    Common.LogError(ex, false);
                }

                if (!ResultWeb.IsNullOrEmpty())
                {
                    HtmlParser parser = new HtmlParser();
                    HtmlDocument = parser.Parse(ResultWeb);

                    if (HtmlDocument.QuerySelectorAll("div.achieveRow").Length != 0)
                    {
                        noData = false;
                    }
                }

                if (noData)
                {
                    return AllAchievements;
                }
            }


            // Find the achievement description
            if (HtmlDocument != null)
            {
                foreach (var achieveRow in HtmlDocument.QuerySelectorAll("div.achieveRow"))
                {
                    try
                    {
                        string Name = string.Empty;
                        if (achieveRow.QuerySelector("h3") != null)
                        {
                            Name = WebUtility.HtmlDecode(achieveRow.QuerySelector("h3").InnerHtml.Trim());
                        }

                        float Percent = 0;
                        if (achieveRow.QuerySelector(".achievePercent") != null)
                        {
                            Percent = float.Parse(achieveRow.QuerySelector(".achievePercent").InnerHtml.Replace("%", string.Empty).Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator));
                        }

                        AllAchievements.Find(x => x.Name.IsEqual(Name)).Percent = Percent;
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false);
                    }
                }
            }

            return AllAchievements;
        }


        private List<Achievements> GetAchievementsByWeb(int AppId)
        {
            List<Achievements> achievements = new List<Achievements>();
            string url = string.Empty;

            // Get data
            url = string.Format(UrlProfilById, SteamId, AppId, LocalLang);
            achievements = GetAchievementsByWeb(achievements, url);

            if (achievements.Count == 0)
            {
                url = string.Format(UrlProfilByName, SteamUser, AppId, LocalLang);
                achievements = GetAchievementsByWeb(achievements, url);
            }

            return achievements;
        }

        private List<Achievements> GetAchievementsByWeb(List<Achievements> Achievements, string Url)
        {
            string ResultWeb = string.Empty;

            try
            {
                Url = Url + "&panorama=please";
                WebViewOffscreen.NavigateAndWait(Url);
                ResultWeb = WebViewOffscreen.GetPageSource();


                string CurrentUrl = WebViewOffscreen.GetCurrentAddress();
                if (CurrentUrl != Url)
                {
                    var urlParams = Url.Split('?').ToList();
                    if (urlParams.Count == 2)
                    {
                        Url = CurrentUrl + "?" + urlParams[1];
                    }

                    WebViewOffscreen.NavigateAndWait(Url);
                    ResultWeb = WebViewOffscreen.GetPageSource();
                }


                int index = ResultWeb.IndexOf("var g_rgAchievements = ");
                if (index > -1)
                {
                    ResultWeb = ResultWeb.Substring(index + "var g_rgAchievements = ".Length);

                    index = ResultWeb.IndexOf("var g_rgLeaderboards");
                    ResultWeb = ResultWeb.Substring(0, index).Trim();

                    ResultWeb = ResultWeb.Substring(0, ResultWeb.Length - 1).Trim();

                    dynamic data = Serialization.FromJson<dynamic>(ResultWeb);

                    dynamic OpenData = data["open"];
                    foreach (dynamic dd in OpenData)
                    {
                        string stringData = Serialization.ToJson(dd.Value);
                        SteamAchievementData steamAchievementData = Serialization.FromJson<SteamAchievementData>(stringData);
                        Achievements.Add(new Achievements
                        {
                            Name = WebUtility.HtmlDecode(steamAchievementData.Name.Trim()),
                            ApiName = steamAchievementData.RawName.Trim(),
                            Description = WebUtility.HtmlDecode(steamAchievementData.Desc.Trim()),
                            UrlUnlocked = steamAchievementData.IconClosed.Trim(),
                            UrlLocked = steamAchievementData.IconClosed.Trim(),
                            DateUnlocked = default(DateTime),
                            IsHidden = steamAchievementData.Hidden,
                            Percent = 100
                        });
                    }

                    dynamic ClosedData = data["closed"];
                    foreach (dynamic dd in ClosedData)
                    {
                        string stringData = Serialization.ToJson(dd.Value);
                        SteamAchievementData steamAchievementData = Serialization.FromJson<SteamAchievementData>(stringData);
                        Achievements.Add(new Achievements
                        {
                            Name = WebUtility.HtmlDecode(steamAchievementData.Name.Trim()),
                            ApiName = steamAchievementData.RawName.Trim(),
                            Description = WebUtility.HtmlDecode(steamAchievementData.Desc.Trim()),
                            UrlUnlocked = steamAchievementData.IconClosed.Trim(),
                            UrlLocked = steamAchievementData.IconClosed.Trim(),
                            DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(steamAchievementData.UnlockTime),
                            IsHidden = steamAchievementData.Hidden,
                            Percent = 100
                        });
                    }
                }
                else
                {
                    Common.LogDebug(true, $"No achievement data on {Url}");
                }
            }
            catch (WebException ex)
            {
                Common.LogError(ex, false);
            }

            return Achievements;
        }


        public static SteamAchievements GetLocalSteamAchievementsProvider()
        {
            var provider = new SteamAchievements();
            provider.SetLocal();
            return provider;
        }
        #endregion


        #region Errors
        public virtual void ShowNotificationPluginNoPublic(string Message)
        {
            logger.Warn($"{ClientName} user is not public");

            PluginDatabase.PlayniteApi.Notifications.Add(new NotificationMessage(
                $"successStory-{ClientName.RemoveWhiteSpace().ToLower()}-nopublic",
                $"SuccessStory\r\n{Message}",
                NotificationType.Error,
                () => PluginDatabase.Plugin.OpenSettingsView()
            ));
        }
        #endregion
    }
}
