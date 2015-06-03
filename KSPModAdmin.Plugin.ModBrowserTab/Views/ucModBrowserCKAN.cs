﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using KSPModAdmin.Core.Controller;
using KSPMODAdmin.Core.Utils.Ckan;
using KSPModAdmin.Core.Utils.Controls.Aga.Controls.Tree;
using KSPModAdmin.Core.Utils.Controls.Aga.Controls.Tree.Helper;
using KSPModAdmin.Core.Utils.Localization;
using KSPModAdmin.Core.Views;
using KSPModAdmin.Plugin.ModBrowserTab.Controller;
using KSPModAdmin.Plugin.ModBrowserTab.Model;

namespace KSPModAdmin.Plugin.ModBrowserTab.Views
{
    public partial class ucModBrowserCKAN : ucBase, IKSPMAModBrowser
    {
        #region Member

        private CkanRepositories repositories;
        private bool updating = false;
        private bool firstStart = false;//true;

        #endregion

        #region Properties

        public string ModBrowserName { get { return "CKAN"; } }

        public string Description { get { return "Mod Browser for CKAN"; } }

        public UserControl ModBrowserView { get { return this; } }

        public CkanTreeModel Model
        {
            get { return tvCkanRepositories.Model as CkanTreeModel; }
            set { tvCkanRepositories.Model = value; }
        }

        public string CountLabelText
        {
            get { return lblModBrowserCkanCount.Text; }
            set { lblModBrowserCkanCount.Text = value; }
        }

        public CkanRepositories Repositories
        {
            get { return repositories; }
            set
            {
                repositories = value;

                cbModBrowserCkanRepository.Items.Clear();
                foreach (var repo in repositories.repositories)
                    cbModBrowserCkanRepository.Items.Add(repo);
            }
        }

        public CkanRepository SelectedRepository
        {
            get { return cbModBrowserCkanRepository.SelectedItem as CkanRepository; }
            set
            {
                if (value == null)
                {
                    cbModBrowserCkanRepository.SelectedItem = null;
                }
                else
                {
                    var repo = cbModBrowserCkanRepository.Items.Cast<CkanRepository>().FirstOrDefault(r => r.name == value.name);
                    cbModBrowserCkanRepository.SelectedItem = repo;
                }
            }
        }

        private List<ColumnData> Columns
        {
            get
            {
                List<ColumnData> columns = new List<ColumnData>()
                {
                    new ColumnData()
                    {
                        Name = "Name",
                        Header = Localizer.GlobalInstance["UcModBrowserCKANView_Item_00"], // "Name",
                        SortOrder = SortOrder.None,
                        TooltipText = null,
                        Width = 250,
                        Items = new List<ColumnItemData>()
                        {
                            new ColumnItemData()
                            {
                                Type = ColumnItemType.NodeCheckBox,
                                DataPropertyName = "Checked",
                                EditEnabled = true,
                                LeftMargin = 0
                            },
                            new ColumnItemData()
                            {
                                Type = ColumnItemType.NodeIcon,
                                DataPropertyName = "Icon",
                                LeftMargin = 3,
                                ImageScaleMode = ImageScaleMode.Clip
                            }, 
                            new ColumnItemData()
                            {
                                Type = ColumnItemType.NodeTextBox,
                                DataPropertyName = "Name",
                                IncrementalSearchEnabled = true,
                                LeftMargin = 0,
                            }
                        }
                    },
                    new ColumnData()
                    {
                        Name = "Version",
                        Header = Localizer.GlobalInstance["UcModBrowserCKANView_Item_01"], // "Version",
                        SortOrder = SortOrder.None,
                        TooltipText = null,
                        Width = 50,
                        Items = new List<ColumnItemData>()
                        {
                            new ColumnItemData()
                            {
                                Type = ColumnItemType.NodeTextBox,
                                DataPropertyName = "Version",
                                IncrementalSearchEnabled = true,
                                LeftMargin = 3
                            }
                        }
                    },
                    new ColumnData()
                    {
                        Name = "Author",
                        Header = Localizer.GlobalInstance["UcModBrowserCKANView_Item_02"], // "Author",
                        SortOrder = SortOrder.None,
                        TooltipText = null,
                        Width = 90,
                        Items = new List<ColumnItemData>()
                        {
                            new ColumnItemData()
                            {
                                Type = ColumnItemType.NodeTextBox,
                                DataPropertyName = "Author",
                                IncrementalSearchEnabled = true,
                                LeftMargin = 3
                            }
                        }
                    },
                    new ColumnData()
                    {
                        Name = "Description",
                        Header = Localizer.GlobalInstance["UcModBrowserCKANView_Item_03"], // "Description",
                        SortOrder = SortOrder.None,
                        TooltipText = null,
                        Width = 350,
                        Items = new List<ColumnItemData>()
                        {
                            new ColumnItemData()
                            {
                                Type = ColumnItemType.NodeTextBox,
                                DataPropertyName = "Description",
                                IncrementalSearchEnabled = true,
                                LeftMargin = 3
                            }
                        }
                    }
                };

                return columns;
            }
        }

