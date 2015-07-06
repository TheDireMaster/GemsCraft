﻿// Copyright 2009-2012 Matvei Stefarov <me@matvei.org>

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using fCraft;
using fCraft.ConfigGUI;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace GemsCraftGUI
{
    // This section handles transfer of settings from Config to the specific UI controls, and vice versa.
    // Effectively, it's an adapter between Config's and ConfigUI's representations of the settings
    partial class MainForm
    {
        #region Loading & Applying Config

        void LoadConfig()
        {
            string missingFileMsg = null;
            if (!File.Exists(Paths.WorldListFileName) && !File.Exists(Paths.ConfigFileName))
            {
                missingFileMsg = String.Format("Configuration ({0}) and world list ({1}) were not found. Using defaults.",
                                                Paths.ConfigFileName,
                                                Paths.WorldListFileName);
            }
            else if (!File.Exists(Paths.ConfigFileName))
            {
                missingFileMsg = String.Format("Configuration ({0}) was not found. Using defaults.",
                                                 Paths.ConfigFileName);
            }
            else if (!File.Exists(Paths.WorldListFileName))
            {
                missingFileMsg = String.Format("World list ({0}) was not found. Assuming 0 worlds.",
                                                Paths.WorldListFileName);
            }
            if (missingFileMsg != null)
            {
                MessageBox.Show(missingFileMsg);
            }

            using (LogRecorder loadLogger = new LogRecorder())
            {
                if (Config.Load(false, false))
                {
                    if (loadLogger.HasMessages)
                    {
                        MessageBox.Show(loadLogger.MessageString, "Config loading warnings");
                    }
                }
                else
                {
                    MessageBox.Show(loadLogger.MessageString, "Error occured while trying to load config");
                }
            }

            ApplyTabGeneral();
            ApplyTabChat();
            ApplyTabWorlds(); // also reloads world list
            ApplyTabRanks();
            ApplyTabSecurity();
            ApplyTabSavingAndBackup();
            ApplyTabLogging();
            ApplyTabIrc();
            ApplyTabAdvanced();
            ApplyTabCpe();
            AddChangeHandler(tabs, SomethingChanged);
            AddChangeHandler(bResetTab, SomethingChanged);
            AddChangeHandler(bResetAll, SomethingChanged);
            dgvWorlds.CellValueChanged += delegate
            {
                SomethingChanged(null, null);
            };

            AddChangeHandler(tabChat, HandleTabChatChange);
            bApply.Enabled = false;
        }


        void LoadWorldList()
        {
            if (Worlds.Count > 0) Worlds.Clear();
            if (!File.Exists(Paths.WorldListFileName)) return;

            try
            {
                XDocument doc = XDocument.Load(Paths.WorldListFileName);
                XElement root = doc.Root;
                if (root == null)
                {
                    MessageBox.Show("Worlds.xml is empty or corrupted.");
                    return;
                }

                string errorLog = "";
                using (LogRecorder logRecorder = new LogRecorder())
                {
                    foreach (XElement el in root.Elements("World"))
                    {
                        try
                        {
                            Worlds.Add(new WorldListEntry(el));
                        }
                        catch (Exception ex)
                        {
                            errorLog += ex + Environment.NewLine;
                        }
                    }
                    if (logRecorder.HasMessages)
                    {
                        MessageBox.Show(logRecorder.MessageString, "World list loading warnings.");
                    }
                }
                if (errorLog.Length > 0)
                {
                    MessageBox.Show("Some errors occured while loading the world list:" + Environment.NewLine + errorLog, "Warning");
                }

                FillWorldList();
                XAttribute mainWorldAttr = root.Attribute("main");
                if (mainWorldAttr != null)
                {
                    foreach (WorldListEntry world in Worlds.Where(world => String.Equals(world.Name, mainWorldAttr.Value, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        cMainWorld.SelectedItem = world.Name;
                        break;
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occured while loading the world list: " + Environment.NewLine + ex, "Warning");
            }

            Worlds.ListChanged += SomethingChanged;
        }


        void ApplyTabGeneral()
        {

            tServerName.Text = ConfigKey.ServerName.GetString();
            CustomName.Text = ConfigKey.CustomChatName.GetString();
            SwearBox.Text = ConfigKey.SwearName.GetString();
            CustomAliases.Text = ConfigKey.CustomAliasName.GetString();
            tMOTD.Text = ConfigKey.MOTD.GetString();
            websiteURL.Text = ConfigKey.WebsiteURL.GetString();
            HeartBeatUrlComboBox.Text = ConfigKey.HeartbeatUrl.GetString();

            nMaxPlayers.Value = ConfigKey.MaxPlayers.GetInt();
            CheckMaxPlayersPerWorldValue();
            nMaxPlayersPerWorld.Value = ConfigKey.MaxPlayersPerWorld.GetInt();

            checkUpdate.Checked = ConfigKey.CheckForUpdates.GetString() == "True";



            FillRankList(cDefaultRank, "(lowest rank)");
            if (ConfigKey.DefaultRank.IsBlank())
            {
                cDefaultRank.SelectedIndex = 0;
            }
            else
            {
                RankManager.DefaultRank = Rank.Parse(ConfigKey.DefaultRank.GetString());
                cDefaultRank.SelectedIndex = RankManager.GetIndex(RankManager.DefaultRank);
            }

            cPublic.SelectedIndex = ConfigKey.IsPublic.Enabled() ? 0 : 1;
            nPort.Value = ConfigKey.Port.GetInt();
            MaxCapsValue.Value = ConfigKey.MaxCaps.GetInt();
            nUploadBandwidth.Value = ConfigKey.UploadBandwidth.GetInt();

            int interval = 0;
            xAnnouncements.Checked = ConfigKey.AnnouncementInterval.TryGetInt(out interval) && interval > 0;

            nAnnouncements.Value = xAnnouncements.Checked ? ConfigKey.AnnouncementInterval.GetInt() : 1;

            // UpdaterSettingsWindow
            _updaterWindow.BackupBeforeUpdate = ConfigKey.BackupBeforeUpdate.Enabled();
            _updaterWindow.RunBeforeUpdate = ConfigKey.RunBeforeUpdate.GetString();
            _updaterWindow.RunAfterUpdate = ConfigKey.RunAfterUpdate.GetString();
            _updaterWindow.UpdaterMode = ConfigKey.UpdaterMode.GetEnum<UpdaterMode>();
        }


        void ApplyTabChat()
        {
            xRankColorsInChat.Checked = ConfigKey.RankColorsInChat.Enabled();
            xRankPrefixesInChat.Checked = ConfigKey.RankPrefixesInChat.Enabled();
            xRankPrefixesInList.Checked = ConfigKey.RankPrefixesInList.Enabled();
            xRankColorsInWorldNames.Checked = ConfigKey.RankColorsInWorldNames.Enabled();
            xShowJoinedWorldMessages.Checked = ConfigKey.ShowJoinedWorldMessages.Enabled();
            xShowConnectionMessages.Checked = ConfigKey.ShowConnectionMessages.Enabled();

            _colorSys = Color.ParseToIndex(ConfigKey.SystemMessageColor.GetString());
            ApplyColor(bColorSys, _colorSys);
            Color.Sys = Color.Parse(_colorSys);

            _colorCustom = Color.ParseToIndex(ConfigKey.CustomChatColor.GetString());
            ApplyColor(CustomColor, _colorCustom);
            Color.Custom = Color.Parse(_colorCustom);

            _colorHelp = Color.ParseToIndex(ConfigKey.HelpColor.GetString());
            ApplyColor(bColorHelp, _colorHelp);
            Color.Help = Color.Parse(_colorHelp);

            _colorSay = Color.ParseToIndex(ConfigKey.SayColor.GetString());
            ApplyColor(bColorSay, _colorSay);
            Color.Say = Color.Parse(_colorSay);

            _colorAnnouncement = Color.ParseToIndex(ConfigKey.AnnouncementColor.GetString());
            ApplyColor(bColorAnnouncement, _colorAnnouncement);
            Color.Announcement = Color.Parse(_colorAnnouncement);

            _colorPm = Color.ParseToIndex(ConfigKey.PrivateMessageColor.GetString());
            ApplyColor(bColorPM, _colorPm);
            Color.PM = Color.Parse(_colorPm);

            _colorWarning = Color.ParseToIndex(ConfigKey.WarningColor.GetString());
            ApplyColor(bColorWarning, _colorWarning);
            Color.Warning = Color.Parse(_colorWarning);

            _colorMe = Color.ParseToIndex(ConfigKey.MeColor.GetString());
            ApplyColor(bColorMe, _colorMe);
            Color.Me = Color.Parse(_colorMe);

            _colorGlobal = Color.ParseToIndex(ConfigKey.GlobalColor.GetString());
            ApplyColor(bColorGlobal, _colorGlobal);
            Color.Global = Color.Parse(_colorGlobal);

            UpdateChatPreview();
        }


        void ApplyTabWorlds()
        {
            if (_rankNameList == null)
            {
                _rankNameList = new BindingList<string> {
                    WorldListEntry.DefaultRankOption
                };
                foreach (Rank rank in RankManager.Ranks)
                {
                    _rankNameList.Add(MainForm.ToComboBoxOption(rank));
                }
                dgvcAccess.DataSource = _rankNameList;
                dgvcBuild.DataSource = _rankNameList;
                dgvcBackup.DataSource = WorldListEntry.BackupEnumNames;

                LoadWorldList();
                dgvWorlds.DataSource = Worlds;

            }
            else
            {
                //dgvWorlds.DataSource = null;
                _rankNameList.Clear();
                _rankNameList.Add(WorldListEntry.DefaultRankOption);
                foreach (Rank rank in RankManager.Ranks)
                {
                    _rankNameList.Add(MainForm.ToComboBoxOption(rank));
                }
                foreach (WorldListEntry world in Worlds)
                {
                    world.ReparseRanks();
                }
                Worlds.ResetBindings();
                //dgvWorlds.DataSource = worlds;
            }

            FillRankList(cDefaultBuildRank, "(default rank)");
            if (ConfigKey.DefaultBuildRank.IsBlank())
            {
                cDefaultBuildRank.SelectedIndex = 0;
            }
            else
            {
                RankManager.DefaultBuildRank = Rank.Parse(ConfigKey.DefaultBuildRank.GetString());
                cDefaultBuildRank.SelectedIndex = RankManager.GetIndex(RankManager.DefaultBuildRank);
            }

            if (Paths.IsDefaultMapPath(ConfigKey.MapPath.GetString()))
            {
                tMapPath.Text = Paths.MapPathDefault;
                xMapPath.Checked = false;
            }
            else
            {
                tMapPath.Text = ConfigKey.MapPath.GetString();
                xMapPath.Checked = true;
            }

            xWoMEnableEnvExtensions.Checked = ConfigKey.WoMEnableEnvExtensions.Enabled();
        }


        void ApplyTabRanks()
        {
            _selectedRank = null;
            RebuildRankList();
            DisableRankOptions();
        }


        void ApplyTabSecurity()
        {
            ApplyEnum(cVerifyNames, ConfigKey.VerifyNames, NameVerificationMode.Balanced);

            nMaxConnectionsPerIP.Value = ConfigKey.MaxConnectionsPerIP.GetInt();
            xMaxConnectionsPerIP.Checked = (nMaxConnectionsPerIP.Value > 0);
            xAllowUnverifiedLAN.Checked = ConfigKey.AllowUnverifiedLAN.Enabled();

            nAntispamMessageCount.Value = ConfigKey.AntispamMessageCount.GetInt();
            nAntispamInterval.Value = ConfigKey.AntispamInterval.GetInt();
            nSpamMute.Value = ConfigKey.AntispamMuteDuration.GetInt();

            xAntispamKicks.Checked = (ConfigKey.AntispamMaxWarnings.GetInt() > 0);
            nAntispamMaxWarnings.Value = ConfigKey.AntispamMaxWarnings.GetInt();
            if (!xAntispamKicks.Checked) nAntispamMaxWarnings.Enabled = false;

            xRequireKickReason.Checked = ConfigKey.RequireKickReason.Enabled();
            xRequireBanReason.Checked = ConfigKey.RequireBanReason.Enabled();
            xRequireRankChangeReason.Checked = ConfigKey.RequireRankChangeReason.Enabled();
            xAnnounceKickAndBanReasons.Checked = ConfigKey.AnnounceKickAndBanReasons.Enabled();
            xAnnounceRankChanges.Checked = ConfigKey.AnnounceRankChanges.Enabled();
            xAnnounceRankChangeReasons.Checked = ConfigKey.AnnounceRankChangeReasons.Enabled();
            xAnnounceRankChangeReasons.Enabled = xAnnounceRankChanges.Checked;

            FillRankList(cPatrolledRank, "(default rank)");
            if (ConfigKey.PatrolledRank.IsBlank())
            {
                cPatrolledRank.SelectedIndex = 0;
            }
            else
            {
                RankManager.PatrolledRank = Rank.Parse(ConfigKey.PatrolledRank.GetString());
                cPatrolledRank.SelectedIndex = RankManager.GetIndex(RankManager.PatrolledRank);
            }


            xBlockDBEnabled.Checked = ConfigKey.BlockDBEnabled.Enabled();
            xBlockDBAutoEnable.Checked = ConfigKey.BlockDBAutoEnable.Enabled();

            FillRankList(cBlockDBAutoEnableRank, "(default rank)");
            if (ConfigKey.BlockDBAutoEnableRank.IsBlank())
            {
                cBlockDBAutoEnableRank.SelectedIndex = 0;
            }
            else
            {
                RankManager.BlockDbAutoEnableRank = Rank.Parse(ConfigKey.BlockDBAutoEnableRank.GetString());
                cBlockDBAutoEnableRank.SelectedIndex = RankManager.GetIndex(RankManager.BlockDbAutoEnableRank);
            }
        }


        void ApplyTabSavingAndBackup()
        {
            xSaveInterval.Checked = (ConfigKey.SaveInterval.GetInt() > 0);
            nSaveInterval.Value = ConfigKey.SaveInterval.GetInt();
            if (!xSaveInterval.Checked) nSaveInterval.Enabled = false;

            xBackupOnStartup.Checked = ConfigKey.BackupOnStartup.Enabled();
            xBackupOnJoin.Checked = ConfigKey.BackupOnJoin.Enabled();
            xBackupOnlyWhenChanged.Checked = ConfigKey.BackupOnlyWhenChanged.Enabled();

            xBackupInterval.Checked = (ConfigKey.DefaultBackupInterval.GetInt() > 0);
            nBackupInterval.Value = ConfigKey.DefaultBackupInterval.GetInt();
            if (!xBackupInterval.Checked) nBackupInterval.Enabled = false;

            xMaxBackups.Checked = (ConfigKey.MaxBackups.GetInt() > 0);
            nMaxBackups.Value = ConfigKey.MaxBackups.GetInt();
            if (!xMaxBackups.Checked) nMaxBackups.Enabled = false;

            xMaxBackupSize.Checked = (ConfigKey.MaxBackupSize.GetInt() > 0);
            nMaxBackupSize.Value = ConfigKey.MaxBackupSize.GetInt();
            if (!xMaxBackupSize.Checked) nMaxBackupSize.Enabled = false;

            xBackupDataOnStartup.Checked = ConfigKey.BackupDataOnStartup.Enabled();
        }


        void ApplyTabLogging()
        {
            foreach (ListViewItem item in vConsoleOptions.Items)
            {
                item.Checked = Logger.ConsoleOptions[item.Index];
            }
            foreach (ListViewItem item in vLogFileOptions.Items)
            {
                item.Checked = Logger.LogFileOptions[item.Index];
            }

            ApplyEnum(cLogMode, ConfigKey.LogMode, LogSplittingType.OneFile);

            xLogLimit.Checked = (ConfigKey.MaxLogs.GetInt() > 0);
            nLogLimit.Value = ConfigKey.MaxLogs.GetInt();
            if (!xLogLimit.Checked) nLogLimit.Enabled = false;
        }


        void ApplyTabIrc()
        {
            xIRCBotEnabled.Checked = ConfigKey.IRCBotEnabled.Enabled();
            gIRCNetwork.Enabled = xIRCBotEnabled.Checked;
            gIRCOptions.Enabled = xIRCBotEnabled.Checked;

            tIRCBotNetwork.Text = ConfigKey.IRCBotNetwork.GetString();
            nIRCBotPort.Value = ConfigKey.IRCBotPort.GetInt();
            nIRCDelay.Value = ConfigKey.IRCDelay.GetInt();

            tIRCBotChannels.Text = ConfigKey.IRCBotChannels.GetString();

            tIRCBotNick.Text = ConfigKey.IRCBotNick.GetString();
            xIRCRegisteredNick.Checked = ConfigKey.IRCRegisteredNick.Enabled();

            tIRCNickServ.Text = ConfigKey.IRCNickServ.GetString();
            tIRCNickServMessage.Text = ConfigKey.IRCNickServMessage.GetString();

            xIRCBotAnnounceIRCJoins.Checked = ConfigKey.IRCBotAnnounceIRCJoins.Enabled();
            xIRCBotAnnounceServerJoins.Checked = ConfigKey.IRCBotAnnounceServerJoins.Enabled();
            xIRCBotForwardFromIRC.Checked = ConfigKey.IRCBotForwardFromIRC.Enabled();
            xIRCBotForwardFromServer.Checked = ConfigKey.IRCBotForwardFromServer.Enabled();


            _colorIrc = Color.ParseToIndex(ConfigKey.IRCMessageColor.GetString());
            ApplyColor(bColorIRC, _colorIrc);
            Color.IRC = Color.Parse(_colorIrc);

            xIRCUseColor.Checked = ConfigKey.IRCUseColor.Enabled();
            xIRCBotAnnounceServerEvents.Checked = ConfigKey.IRCBotAnnounceServerEvents.Enabled();

            //if server pass is in use
            if (ConfigKey.IRCBotNetworkPass.GetString() != "defaultPass")
            {
                xServPass.Checked = true;
            }

            //if chan pass is in use
            if (ConfigKey.IRCChannelPassword.GetString() != "password")
            {
                xChanPass.Checked = true;
            }

            tChanPass.Text = ConfigKey.IRCChannelPassword.GetString();
            tServPass.Text = ConfigKey.IRCBotNetworkPass.GetString();
                
        }


        void ApplyTabAdvanced()
        {
            xRelayAllBlockUpdates.Checked = ConfigKey.RelayAllBlockUpdates.Enabled();
            xNoPartialPositionUpdates.Checked = ConfigKey.NoPartialPositionUpdates.Enabled();
            nTickInterval.Value = ConfigKey.TickInterval.GetInt();

            if (ConfigKey.ProcessPriority.IsBlank())
            {
                cProcessPriority.SelectedIndex = 0; // Default
            }
            else
            {
                switch (ConfigKey.ProcessPriority.GetEnum<ProcessPriorityClass>())
                {
                    case ProcessPriorityClass.High:
                        cProcessPriority.SelectedIndex = 1; break;
                    case ProcessPriorityClass.AboveNormal:
                        cProcessPriority.SelectedIndex = 2; break;
                    case ProcessPriorityClass.Normal:
                        cProcessPriority.SelectedIndex = 3; break;
                    case ProcessPriorityClass.BelowNormal:
                        cProcessPriority.SelectedIndex = 4; break;
                    case ProcessPriorityClass.Idle:
                        cProcessPriority.SelectedIndex = 5; break;
                }
            }



            nThrottling.Value = ConfigKey.BlockUpdateThrottling.GetInt();
            xLowLatencyMode.Checked = ConfigKey.LowLatencyMode.Enabled();
            xAutoRank.Checked = ConfigKey.AutoRankEnabled.Enabled();


            if (ConfigKey.MaxUndo.GetInt() > 0)
            {
                xMaxUndo.Checked = true;
                nMaxUndo.Value = ConfigKey.MaxUndo.GetInt();
            }
            else
            {
                xMaxUndo.Checked = false;
                nMaxUndo.Value = (int)ConfigKey.MaxUndo.GetDefault();
            }
            nMaxUndoStates.Value = ConfigKey.MaxUndoStates.GetInt();

            tConsoleName.Text = ConfigKey.ConsoleName.GetString();

            tIP.Text = ConfigKey.IP.GetString();
            xCrash.Checked = ConfigKey.SubmitCrashReports.Enabled();
            if (ConfigKey.IP.IsBlank() || ConfigKey.IP.IsDefault())
            {
                tIP.Enabled = false;
                xIP.Checked = false;
            }
            else
            {
                tIP.Enabled = true;
                xIP.Checked = true;
            }

            //Dragon stuffs
            var dragonString = ConfigKey.DragonDefault.GetString();
            if (dragonString == null || dragonString.Equals("Fire"))
            {
                cboDragonDefault.SelectedIndex = 0;
            }
            else
            {
                cboDragonDefault.SelectedIndex = cboDragonDefault.FindStringExact(dragonString);
            }

            for (int x = 0; x <= 3; x++)
            {
                clbDragonPermits.SetItemCheckState(x, CheckStateS(x));
            }
        }

        // TODO - Insert all CPE configs here
        void ApplyTabCpe()
        {
            chkClickDistanceAllowed.Checked = ConfigKey.ClickDistanceEnabled.Enabled();
            #region CB
            chkCustomBlocksAllowed.Checked = ConfigKey.CustomBlocksEnabled.Enabled();
            //var newItemList = clbBlocks.Items.Cast<bool>().ToList();
            clbBlocks.SetItemChecked(0, Bk(ConfigKey.CobbleSlabEnabled));
            clbBlocks.SetItemChecked(1, Bk(ConfigKey.RopeEnabled));
            clbBlocks.SetItemChecked(2, Bk(ConfigKey.SandstoneEnabled));
            clbBlocks.SetItemChecked(3, Bk(ConfigKey.SnowEnabled));
            clbBlocks.SetItemChecked(4, Bk(ConfigKey.FireEnabled));
            clbBlocks.SetItemChecked(5, Bk(ConfigKey.LightPinkEnabled));
            clbBlocks.SetItemChecked(6, Bk(ConfigKey.DarkGreenEnabled));
            clbBlocks.SetItemChecked(7, Bk(ConfigKey.BrownEnabled));
            clbBlocks.SetItemChecked(8, Bk(ConfigKey.DarkBlueEnabled));
            clbBlocks.SetItemChecked(9, Bk(ConfigKey.TurquoiseEnabled));
            clbBlocks.SetItemChecked(10, Bk(ConfigKey.IceEnabled));
            clbBlocks.SetItemChecked(11, Bk(ConfigKey.TileEnabled));
            clbBlocks.SetItemChecked(12, Bk(ConfigKey.MagmaEnabled));
            clbBlocks.SetItemChecked(13, Bk(ConfigKey.PillarEnabled));
            clbBlocks.SetItemChecked(14, Bk(ConfigKey.CrateEnabled));
            clbBlocks.SetItemChecked(15, Bk(ConfigKey.StoneBrickEnabled));
            #endregion
        }
        private static bool Bk(ConfigKey cK)
        {
            return cK.Enabled();
        }
        static CheckState CheckStateS(int dragblock)
        {
            var desBool = false;
            switch (dragblock)
            {
                case 0:
                    desBool = ConfigKey.DragonFire.Enabled();
                    break;
                case 1:
                    desBool = ConfigKey.DragonMagma.Enabled();
                    break;
                case 2:
                    desBool = ConfigKey.DragonLava.Enabled();
                    break;
                case 3:
                    desBool = ConfigKey.DragonRed.Enabled();
                    break;
            }
            return desBool ? CheckState.Checked : CheckState.Unchecked;
        }
        static void ApplyEnum<TEnum>([NotNull] ComboBox box, ConfigKey key, TEnum def) where TEnum : struct
        {
            if (box == null) throw new ArgumentNullException("box");
            if (!typeof(TEnum).IsEnum) throw new ArgumentException("Enum type required");
            try
            {
                if (key.IsBlank())
                {
                    box.SelectedIndex = (int)(object)def;
                }
                else
                {
                    box.SelectedIndex = (int)Enum.Parse(typeof(TEnum), key.GetString(), true);
                }
            }
            catch (ArgumentException)
            {
                box.SelectedIndex = (int)(object)def;
            }
        }

        #endregion


        #region Saving Config

        void SaveConfig()
        {
            // General

            ConfigKey.ServerName.TrySetValue(tServerName.Text);
            ConfigKey.CustomChatName.TrySetValue(CustomName.Text);
            ConfigKey.SwearName.TrySetValue(SwearBox.Text);
            ConfigKey.CheckForUpdates.TrySetValue(checkUpdate.Checked.ToString());
            ConfigKey.WebsiteURL.TrySetValue(websiteURL.Text);
            ConfigKey.HeartbeatUrl.TrySetValue(HeartBeatUrlComboBox.SelectedItem);
            ConfigKey.CustomAliasName.TrySetValue(CustomAliases.Text);
            ConfigKey.MOTD.TrySetValue(tMOTD.Text);
            ConfigKey.MaxPlayers.TrySetValue(nMaxPlayers.Value);
            ConfigKey.MaxPlayersPerWorld.TrySetValue(nMaxPlayersPerWorld.Value);
            ConfigKey.DefaultRank.TrySetValue(cDefaultRank.SelectedIndex == 0 ? "" : RankManager.DefaultRank.FullName);
            ConfigKey.IsPublic.TrySetValue(cPublic.SelectedIndex == 0);
            ConfigKey.Port.TrySetValue(nPort.Value);
            ConfigKey.MaxCaps.TrySetValue(MaxCapsValue.Value);
            if (xIP.Checked)
            {
                ConfigKey.IP.TrySetValue(tIP.Text);
            }
            else
            {
                ConfigKey.IP.ResetValue();
            }

            ConfigKey.UploadBandwidth.TrySetValue(nUploadBandwidth.Value);

            ConfigKey.AnnouncementInterval.TrySetValue(xAnnouncements.Checked ? nAnnouncements.Value : 0);

            // UpdaterSettingsWindow
            ConfigKey.UpdaterMode.TrySetValue(_updaterWindow.UpdaterMode);
            ConfigKey.BackupBeforeUpdate.TrySetValue(_updaterWindow.BackupBeforeUpdate);
            ConfigKey.RunBeforeUpdate.TrySetValue(_updaterWindow.RunBeforeUpdate);
            ConfigKey.RunAfterUpdate.TrySetValue(_updaterWindow.RunAfterUpdate);


            // Chat
            ConfigKey.SystemMessageColor.TrySetValue(Color.GetName(_colorSys));
            ConfigKey.CustomChatColor.TrySetValue(Color.GetName(_colorCustom));
            ConfigKey.HelpColor.TrySetValue(Color.GetName(_colorHelp));
            ConfigKey.SayColor.TrySetValue(Color.GetName(_colorSay));
            ConfigKey.AnnouncementColor.TrySetValue(Color.GetName(_colorAnnouncement));
            ConfigKey.PrivateMessageColor.TrySetValue(Color.GetName(_colorPm));
            ConfigKey.WarningColor.TrySetValue(Color.GetName(_colorWarning));
            ConfigKey.MeColor.TrySetValue(Color.GetName(_colorMe));
            ConfigKey.GlobalColor.TrySetValue(Color.GetName(_colorGlobal));
            ConfigKey.ShowJoinedWorldMessages.TrySetValue(xShowJoinedWorldMessages.Checked);
            ConfigKey.RankColorsInWorldNames.TrySetValue(xRankColorsInWorldNames.Checked);
            ConfigKey.RankColorsInChat.TrySetValue(xRankColorsInChat.Checked);
            ConfigKey.RankPrefixesInChat.TrySetValue(xRankPrefixesInChat.Checked);
            ConfigKey.RankPrefixesInList.TrySetValue(xRankPrefixesInList.Checked);
            ConfigKey.ShowConnectionMessages.TrySetValue(xShowConnectionMessages.Checked);


            // Worlds
            ConfigKey.DefaultBuildRank.TrySetValue(cDefaultBuildRank.SelectedIndex == 0
                ? ""
                : RankManager.DefaultBuildRank.FullName);

            ConfigKey.MapPath.TrySetValue(xMapPath.Checked ? tMapPath.Text : ConfigKey.MapPath.GetDefault());

            ConfigKey.WoMEnableEnvExtensions.TrySetValue(xWoMEnableEnvExtensions.Checked);


            // Security
            WriteEnum<NameVerificationMode>(cVerifyNames, ConfigKey.VerifyNames);

            ConfigKey.MaxConnectionsPerIP.TrySetValue(xMaxConnectionsPerIP.Checked ? nMaxConnectionsPerIP.Value : 0);
            ConfigKey.AllowUnverifiedLAN.TrySetValue(xAllowUnverifiedLAN.Checked);

            ConfigKey.AntispamMessageCount.TrySetValue(nAntispamMessageCount.Value);
            ConfigKey.AntispamInterval.TrySetValue(nAntispamInterval.Value);
            ConfigKey.AntispamMuteDuration.TrySetValue(nSpamMute.Value);

            ConfigKey.AntispamMaxWarnings.TrySetValue(xAntispamKicks.Checked ? nAntispamMaxWarnings.Value : 0);

            ConfigKey.RequireKickReason.TrySetValue(xRequireKickReason.Checked);
            ConfigKey.RequireBanReason.TrySetValue(xRequireBanReason.Checked);
            ConfigKey.RequireRankChangeReason.TrySetValue(xRequireRankChangeReason.Checked);
            ConfigKey.AnnounceKickAndBanReasons.TrySetValue(xAnnounceKickAndBanReasons.Checked);
            ConfigKey.AnnounceRankChanges.TrySetValue(xAnnounceRankChanges.Checked);
            ConfigKey.AnnounceRankChangeReasons.TrySetValue(xAnnounceRankChangeReasons.Checked);

            ConfigKey.PatrolledRank.TrySetValue(cPatrolledRank.SelectedIndex == 0
                ? ""
                : RankManager.PatrolledRank.FullName);

            ConfigKey.BlockDBEnabled.TrySetValue(xBlockDBEnabled.Checked);
            ConfigKey.BlockDBAutoEnable.TrySetValue(xBlockDBAutoEnable.Checked);
            ConfigKey.BlockDBAutoEnableRank.TrySetValue(cBlockDBAutoEnableRank.SelectedIndex == 0
                ? ""
                : RankManager.BlockDbAutoEnableRank.FullName);


            // Saving & Backups
            ConfigKey.SaveInterval.TrySetValue(xSaveInterval.Checked ? nSaveInterval.Value : 0);
            ConfigKey.BackupOnStartup.TrySetValue(xBackupOnStartup.Checked);
            ConfigKey.BackupOnJoin.TrySetValue(xBackupOnJoin.Checked);
            ConfigKey.BackupOnlyWhenChanged.TrySetValue(xBackupOnlyWhenChanged.Checked);

            ConfigKey.DefaultBackupInterval.TrySetValue(xBackupInterval.Checked ? nBackupInterval.Value : 0);
            ConfigKey.MaxBackups.TrySetValue(xMaxBackups.Checked ? nMaxBackups.Value : 0);
            ConfigKey.MaxBackupSize.TrySetValue(xMaxBackupSize.Checked ? nMaxBackupSize.Value : 0);

            ConfigKey.BackupDataOnStartup.TrySetValue(xBackupDataOnStartup.Checked);


            // Logging
            WriteEnum<LogSplittingType>(cLogMode, ConfigKey.LogMode);
            if (xLogLimit.Checked) ConfigKey.MaxLogs.TrySetValue(nLogLimit.Value);
            else ConfigKey.MaxLogs.TrySetValue("0");
            foreach (ListViewItem item in vConsoleOptions.Items)
            {
                Logger.ConsoleOptions[item.Index] = item.Checked;
            }
            foreach (ListViewItem item in vLogFileOptions.Items)
            {
                Logger.LogFileOptions[item.Index] = item.Checked;
            }


            // IRC
            ConfigKey.IRCBotEnabled.TrySetValue(xIRCBotEnabled.Checked);

            ConfigKey.IRCBotNetwork.TrySetValue(tIRCBotNetwork.Text);
            ConfigKey.IRCBotPort.TrySetValue(nIRCBotPort.Value);
            ConfigKey.IRCDelay.TrySetValue(nIRCDelay.Value);

            ConfigKey.IRCBotChannels.TrySetValue(tIRCBotChannels.Text);

            ConfigKey.IRCBotNick.TrySetValue(tIRCBotNick.Text);
            ConfigKey.IRCRegisteredNick.TrySetValue(xIRCRegisteredNick.Checked);
            ConfigKey.IRCNickServ.TrySetValue(tIRCNickServ.Text);
            ConfigKey.IRCNickServMessage.TrySetValue(tIRCNickServMessage.Text);

            ConfigKey.IRCBotAnnounceIRCJoins.TrySetValue(xIRCBotAnnounceIRCJoins.Checked);
            ConfigKey.IRCBotAnnounceServerJoins.TrySetValue(xIRCBotAnnounceServerJoins.Checked);
            ConfigKey.IRCBotAnnounceServerEvents.TrySetValue(xIRCBotAnnounceServerEvents.Checked);
            ConfigKey.IRCBotForwardFromIRC.TrySetValue(xIRCBotForwardFromIRC.Checked);
            ConfigKey.IRCBotForwardFromServer.TrySetValue(xIRCBotForwardFromServer.Checked);

            ConfigKey.IRCMessageColor.TrySetValue(Color.GetName(_colorIrc));
            ConfigKey.IRCUseColor.TrySetValue(xIRCUseColor.Checked);

            ConfigKey.IRCBotNetworkPass.TrySetValue(tServPass.Text);
            ConfigKey.IRCChannelPassword.TrySetValue(tChanPass.Text);


            // advanced

            ConfigKey.SubmitCrashReports.TrySetValue(xCrash.Checked);
            ConfigKey.RelayAllBlockUpdates.TrySetValue(xRelayAllBlockUpdates.Checked);
            ConfigKey.NoPartialPositionUpdates.TrySetValue(xNoPartialPositionUpdates.Checked);
            ConfigKey.TickInterval.TrySetValue(Convert.ToInt32(nTickInterval.Value));

            switch (cProcessPriority.SelectedIndex)
            {
                case 0:
                    ConfigKey.ProcessPriority.ResetValue(); break;
                case 1:
                    ConfigKey.ProcessPriority.TrySetValue(ProcessPriorityClass.High); break;
                case 2:
                    ConfigKey.ProcessPriority.TrySetValue(ProcessPriorityClass.AboveNormal); break;
                case 3:
                    ConfigKey.ProcessPriority.TrySetValue(ProcessPriorityClass.Normal); break;
                case 4:
                    ConfigKey.ProcessPriority.TrySetValue(ProcessPriorityClass.BelowNormal); break;
                case 5:
                    ConfigKey.ProcessPriority.TrySetValue(ProcessPriorityClass.Idle); break;
            }

            ConfigKey.BlockUpdateThrottling.TrySetValue(Convert.ToInt32(nThrottling.Value));

            ConfigKey.LowLatencyMode.TrySetValue(xLowLatencyMode.Checked);

            ConfigKey.AutoRankEnabled.TrySetValue(xAutoRank.Checked);

            ConfigKey.MaxUndo.TrySetValue(xMaxUndo.Checked ? Convert.ToInt32(nMaxUndo.Value) : 0);
            ConfigKey.MaxUndoStates.TrySetValue(Convert.ToInt32(nMaxUndoStates.Value));

            ConfigKey.ConsoleName.TrySetValue(tConsoleName.Text);

            //Dragon stuff
            ConfigKey.DragonDefault.TrySetValue(cboDragonDefault.SelectedItem.ToString());
            ConfigKey.DragonFire.TrySetValue(clbDragonPermits.GetItemChecked(0));
            ConfigKey.DragonMagma.TrySetValue(clbDragonPermits.GetItemChecked(1));
            ConfigKey.DragonLava.TrySetValue(clbDragonPermits.GetItemChecked(2));
            ConfigKey.DragonRed.TrySetValue(clbDragonPermits.GetItemChecked(3));
            SaveWorldList();

            //CPE
            //TODO insert all CPE configs
            ConfigKey.ClickDistanceEnabled.TrySetValue(chkClickDistanceAllowed.Checked);
            #region CB
            ConfigKey.CustomBlocksEnabled.TrySetValue(chkCustomBlocksAllowed.Checked);
            ConfigKey.CobbleSlabEnabled.TrySetValue(clbBlocks.GetItemChecked(0));
            ConfigKey.RopeEnabled.TrySetValue(clbBlocks.GetItemChecked(1));
            ConfigKey.SandstoneEnabled.TrySetValue(clbBlocks.GetItemChecked(2));
            ConfigKey.SnowEnabled.TrySetValue(clbBlocks.GetItemChecked(3));
            ConfigKey.FireEnabled.TrySetValue(clbBlocks.GetItemChecked(4));
            ConfigKey.LightPinkEnabled.TrySetValue(clbBlocks.GetItemChecked(5));
            ConfigKey.DarkGreenEnabled.TrySetValue(clbBlocks.GetItemChecked(6));
            ConfigKey.BrownEnabled.TrySetValue(clbBlocks.GetItemChecked(7));
            ConfigKey.DarkBlueEnabled.TrySetValue(clbBlocks.GetItemChecked(8));
            ConfigKey.TurquoiseEnabled.TrySetValue(clbBlocks.GetItemChecked(9));
            ConfigKey.IceEnabled.TrySetValue(clbBlocks.GetItemChecked(10));
            ConfigKey.TileEnabled.TrySetValue(clbBlocks.GetItemChecked(11));
            ConfigKey.MagmaEnabled.TrySetValue(clbBlocks.GetItemChecked(12));
            ConfigKey.PillarEnabled.TrySetValue(clbBlocks.GetItemChecked(13));
            ConfigKey.CrateEnabled.TrySetValue(clbBlocks.GetItemChecked(14));
            ConfigKey.StoneBrickEnabled.TrySetValue(clbBlocks.GetItemChecked(15));
            #endregion
        }

        private readonly ConfigKey[] _cbConfigs = {
            ConfigKey.CobbleSlabEnabled, ConfigKey.RopeEnabled, ConfigKey.SandstoneEnabled,
            ConfigKey.SnowEnabled, ConfigKey.FireEnabled, ConfigKey.LightPinkEnabled, ConfigKey.DarkGreenEnabled,
            ConfigKey.BrownEnabled, ConfigKey.DarkBlueEnabled, ConfigKey.TurquoiseEnabled, ConfigKey.IceEnabled,
            ConfigKey.TileEnabled, ConfigKey.MagmaEnabled, ConfigKey.PillarEnabled, ConfigKey.CrateEnabled,
            ConfigKey.StoneBrickEnabled, ConfigKey.DefaultRank // Last item never used to protect against exception
        };

        private ConfigKey MyConfig(int i)
        {
            return _cbConfigs[i];
        }
        private int MyIntConfig(ConfigKey cK)
        {
            var intLoop = 0;
            foreach (var ck in _cbConfigs)
            {
                if (cK.Equals(ck))
                {
                    return intLoop;
                }
                intLoop++;
            }
            return -1;
        }

        void SaveWorldList()
        {
            const string worldListTempFileName = Paths.WorldListFileName + ".tmp";
            try
            {
                XDocument doc = new XDocument();
                XElement root = new XElement("fCraftWorldList");
                foreach (WorldListEntry world in Worlds)
                {
                    root.Add(world.Serialize());
                }
                if (cMainWorld.SelectedItem != null)
                {
                    root.Add(new XAttribute("main", cMainWorld.SelectedItem));
                }
                doc.Add(root);
                doc.Save(worldListTempFileName);
                Paths.MoveOrReplace(worldListTempFileName, Paths.WorldListFileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("An error occured while trying to save world list ({0}): {1}{2}",
                                                Paths.WorldListFileName,
                                                Environment.NewLine,
                                                ex));
            }
        }


        static void WriteEnum<TEnum>([NotNull] ComboBox box, ConfigKey key) where TEnum : struct
        {
            if (box == null) throw new ArgumentNullException("box");
            if (!typeof(TEnum).IsEnum) throw new ArgumentException("Enum type required");
            try
            {
                TEnum val = (TEnum)Enum.Parse(typeof(TEnum), box.SelectedIndex.ToString(), true);
                key.TrySetValue(val);
            }
            catch (ArgumentException)
            {
                Logger.Log(LogType.Error,
                            "ConfigUI.WriteEnum<{0}>: Could not parse value for {1}. Using default ({2}).",
                            typeof(TEnum).Name, key, key.GetString());
            }
        }

        #endregion
    }
}
