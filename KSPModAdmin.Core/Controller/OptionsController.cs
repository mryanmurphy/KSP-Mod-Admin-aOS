﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using FolderSelect;
using KSPModAdmin.Core.Model;
using KSPModAdmin.Core.Utils.Localization;
using KSPModAdmin.Core.Views;
using KSPModAdmin.Core.Utils;

namespace KSPModAdmin.Core.Controller
{
    /// <summary>
    /// EventHandler for the KSPPathChanging event.
    /// </summary>
    /// <param name="oldKSPPath"></param>
    /// <param name="newKSPPath"></param>
    public delegate void KSPPathChangingHandler(string oldKSPPath, string newKSPPath);

    /// <summary>
    /// EventHandler for the KSPPathChanged event.
    /// </summary>
    public delegate void KSPPathChangedHandler(string kspPath);

    public class OptionsController : BaseController<OptionsController, ucOptions>
    {
        #region Enums

        /// <summary>
        /// Enum of possible AsyncTask actions.
        /// </summary>
        enum TaskAction
        {
            None,
            AppUpdateCheck,
            ModsUpdateCheck,
            DownloadApp,
            SteamSearch,
            FolderSearch
        }

        #endregion

        #region Events

        /// <summary>
        /// Event for selected KSP paths changing.
        /// Occurs when the selected KSP paths has changing.
        /// </summary>
        public static event KSPPathChangingHandler KSPPathChanging = null;

        /// <summary>
        /// Event for selected KSP paths changed.
        /// Occurs when the selected KSP paths has changed.
        /// </summary>
        public static event KSPPathChangedHandler KSPPathChanged = null;

        #endregion

        #region Constants

        public const string KSP = "KSP";
        public const string KSPMA = "KSPModAdmin";
        public const string KSPINSTALL = "KSP install";
        public const string DOWNLOAD = "download";
        public const string DOWNLOADS = "Downloads";
        public const string STEAM = "Steam";
        public const string STEAMAPP_PATH = "SteamApps\\common";
        public const string RECYCLE_BIN = "recycle.bin";
        public const string START = "Start";
        public const string STOP = "Stop";
        public const string KSPMA_UPDATER_EXE = "KSPModAdmin.Updater.exe";
        public const string KSPMA_UPDATER_PARAMETER_0_1_2_3 = "version={0} process={1} archive={2} dest={3}";

        #endregion

        #region Members

        /// <summary>
        /// Flag to stop ksp install folder search.
        /// </summary>
        private static bool mStopSearch;

        private static TaskAction mTaskAction = TaskAction.None;

        private static string mLastSelectedKSPPath = string.Empty;

        #endregion

        #region Properties

        #region Update

        /// <summary>
        /// Gets or sets the cbVersionCheck CheckBox.
        /// Determines whether we should check for updates at start up.
        /// </summary>
        public static bool VersionCheck
        {
            get { return (View != null) && View.VersionCheck; }
            set { if (View != null) View.VersionCheck = value; }
        }

        /// <summary>
        /// The action the should be performed after an update download.
        /// </summary>
        public static PostDownloadAction PostDownloadAction
        {
            get { return (View != null) ? View.PostDownloadAction : PostDownloadAction.Ask; }
            set { if (View != null) View.PostDownloadAction = value; }
        }

        /// <summary>
        /// The interval of mod updating.
        /// </summary>
        public static ModUpdateInterval ModUpdateInterval
        {
            get { return (View != null) ? View.ModUpdateInterval : ModUpdateInterval.Manualy; }
            set { if (View != null) View.ModUpdateInterval = value; }
        }

        /// <summary>
        /// Date of last mod update check
        /// </summary>
        public static DateTime LastModUpdateTry
        {
            get { return (View != null) ? View.LastModUpdateTry : DateTime.MinValue; }
            set { if (View != null) View.LastModUpdateTry = value; }
        }

        /// <summary>
        /// Gets or sets the action that should be performed when the mod will be auto updated.
        /// </summary>
        public static ModUpdateBehavior ModUpdateBehavior
        {
            get { return (View != null) ? View.ModUpdateBehavior : ModUpdateBehavior.CopyDestination; }
            set { if (View != null) View.ModUpdateBehavior = value; }
        }

        #endregion

        #region Paths