        #endregion

        /// <summary>
        /// Creates a new instance of the ucTranslationView class.
        /// </summary>
        public ucModBrowserCKAN()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
                return;

            ModBrowserCKANController.Initialize(this);
        }

        #region Event handling

        private void ucModBrowserCKAN_Load(object sender, EventArgs e)
        {
            // do View related init here or in the PluginViewController.Initialize(...) methode.
            TreeViewAdvColumnHelper.ColumnsToTreeViewAdv(tvCkanRepositories, Columns);
            ModBrowserCKANController.RefreshCkanRepositories();
        }

        private void tsbModBrowserCkanRefresh_Click(object sender, EventArgs e)
        {
            updating = true;
            ModBrowserCKANController.RefreshCkanRepositories();
            ModBrowserCKANController.RefreshCkanArchive(cbModBrowserCkanRepository.SelectedItem as CkanRepository, true);
            updating = false;
        }

        private void btnModBrowserCkanProcessChanges_Click(object sender, EventArgs e)
        {
            ModBrowserCKANController.ProcessChanges();
        }

        private void cbModBrowserCkanRepository_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updating)
                return;

            var sel = cbModBrowserCkanRepository.SelectedItem as CkanRepository;
            tableLayoutPanel1.Visible = (sel == null);

            if (sel != null)
                ModBrowserCKANController.RefreshCkanArchive(sel, firstStart);

            firstStart = false;
        }

        private void cbModBrowserCkanRepository_DropDown(object sender, EventArgs e)
        {
            var cb = sender as ToolStripComboBox;
            if (cb == null)
                return;

            int maxWidth = 0;
            int temp = 0;
            Label label1 = new Label();

            foreach (var obj in cb.Items)
            {
                label1.Text = obj.ToString();
                temp = label1.PreferredWidth;
                if (temp > maxWidth)
                    maxWidth = temp;
            }
            label1.Dispose();

            if (maxWidth > cb.Width)
                cb.DropDownWidth = maxWidth;
        }

        private void tvCkanRepositories_DrawControl(object sender, Core.Utils.Controls.Aga.Controls.Tree.NodeControls.DrawEventArgs e)
        {
            var node = e.Node.Tag as CkanNode;
            if (node == null)
                return;

            if (node.Added || node.ChildAdded)
                e.TextColor = OptionsController.ColorModInstalled;
            //if (node.IsOutdated)
            //    e.TextColor = OptionsController.ColorModOutdated;
        }

        internal void LanguageChanged()
        {
            // translates the controls of the view.
            ControlTranslator.TranslateControls(Localizer.GlobalInstance, this as Control, OptionsController.SelectedLanguage);

            // translate columns of ModSelection TreeView
            foreach (NamedTreeColumn column in tvCkanRepositories.Columns)
            {
                var newColData = TreeViewAdvColumnHelper.GetColumn(Columns, column.Name);
                if (newColData != null)
                    column.Header = newColData.Header;
            }
        }

        #endregion

        /// <summary>
        /// Forces the view to redraw.
        /// </summary>
        public override void InvalidateView()
        {
            Invalidate();
            Update();
            Refresh();
        }

        /// <summary>
        /// Gets the Name for the parent TabPage.
        /// </summary>
        /// <returns>The Name for the parent TabPage.</returns>
        public override string GetTabCaption()
        {
            return Messages.MSG_MODBROWSER_CKAN_VIEW_TITLE;
        }

        /// <summary>
        /// Sets the enabled state of some view controls.
        /// </summary>
        public void SetEnabledOfAllControls(bool enable)
        {
            // Enable/Disable your View Controls here.
            // Normally when KSP MA calls this methode with enable = false, all controls should be disabled.
            ////this.Enabled = enable;
            cbModBrowserCkanRepository.Enabled = enable;
            tsbModBrowserCkanRefresh.Enabled = enable;
            tvCkanRepositories.Enabled = enable;
            cbModBrowserCkanJustAdd.Enabled = enable;
            btnModBrowserCkanProcessChanges.Enabled = enable;
        }
    }
}