        /// <summary>
        /// Gets or sets the selected KSP path.
        /// </summary>
        public static string SelectedKSPPath
        {
            get { return (View == null) ? string.Empty : View.SelectedKSPPath; }
            set
            {
                if (View != null)
                {
                    if (KSPPathChanging != null)
                    {
                        View.SilentSetSelectedKSPPath(mLastSelectedKSPPath);
                        KSPPathChanging(View.SelectedKSPPath, value);
                    }

                    View.SilentSetSelectedKSPPath(value);

                    View.tbDownloadPath.Text = string.Empty;
                    View.btnOpenDownloadFolder.Enabled = false;
                    View.btnOpenDownloads.Enabled = false;
                    View.btnOpenKSPRoot.Enabled = false;
                    View.btnOpenKSPRoot.Enabled = !string.IsNullOrEmpty(value);
                    
                    if (KSPPathChanged != null)
                    {
                        Messenger.AddInfo(string.Format(Messages.MSG_SELECTED_KSP_FOLDER_CHANGED_0, value));
                        KSPPathChanged(value);
                    }

                    mLastSelectedKSPPath = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the download\mods path.
        /// </summary>
        public static string DownloadPath
        {
            get { return (View == null) ? string.Empty : View.DownloadPath; }
            set
            {
                if (View != null)
                {
                    Messenger.AddInfo(string.Format(Messages.MSG_DOWNLOADPATH_CHANGED_0, value));
                    View.DownloadPath = value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        View.btnOpenDownloadFolder.Enabled = true;
                        View.btnOpenDownloads.Enabled = true;
                    }
                    else
                    {
                        View.btnOpenDownloadFolder.Enabled = false;
                        View.btnOpenDownloads.Enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected KSP path.
        /// </summary>
        public static NoteNode SelectedKnownKSPPath
        {
            get { return (View == null) ? null : View.SelectedKnownKSPPath; }
            set { if (View != null) View.SelectedKnownKSPPath = value; }
        }

        /// <summary>
        /// Gets or sets the known KSP install paths.
        /// </summary>
        public static List<NoteNode> KnownKSPPaths
        {
            get { return (View == null) ? new List<NoteNode>() : View.KnownKSPPaths; }
            set
            {
                if (View != null)
                {
                    if (value != null)
                    {
                        View.KnownKSPPaths = value;
                        MainController.KnownKSPPaths = value;
                    }
                    else
                    {
                        View.KnownKSPPaths = new List<NoteNode>();
                        MainController.KnownKSPPaths = new List<NoteNode>();
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the search depth.
        /// </summary>
        public static int SearchDepth
        {
            get { return (View == null) ? 3 : View.SearchDepth; }
            set { if (View != null) View.SearchDepth = value; }
        }

        #endregion

        #region Misc

        /// <summary>
        /// Gets or sets the flag to determine if the Conflict detection should be turned on or off.
        /// </summary>
        public static bool ConflictDetectionOnOff
        {
            get { return (View != null) && View.ConflictDetectionOnOff; }
            set
            {
                if (View != null && value != View.ConflictDetectionOnOff)
                    View.ConflictDetectionOnOff = value;

                if (value != ModRegister.ConflictDetectionOnOff)
                {
                    ModRegister.ConflictDetectionOnOff = value;
                    Messenger.AddInfo((value) ? Messages.MSG_CONFLICT_DETECTION_ON : Messages.MSG_CONFLICT_DETECTION_OFF);
                }
            }
        }

        /// <summary>
        /// Gets or sets the flag to determine if the ConflictSolver dialog should be shown or not.
        /// </summary>
        public static bool ShowConflictSolver
        {
            get { return (View != null) && View.ShowConflictSolver; }
            set { if (View != null) View.ShowConflictSolver = value; }
        }

        #region Colors

        /// <summary>
        /// Gets or sets the color for TreeNodes where a destination was found.
        /// </summary>
        public static Color ColorDestinationDetected
        {
            get { return View.ColorDestinationDetected; }
            set { View.ColorDestinationDetected = value; }
        }

        /// <summary>
        /// Gets or sets the color for TreeNodes where a destination is missing.
        /// </summary>
        public static Color ColorDestinationMissing
        {
            get { return View.ColorDestinationMissing; }
            set { View.ColorDestinationMissing = value; }
        }

        /// <summary>
        /// Gets or sets the color for TreeNodes where the mod has conflicts with other mods.
        /// </summary>
        public static Color ColorDestinationConflict
        {
            get { return View.ColorDestinationConflict; }
            set { View.ColorDestinationConflict = value; }
        }

        /// <summary>
        /// Gets or sets the color for TreeNodes where is installed.
        /// </summary>
        public static Color ColorModInstalled
        {
            get { return View.ColorModInstalled; }
            set { View.ColorModInstalled = value; }
        }

        /// <summary>
        /// Gets or sets the color for TreeNodes where mod archive missing.
        /// </summary>
        public static Color ColorModArchiveMissing
        {
            get { return View.ColorModArchiveMissing; }
            set { View.ColorModArchiveMissing = value; }
        }

        /// <summary>
        /// Gets or sets the color for TreeNodes with outdated mods.
        /// </summary>
        public static Color ColorModOutdated
        {
            get { return View.ColorModOutdated; }
            set { View.ColorModOutdated = value; }
        }

        #endregion

        #endregion

        public static string[] AvailableLanguages
        {
            get { return View.AvailableLanguages; }
            set { View.AvailableLanguages = value; }
        }

        public static string SelectedLanguage 
        {
            get { return View.SelectedLanguage; }
            set
            {
                View.SelectedLanguage = value;
                Localizer.GlobalInstance.CurrentLanguage = value;
            } 
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Private constructor
        /// </summary>
        private OptionsController()
        {

        }

        #endregion

        #region Public

        #region Update Tab

        #region Update App

        /// <summary>
        /// Starts an async Job.
        /// Gets the current version from "www.services.mactee.de/..."
        /// and asks to start a download if new version is available.
        /// </summary>
        public static void Check4AppUpdates()
        {
            Messenger.AddInfo(Messages.MSG_KSPMA_UPDATE_CHECK_STARTED);
            mTaskAction = TaskAction.AppUpdateCheck;
            EventDistributor.InvokeAsyncTaskStarted(Instance);
            AsyncTask<WebResponse>.DoWork(
                delegate()
                {
                    WebResponse response = null;
                    WebRequest request = WebRequest.Create(Constants.SERVICE_ADMIN_VERSION);
                    request.Credentials = CredentialCache.DefaultCredentials;
                    response = request.GetResponse();
                    return response; 
                },
                delegate(WebResponse response, Exception ex)
                {
                    EventDistributor.InvokeAsyncTaskDone(Instance);

                    if (ex != null)
                        MessageBox.Show(View.ParentForm, ex.Message);
                    else
                    {
                        string status = ((HttpWebResponse)response).StatusDescription;
                        Stream dataStream = response.GetResponseStream();
                        StreamReader reader = new StreamReader(dataStream);
                        string responseFromServer = reader.ReadToEnd();
                        Dictionary<string, string> parameter = ToParameterDic(responseFromServer);

                        if (!parameter.ContainsKey(Constants.VERSION))
                            return;

                        Version oldVersion = new Version(VersionHelper.GetAssemblyVersion());
                        Version newVersion = new Version(parameter[Constants.VERSION]);
                        if (oldVersion < newVersion)
                        {
                            View.llblAdminDownload.Text = String.Format(Constants.DOWNLOAD_FILENAME_TEMPLATE, parameter[Constants.VERSION]);
                            frmUpdateDLG updateDLG = new frmUpdateDLG();
                            updateDLG.DownloadPath = DownloadPath;
                            updateDLG.PostDownloadAction = PostDownloadAction;
                            updateDLG.Message = GetDownloadMSG(parameter);

                            if (updateDLG.ShowDialog(View.ParentForm) != DialogResult.OK)
                                return;

                            DownloadPath = updateDLG.DownloadPath;
                            PostDownloadAction = updateDLG.PostDownloadAction;
                            DownloadNewAdminVersion();
                        }
                        else
                        { 
                            View.Up2Date = true;
                            Messenger.AddInfo(Messages.MSG_KSP_UPTODATE);
                        }
                    }
                });
        }

        /// <summary>
        /// Converts a string (e.g. : "Version=1.0.1;TEST=123123") to a dictionary.
        /// </summary>
        /// <param name="parameterString">A string with ; separated parameters.</param>
        /// <returns>A dictionary of parameter names and values.</returns>
        private static Dictionary<string, string> ToParameterDic(string parameterString)
        {
            parameterString = parameterString.Replace(Environment.NewLine, string.Empty);

            Dictionary<string, string> dic = new Dictionary<string, string>();
            string[] temp = parameterString.Split(';');
            foreach (string entry in temp)
            {
                if (entry == string.Empty) continue;

                string[] keyValue = entry.Split('=');
                dic.Add(keyValue[0], keyValue[1]);
            }

            return dic;
        }

        /// <summary>
        /// Creates the display message for the download / update dialog.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        private static string GetDownloadMSG(Dictionary<string, string> parameter)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(Messages.MSG_NEW_VERSION_0, parameter[Constants.VERSION]);
            sb.AppendLine();
            sb.AppendLine();
            if (parameter.ContainsKey(Constants.MESSAGE) && parameter[Constants.MESSAGE] != String.Empty)
            {
                string[] lines = parameter[Constants.MESSAGE].Split('#');
                foreach (string line in lines)
                {
                    string tempLine = line.Trim();
                    if (tempLine != string.Empty)
                        sb.AppendLine(tempLine);
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Opens a FolderBrowser to select a Download destination. 
        /// Starts a AsyncJob to download the new version.
        /// </summary>
        private static void DownloadNewAdminVersion()
        {
            // get valid download path.
            if (string.IsNullOrEmpty(DownloadPath) || !Directory.Exists(DownloadPath))
                SelectNewDownloadPath();

            if (!string.IsNullOrEmpty(DownloadPath) && Directory.Exists(DownloadPath))
            {
                string filename = View.llblAdminDownload.Text;
                string url = Constants.SERVICE_DOWNLOAD_LINK;

                int index = 1;
                string downloadDest = Path.Combine(DownloadPath, filename);
                while (File.Exists(downloadDest))
                {
                    string temp = Path.GetFileNameWithoutExtension(downloadDest).Replace("_(" + index++ + ")", "");
                    string newFilename = String.Format("{0}_({1}){2}", temp, index, Path.GetExtension(downloadDest));
                    downloadDest = Path.Combine(Path.GetDirectoryName(downloadDest), newFilename);
                }

                mTaskAction = TaskAction.DownloadApp;
                EventDistributor.InvokeAsyncTaskStarted(Instance);
                AsyncTask<bool>.RunDownload(url, downloadDest,
                                            delegate(bool result, Exception ex)
                                            {
                                                EventDistributor.InvokeAsyncTaskDone(Instance);
                                                
                                                if (ex != null)
                                                    MessageBox.Show(View.ParentForm, ex.Message);
                                                else
                                                {
                                                    switch (PostDownloadAction)
                                                    {
                                                        case PostDownloadAction.Ask:
                                                            if (MessageBox.Show(View.ParentForm, Messages.MSG_DOWNLOAD_COMPLETE_INSTALL, Messages.MSG_DOWNLOAD_COMPLETE, MessageBoxButtons.YesNo) == DialogResult.Yes)
                                                                AutoUpdateKSPMA(downloadDest);
                                                            break;
                                                        case PostDownloadAction.AutoUpdate:
                                                            AutoUpdateKSPMA(downloadDest);
                                                            break;
                                                        default: // case Views.PostDownloadAction.Ignore:
                                                            break;
                                                    }
                                                }
                                            },
                                            delegate(int progressPercentage)
                                            {
                                                View.prgBarAdminDownload.Value = progressPercentage;
                                            });
            }
        }

        /// <summary>
        /// Starts the KSPModAdmin_Updater in another process.
        /// </summary>
        /// <param name="archivePath">The path to the new KSPModAdmin archive.</param>
        private static void AutoUpdateKSPMA(string archivePath)
        {
            Process process = new Process();
            process.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), KSPMA_UPDATER_EXE);
            process.StartInfo.Arguments = string.Format(KSPMA_UPDATER_PARAMETER_0_1_2_3,
                                                        GetVersionFromArchive(archivePath),
                                                        KSPMA,
                                                        archivePath,
                                                        Path.GetDirectoryName(Application.ExecutablePath));
            process.Start();

            MainController.View.Close();
        }

        /// <summary>
        /// Extracts the version from the fullpath of the KSPModAdmin archive.
        /// </summary>
        /// <param name="archivePath">The path to the KSPModAdmin archive.</param>
        /// <returns>The version of the KSPModAdmin archive.</returns>
        private static string GetVersionFromArchive(string archivePath)
        {
            string version = Path.GetFileNameWithoutExtension(archivePath);
            int index = version.IndexOf("_");
            if (index > 0)
            {
                version = version.Substring(version.IndexOf("-v") + 2);
                index = version.IndexOf("_");
                version = version.Substring(0, index);
            }
            else
                version = version.Substring(version.IndexOf("-v") + 2);

            return version;
        }

        #endregion

        #region Update Mods

        /// <summary>
        /// Check if the auto mod update is active and starts the modInfo update process.
        /// </summary>
        /// <param name="isStartup">Flag to determine if the call of this function is aon app startup.</param>
        /// <param name="silent"></param>
        public static void Check4ModUpdates(bool isStartup = false, bool silent = false)
        {
            bool doUpdateCheck = false;
            switch (ModUpdateInterval)
            {
                case ModUpdateInterval.OnStartup:
                    doUpdateCheck = (isStartup);
                    break;
                case ModUpdateInterval.OnceADay:
                    doUpdateCheck = (LastModUpdateTry.AddHours(24) < DateTime.Now);
                    break;
                case ModUpdateInterval.EveryTwoDays:
                    doUpdateCheck = (LastModUpdateTry.AddHours(48) < DateTime.Now);
                    break;
                case ModUpdateInterval.OnceAWeek:
                    doUpdateCheck = (LastModUpdateTry.AddDays(7) < DateTime.Now);
                    break;
            }

            if (doUpdateCheck)
                Check4ModUpdates(ModSelectionController.Mods.ToArray(), silent);
        }

        /// <summary>
        /// Updates the ModInfo
        /// </summary>
        public static void Check4ModUpdates(ModNode[] mods, bool silent = false)
        {
            ModSelectionController.CheckForModUpdates(mods);

            #region OldCode

            //mTaskAction = TaskAction.ModsUpdateCheck;
            //EventDistributor.InvokeAsyncTaskStarted(Instance);
            //AsyncTask<bool>.DoWork(
            //    delegate()
            //    {
            //        bool result = false;
            //        foreach (ModNode mod in mods)
            //        {
            //            Messenger.AddInfo(string.Format("\"{0}\" checking ...", mod.Text));
            //            ModInfo modInfo = null;
            //            if (mod.VersionControl == VersionControl.KSPForum && KSPForum.IsValidURL(mod.KSPForumURL))
            //                modInfo = KSPForum.GetModInfo(mod.KSPForumURL);
            //            else if (mod.VersionControl == VersionControl.CurseForge && CurseForge.IsValidURL(CurseForge.GetCurseForgeModURL(mod.CurseForgeURL)))
            //                modInfo = CurseForge.GetModInfo(CurseForge.GetCurseForgeModURL(mod.CurseForgeURL));

            //            if (modInfo != null)
            //            {
            //                modInfo.LocalPath = string.Empty;

            //                DateTime oldDate = DateTime.MinValue;
            //                DateTime newDate = DateTime.MinValue;
            //                if (!DateTime.TryParse(mod.AddDate, out oldDate))
            //                    oldDate = DateTime.MinValue;

            //                if (!DateTime.TryParse(modInfo.CreationDate, out newDate))
            //                    newDate = DateTime.MinValue;

            //                bool updateAvailable = false;
            //                if (oldDate < newDate)
            //                    updateAvailable = true;
            //                else
            //                {
            //                    if (!DateTime.TryParse(mod.CreationDate, out oldDate))
            //                        continue;

            //                    if (oldDate < newDate)
            //                        updateAvailable = true;
            //                }

            //                if (updateAvailable)
            //                {
            //                    Messenger.AddInfo(string.Format("\"{0}\" is outdated", mod.Text));
            //                    result = true;
            //                }
            //                else
            //                {
            //                    Messenger.AddInfo(string.Format("\"{0}\" is up to date.", mod.Text));
            //                }

            //                mod.IsOutdated = updateAvailable;
            //                //mod.CreationDate = modInfo.CreationDate;
            //                mod.Rating = modInfo.Rating;
            //                mod.Downloads = modInfo.Downloads;
            //                mod.SpaceportURL = modInfo.SpaceportURL;
            //                mod.KSPForumURL = modInfo.ForumURL;
            //                mod.CurseForgeURL = modInfo.CurseForgeURL;
            //                mod.Author = modInfo.Author;
            //            }
            //            else
            //                Messenger.AddInfo(string.Format("\"{0}\" has no valid CurseForge or KSP Forum URL", mod.Text));
            //        }

            //    return result;
            //},
            //delegate(bool result, Exception ex)
            //{
            //    EventDistributor.InvokeAsyncTaskDone(Instance);

            //    if (ex != null)
            //    {
            //        MessageBox.Show(View.ParentForm, ex.Message);
            //        Messenger.AddError("Error during update check.", ex);
            //    }
            //    else
            //    {
            //        if (result && !silent)
            //        {
            //            string msg = "One or more mods are outdated.";
            //            MessageBox.Show(View.ParentForm, msg, "Update Info");
            //            // TODO: Ask to switch to ModSelection.
            //            //MessageBoxButtons buttons = MessageBoxButtons.OK;
            //            //if (MainForm.tabControl1.SelectedTab != MainForm.tabPageMods)
            //            //{
            //            //    msg += "\n\rSwitch to ModSelection?";
            //            //    buttons = MessageBoxButtons.YesNo;
            //            //}

            //            //if (MessageBox.Show(View.ParentForm, msg, "Update Info", buttons) == DialogResult.Yes)
            //            //{
            //            //    MainForm.tabControl1.SelectedTab = MainForm.tabPageMods;
            //            //    MainForm.tabControl1.Refresh();
            //            //}
            //        }

            //        LastModUpdateTry = DateTime.Now;
            //    }

            //    Messenger.AddInfo("Update check done.");
            //});

            #endregion
        }

        #endregion

        #endregion

        #region Path Tab

        /// <summary>
        /// Opens the KSP install path in a explorer window.
        /// </summary>
        public static void OpenKSPRoot()
        {
            if (string.IsNullOrEmpty(SelectedKSPPath))
            {
                MessageBox.Show(View.ParentForm, string.Format(Messages.MSG_SELECT_0_FOLDER_FIRST, KSPINSTALL));
                return;
            }

            string fullpath = SelectedKSPPath;
            try
            {
                if (Directory.Exists(fullpath))
                {
                    Messenger.AddInfo(string.Format(Messages.MSG_OPENING_0_FOLDER, KSPINSTALL));
                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = fullpath;
                    process.Start();
                }
            }
            catch (Exception ex)
            {
                Messenger.AddError(string.Format(Messages.MSG_OPEN_0_FOLDER_FAILD, KSPINSTALL), ex);
            }
        }

        /// <summary>
        /// Opens the download path in a explorer window.
        /// </summary>
        public static void OpenDownloadFolder()
        {
            if (string.IsNullOrEmpty(DownloadPath))
            {
                MessageBox.Show(View.ParentForm, string.Format(Messages.MSG_SELECT_0_FOLDER_FIRST, DOWNLOAD));
                return;
            }

            string fullpath = DownloadPath;
            try
            {
                if (Directory.Exists(fullpath))
                {
                    Messenger.AddInfo(string.Format(Messages.MSG_OPENING_0_FOLDER, DOWNLOAD));
                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = fullpath;
                    process.Start();
                }
            }
            catch (Exception ex)
            {
                Messenger.AddError(string.Format(Messages.MSG_OPEN_0_FOLDER_FAILD, DOWNLOAD), ex);
            }
        }

        /// <summary>
        /// Opens the FolderBrowserDialog and lets the user chose a new download folder.
        /// </summary>
        /// <returns>The new selected download folder or string.Empty.</returns>
        public static string SelectNewDownloadPath()
        {
            if (string.IsNullOrEmpty(SelectedKSPPath))
            {
                MessageBox.Show(View.ParentForm, string.Format(Messages.MSG_SELECT_0_FOLDER_FIRST, KSPINSTALL));
                return string.Empty;
            }

            string pathUser = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string pathDownload = Path.Combine(pathUser, DOWNLOADS);
            if (!string.IsNullOrEmpty(DownloadPath))
                pathDownload = DownloadPath;

            FolderSelectDialog dlg = new FolderSelectDialog();
            dlg.Title = Messages.MSG_DOWNLOAD_SELECTION_TITLE;
            dlg.InitialDirectory = pathDownload;
            if (dlg.ShowDialog(View.ParentForm.Handle))
                DownloadPath = dlg.FileName;

            return DownloadPath;
        }

        /// <summary>
        /// Opens a folder browser and let the user select a KSP install folder.
        /// </summary>
        public static void AddKSPPath()
        {
            string kspPath = AskForKSPInstallFolder();
            if (!string.IsNullOrEmpty(kspPath))
                AddKSPPath(kspPath);
        }

        /// <summary>
        /// Asks the user for a KSP Install path with a folder browser dialog .
        /// </summary>
        /// <returns>The selected KSPpath or String.Empty.</returns>
        public static string AskForKSPInstallFolder()
        {
            string kspPath = string.Empty;


            FolderSelectDialog dlg = new FolderSelectDialog();
            dlg.Title = Messages.MSG_KSP_INSTALL_FOLDER_SELECTION_TITLE;
            dlg.InitialDirectory = "c:/";
            if (dlg.ShowDialog(View.ParentForm.Handle))
            {
                if (KSPPathHelper.IsKSPInstallFolder(dlg.FileName))
                    kspPath = dlg.FileName;
                else
                    MessageBox.Show(Messages.MSG_NOT_KSP_FOLDER);
            }

            return kspPath;
        }

        /// <summary>
        /// Adds a KSP path to the known KSP paths.
        /// </summary>
        /// <param name="kspPath">The full path to add.</param>
        public static void AddKSPPath(string kspPath)
        {
            if (string.IsNullOrEmpty(kspPath) || !KSPPathHelper.IsKSPInstallFolder(kspPath))
                return;

            bool found = false;
            var knownPaths = KnownKSPPaths;
            foreach (NoteNode node in knownPaths)
            {
                if (node.Text != kspPath)
                    continue;

                found = true;
                break;
            }
            
            if (!found)
            {
                int lastKnownPathsCount = knownPaths.Count;
                string lastSelectedKSPPath = SelectedKSPPath;
                knownPaths.Add(new NoteNode(kspPath, kspPath, string.Empty));
                Messenger.AddInfo(string.Format(Messages.MSG_KSP_FOLDER_ADDED_0, kspPath));
                KnownKSPPaths = knownPaths;
                if (lastKnownPathsCount == 0)
                    SelectedKSPPath = kspPath;
                else
                {
                    // selected ksp path is the same so silent set.
                    SilentSetSelectedKSPPath(lastSelectedKSPPath);
                    MainController.SilentSetSelectedKSPPath(lastSelectedKSPPath);
                    View.SelectedKnownKSPPath = knownPaths.FirstOrDefault(node => node.Text == lastSelectedKSPPath);
                }
            }
        }

        /// <summary>
        /// Removes the selected known path from the known KSP paths list.
        /// </summary>
        public static void RemoveKSPPath()
        {
            if (View.SelectedKnownKSPPath != null)
            {
                var knownPaths = View.KnownKSPPaths;
                string lastPath = View.SelectedKSPPath;
                NoteNode info2Del = knownPaths.FirstOrDefault(node => node.Text == View.SelectedKnownKSPPath.Text);

                if (info2Del != null)
                {
                    if (lastPath == info2Del.Name)
                        lastPath = string.Empty;

                    knownPaths.Remove(info2Del);
                    Messenger.AddInfo(string.Format(Messages.MSG_KSP_FOLDER_REMOVED_0, info2Del.Name));

                    KnownKSPPaths = knownPaths;

                    if (knownPaths.Count <= 0)
                    {
                        SelectedKSPPath = string.Empty;
                    }
                    else if (!string.IsNullOrEmpty(lastPath))
                    {
                        // selected ksp path is the same so silent set.
                        SilentSetSelectedKSPPath((knownPaths.Count <= 0) ? string.Empty : knownPaths[knownPaths.Count - 1].Name);
                        MainController.SilentSetSelectedKSPPath(View.SelectedKSPPath);
                        View.SelectedKnownKSPPath = knownPaths.FirstOrDefault(node => node.Text == View.SelectedKSPPath);
                    }
                    else
                    {
                        View.SelectedKnownKSPPath = knownPaths[0];
                        SelectedKSPPath = View.SelectedKnownKSPPath.Name;
                    }
                }
            }
        }

        /// <summary>
        /// Starts the steam search for the KSP install folder.
        /// </summary>
        public static void SteamSearch4KSPPath()
        {
            if (View.btnSteamSearch.Tag != null && View.btnSteamSearch.Tag.ToString() == STOP)
            {
                OptionsController.StopSearch4KSPPath();
                return;
            }

            Messenger.AddInfo(Messages.MSG_STEAM_SEARCH_STARTED);

            mStopSearch = false;
            mTaskAction = TaskAction.SteamSearch;
            EventDistributor.InvokeAsyncTaskStarted(Instance);
            AsyncTask<string[]>.DoWork(() =>
            {
                // ToDo: Search for other OSs!
                string path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string[] paths = Directory.GetDirectories(path, STEAM, SearchOption.TopDirectoryOnly);
                if (paths.Length == 0)
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    paths = Directory.GetDirectories(path, STEAM, SearchOption.TopDirectoryOnly);
                }

                return paths;
            }, (paths, ex) =>
            {
                EventDistributor.InvokeAsyncTaskDone(Instance);

                if (ex != null)
                {
                    MessageBox.Show(View.ParentForm, ex.Message);
                    Messenger.AddError(Messages.MSG_ERROR_WHILE_STEAM_SEARCH, ex);
                }
                else
                {
                    foreach (string p in paths)
                    {
                        string d = Path.Combine(p, STEAMAPP_PATH);
                        string[] dirs = Directory.GetDirectories(d, KSP, SearchOption.TopDirectoryOnly);

                        List<string> list = new List<string>();
                        foreach (string dir in dirs)
                        {
                            if (mStopSearch || !SearchSubDirs(dir, 2, ref list))
                                break;
                        }

                        TryAddPaths(list.ToArray());
                    }

                    if (paths.Length == 0)
                        Messenger.AddInfo(Messages.MSG_STEAM_FOLDER_NOT_FOUND);
                    else
                        Messenger.AddInfo(Messages.MSG_STEAM_SEARCH_DONE);
                }
            });
        }

        /// <summary>
        /// Starts the normal search for the KSP install folder.
        /// </summary>
        public static void FolderSearch4KSPPath()
        {
            if (View.btnKSPFolderSearch.Tag != null && View.btnKSPFolderSearch.Tag.ToString() == STOP)
            {
                OptionsController.StopSearch4KSPPath();
                return;
            }

            Messenger.AddInfo(Messages.MSG_KSP_SEARCH_STARTED);

            mStopSearch = false;
            mTaskAction = TaskAction.FolderSearch;
            EventDistributor.InvokeAsyncTaskStarted(Instance);
            AsyncTask<List<string>>.DoWork(() =>
            {
                string adminDir = Path.GetDirectoryName(Application.ExecutablePath);

                int depth = 1;
                bool done = false;
                DirectoryInfo dirInfo = Directory.GetParent(adminDir);
                while ((depth <= View.SearchDepth || 0 == View.SearchDepth) && !done && !mStopSearch)
                {
                    if (dirInfo == null || dirInfo.Root.FullName == dirInfo.FullName)
                    {
                        done = true;
                        break;
                    }

                    dirInfo = dirInfo.Parent;
                    ++depth;
                }

                List<string> list = new List<string>();
                if (dirInfo != null)
                    SearchSubDirs(dirInfo.FullName, View.SearchDepth * 2, ref list);

                return list;
            }, (list, ex) =>
            {
                EventDistributor.InvokeAsyncTaskDone(Instance);

                if (ex != null)
                {
                    MessageBox.Show(View.ParentForm, ex.Message);
                    Messenger.AddError(Messages.MSG_ERROR_WHILE_FOLDER_SEARCH, ex);
                }
                else
                {
                    if (!mStopSearch)
                    {
                        TryAddPaths(list.ToArray());

                        if (list.Count == 0)
                            Messenger.AddInfo(Messages.MSG_KSP_FOLDER_NOT_FOUND);
                        else
                            Messenger.AddInfo(Messages.MSG_KSP_SEARCH_DONE);
                    }
                }
            });
        }

        /// <summary>
        /// Stops the search of ksp install folder.
        /// </summary>
        public static void StopSearch4KSPPath()
        {
            Messenger.AddInfo(Messages.MSG_FOLDER_SEARCH_ABORTED);
            mStopSearch = true;
        }


        /// <summary>
        /// Sets the selected KSP path without raising event SelectedIndexChanged.
        /// </summary>
        /// <param name="kspPath">The new selected KSP path.</param>
        internal static void SilentSetSelectedKSPPath(string kspPath)
        {
            if (View != null)
                View.SilentSetSelectedKSPPath(kspPath);
        }


        /// <summary>
        /// Searches the sub directories for the KSP install folder.
        /// </summary>
        /// <param name="dirName">The directory to check the subdirectories from.</param>
        /// <param name="searchDepth">The current depth of the sub folder.</param>
        /// <param name="foundKSPDirs"></param>
        /// <param name="stopOnFirstHit"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        private static bool SearchSubDirs(string dirName, int searchDepth, ref List<string> foundKSPDirs, bool stopOnFirstHit = false, int depth = 1)
        {
            if ((searchDepth != 0 && depth + 1 > searchDepth) || mStopSearch)
                return true;

            string currentDir = string.Empty;

            try
            {
                bool continueSearch = true;
                string[] subDirs = Directory.GetDirectories(dirName);
                foreach (string dir in subDirs)
                {
                    currentDir = dir;

                    if (mStopSearch)
                        break;

                    if (KSPPathHelper.IsKSPInstallFolder(dir) && !dir.ToLower().Contains(RECYCLE_BIN))
                    {
                        switch (AskUser(dir))
                        {
                            case DialogResult.Yes:
                                foundKSPDirs.Add(dir);
                                continueSearch = !stopOnFirstHit;
                                break;
                            case DialogResult.No:
                                continueSearch = true;
                                break;
                            case DialogResult.Cancel:
                                continueSearch = false;
                                break;
                        }

                        if (!continueSearch)
                            return continueSearch;
                    }

                    if (!continueSearch)
                        return continueSearch;
                    else
                        if (!SearchSubDirs(dir, searchDepth, ref foundKSPDirs, stopOnFirstHit, depth + 1))
                            return false;
                }
            }
            catch (Exception ex)
            {
                // ignore directories where we aren't authorized for.
                Messenger.AddError(string.Format(Messages.MSG_ERROR_DURING_FOLDER_SEARCH_0, currentDir), ex);
            }

            return true;
        }

        /// <summary>
        /// Opens a MessageBox to ask the user if he wants to take the found KSP folder or continue the search.
        /// </summary>
        /// <param name="dir">The found KSP install directory.</param>
        /// <returns>True if the User wants to take the directory otherwise false.</returns>
        private static DialogResult AskUser(string dir)
        {
            foreach (NoteNode path in View.KnownKSPPaths)
            {
                if (path.Name == dir)
                    return DialogResult.No;
            }

            string msg = string.Format(Messages.MSG_KSP_FOLDER_FOUND_DIALOG_TEXT, dir);
            DialogResult result = DialogResult.Cancel;
            View.InvokeIfRequired(() => result = MessageBox.Show(View.ParentForm, msg, Messages.MSG_KSP_FOLDER_FOUND_DIALOG_TITLE, MessageBoxButtons.YesNoCancel));

            return result;
        }

        /// <summary>
        /// Checks the string array for valid KSP install paths and adds them to the known paths.
        /// </summary>
        /// <param name="files">The string array of paths to check.</param>
        private static void TryAddPaths(string[] files)
        {
            foreach (string file in files)
            {
                bool isKSPDir = false;
                try
                {
                    string dir = file;
                    if (file.EndsWith(Constants.KSP_EXE))
                        dir = Path.GetDirectoryName(file);
                    if (KSPPathHelper.IsKSPInstallFolder(dir))
                        isKSPDir = true;
                    if (dir != null && dir.ToLower().Contains(RECYCLE_BIN))
                        isKSPDir = false;
                }
                catch (Exception ex)
                {
                    Messenger.AddError(string.Format(Messages.MSG_ERROR_ADD_FOLDER_FAILED_0, file), ex);
                }

                if (isKSPDir)
                    AddKSPPath(file);
                else
                    Messenger.AddInfo(string.Format(Messages.MSG_ERROR_NOT_A_KSP_FOLDER_0, file));
            }
        }

        #endregion

        #endregion

        /// <summary>
        /// This method gets called when your Controller should be initialized.
        /// Perform additional initialization of your UserControl here.
        /// </summary>
        protected override void Initialize()
        {
            View.KSPMAVersion = VersionHelper.GetAssemblyVersion();
        }

        /// <summary>
        /// This method gets called when a critical asynchrony task will be started.
        /// Disable all controls of your View here to avoid multiple critical KSP MA changes.
        /// </summary>
        protected override void AsyncroneTaskStarted(object sender)
        {
            if (sender == this)
            {
                switch (mTaskAction)
                {
                    case TaskAction.AppUpdateCheck:
                        View.gbPaths.Enabled = false;
                        View.btnUpdate.Enabled = false;
                        View.btnUpdate.Text = Messages.MSG_CHECKING;
                        View.btnCheckModUpdates.Enabled = false;
                        View.pbUpdateLoad.Visible = true;
                        View.pbUp2Date.Visible = false;
                        View.cbPostDownloadAction.Enabled = false;
                        View.btnCheckModUpdates.Enabled = false;
                        View.llblAdminDownload.Enabled = false;
                        break;
                    case TaskAction.DownloadApp:
                        View.gbPaths.Enabled = false;
                        View.btnUpdate.Enabled = false;
                        View.btnUpdate.Text = Messages.MSG_DOWNLOADING_UPDATE;
                        View.btnCheckModUpdates.Enabled = false;
                        View.pbUpdateLoad.Visible = true;
                        View.pbUp2Date.Visible = false;
                        View.cbPostDownloadAction.Enabled = false;

                        View.btnCheckModUpdates.Enabled = false;
                        View.prgBarAdminDownload.Visible = true;
                        View.llblAdminDownload.Visible = false;
                        break;
                    case TaskAction.ModsUpdateCheck:
                        View.gbPaths.Enabled = false;
                        View.btnUpdate.Enabled = false;
                        View.llblAdminDownload.Enabled = false;
                        View.btnCheckModUpdates.Enabled = false;
                        View.btnCheckModUpdates.Text = Messages.MSG_CHECKING;
                        View.pbUpdateLoad.Visible = true;
                        View.pbUp2Date.Visible = false;
                        View.cbPostDownloadAction.Enabled = false;
                        View.cbModUpdateInterval.Enabled = false;
                        View.cbModUpdateBehavior.Enabled = false;
                        View.btnUpdate.Enabled = false;
                        break;
                    case TaskAction.SteamSearch:
                        View.btnUpdate.Enabled = false;
                        View.btnCheckModUpdates.Enabled = false;
                        View.btnOpenDownloads.Enabled = false;
                        View.llblAdminDownload.Visible = false;
                        View.cbKSPPath.Enabled = false;
                        View.btnOpenKSPRoot.Enabled = false;
                        View.btnAddPath.Enabled = false;
                        View.btnRemove.Enabled = false;
                        View.btnSteamSearch.Image = Properties.Resources.stop;
                        View.btnSteamSearch.Tag = STOP;
                        View.btnKSPFolderSearch.Enabled = false;
                        View.tbDepth.Enabled = false;
                        View.btnDownloadPath.Enabled = false;
                        View.btnOpenDownloadFolder.Enabled = false;
                        View.splitContainer1.Enabled = false;
                        View.tlpSearchBG.Visible = true;
                        break;
                    case TaskAction.FolderSearch:
                        View.btnUpdate.Enabled = false;
                        View.btnCheckModUpdates.Enabled = false;
                        View.btnOpenDownloads.Enabled = false;
                        View.llblAdminDownload.Visible = false;
                        View.cbKSPPath.Enabled = false;
                        View.btnOpenKSPRoot.Enabled = false;
                        View.btnAddPath.Enabled = false;
                        View.btnRemove.Enabled = false;
                        View.btnSteamSearch.Enabled = false;
                        View.btnKSPFolderSearch.Image = Properties.Resources.stop;
                        View.btnKSPFolderSearch.Tag = STOP;
                        View.tbDepth.Enabled = false;
                        View.btnDownloadPath.Enabled = false;
                        View.btnOpenDownloadFolder.Enabled = false;
                        View.splitContainer1.Enabled = false;
                        View.tlpSearchBG.Visible = true;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                View.btnUpdate.Enabled = false;
                View.btnCheckModUpdates.Enabled = false;
                View.btnOpenDownloads.Enabled = false;
                View.llblAdminDownload.Visible = false;
                View.gbPaths.Enabled = false;
            }
        }

        /// <summary>
        /// This method gets called when a critical asynchrony task is complete.
        /// Enable the controls of your View again here.
        /// </summary>
        protected override void AsyncroneTaskDone(object sender)
        {
            if (sender == this)
            {
                switch (mTaskAction)
                {
                    case TaskAction.AppUpdateCheck:
                        View.gbPaths.Enabled = true;
                        View.btnUpdate.Enabled = true;
                        View.btnUpdate.Text = Messages.MSG_CHECK_FOR_KSPMA_UPDATES;
                        View.btnCheckModUpdates.Enabled = false;
                        View.pbUpdateLoad.Visible = false;
                        View.cbPostDownloadAction.Enabled = true;
                        View.btnCheckModUpdates.Enabled = true;
                        View.llblAdminDownload.Enabled = true;
                        break;
                    case TaskAction.DownloadApp:
                        View.gbPaths.Enabled = true;
                        View.btnUpdate.Enabled = true;
                        View.btnUpdate.Text = Messages.MSG_CHECK_FOR_KSPMA_UPDATES;
                        View.btnCheckModUpdates.Enabled = false;
                        View.pbUpdateLoad.Visible = false;
                        View.cbPostDownloadAction.Enabled = true;
                        View.btnCheckModUpdates.Enabled = true;
                        View.llblAdminDownload.Visible = true;
                        View.prgBarAdminDownload.Visible = false;
                        View.prgBarAdminDownload.Value = 0;
                        break;
                    case TaskAction.ModsUpdateCheck:
                        View.gbPaths.Enabled = true;
                        View.btnUpdate.Enabled = true;
                        View.llblAdminDownload.Enabled = true;
                        View.btnCheckModUpdates.Enabled = true;
                        View.btnCheckModUpdates.Text = Messages.MSG_CHECK_FOR_KSPMA_UPDATES;
                        View.pbUpdateLoad.Visible = false;
                        View.cbPostDownloadAction.Enabled = true;
                        View.cbModUpdateInterval.Enabled = true;
                        View.cbModUpdateBehavior.Enabled = true;
                        View.btnUpdate.Enabled = true;
                        break;
                    case TaskAction.SteamSearch:
                        View.btnUpdate.Enabled = true;
                        View.btnCheckModUpdates.Enabled = true;
                        View.btnOpenDownloads.Enabled = true;
                        View.llblAdminDownload.Visible = true;
                        View.cbKSPPath.Enabled = true;
                        View.btnOpenKSPRoot.Enabled = true;
                        View.btnAddPath.Enabled = true;
                        View.btnRemove.Enabled = true;
                        View.btnSteamSearch.Image = Properties.Resources.folder_tool;
                        View.btnSteamSearch.Tag = START;
                        View.btnKSPFolderSearch.Enabled = true;
                        View.tbDepth.Enabled = true;
                        View.btnDownloadPath.Enabled = true;
                        View.btnOpenDownloadFolder.Enabled = true;
                        View.splitContainer1.Enabled = true;
                        View.tlpSearchBG.Visible = false;
                        break;
                    case TaskAction.FolderSearch:
                        View.btnUpdate.Enabled = true;
                        View.btnCheckModUpdates.Enabled = true;
                        View.btnOpenDownloads.Enabled = true;
                        View.llblAdminDownload.Visible = true;
                        View.cbKSPPath.Enabled = true;
                        View.btnOpenKSPRoot.Enabled = true;
                        View.btnAddPath.Enabled = true;
                        View.btnRemove.Enabled = true;
                        View.btnSteamSearch.Enabled = true;
                        View.btnKSPFolderSearch.Image = Properties.Resources.folder_view;
                        View.btnKSPFolderSearch.Tag = START;
                        View.tbDepth.Enabled = true;
                        View.btnDownloadPath.Enabled = true;
                        View.btnOpenDownloadFolder.Enabled = true;
                        View.splitContainer1.Enabled = true;
                        View.tlpSearchBG.Visible = false;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                View.btnUpdate.Enabled = true;
                View.btnCheckModUpdates.Enabled = true;
                View.btnOpenDownloads.Enabled = true;
                View.llblAdminDownload.Visible = true;
                View.gbPaths.Enabled = true;
            }

            mTaskAction = TaskAction.None;
        }

        /// <summary>
        /// This method gets called when the language of KSP MA was changed.
        /// Perform extra translation work for your View here.
        /// </summary>
        protected override void LanguageHasChanged(object sender)
        {

        }
    }
}