﻿/*
 * Copyright © 2016 - 2020 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * 
 */
using EDDiscovery.Controls;
using EliteDangerousCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EDDiscovery.UserControls
{
    public partial class UserControlOutfitting : UserControlCommonBase
    {
        private string dbYardSave = "Select";

        #region Init

        public UserControlOutfitting()
        {
            InitializeComponent();
        }
        public override void Init()
        {
            DBBaseName = "Outfitting";

            dataGridViewOutfitting.MakeDoubleBuffered();
            dataGridViewOutfitting.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            DiscoveryForm.OnHistoryChange += Discoveryform_OnHistoryChange; ;
            DiscoveryForm.OnNewEntry += Discoveryform_OnNewEntry;

            Col1.HeaderText = Col2.HeaderText = Col3.HeaderText = Col4.HeaderText = ColPrice.HeaderText = ""; // zero because no values until a display selected

            var enumlist = new Enum[] { EDTx.UserControlOutfitting_labelYardSel, EDTx.UserControlOutfitting_labelYard };
            var enumlisttt = new Enum[] { EDTx.UserControlOutfitting_comboBoxYards_ToolTip };

            BaseUtils.Translator.Instance.TranslateControls(this, enumlist);
            BaseUtils.Translator.Instance.TranslateTooltip(toolTip, enumlisttt, this);
        }

        public override void LoadLayout()
        {
            dataGridViewOutfitting.RowTemplate.MinimumHeight = Font.ScalePixels(26);
            DGVLoadColumnLayout(dataGridViewOutfitting);
        }

        public override void Closing()
        {
            DGVSaveColumnLayout(dataGridViewOutfitting);
            DiscoveryForm.OnNewEntry -= Discoveryform_OnNewEntry;
            DiscoveryForm.OnHistoryChange -= Discoveryform_OnHistoryChange;
        }

        #endregion

        #region Display

        private void Discoveryform_OnNewEntry(HistoryEntry he)
        {
            if (he.EntryType == JournalTypeEnum.Outfitting)    // only this affects the list..
            {
                UpdateComboBox();
            }
        }

        private void Discoveryform_OnHistoryChange()
        {
            UpdateComboBox();
        }

        private void UpdateComboBox()
        {
            OutfittingList ofl = DiscoveryForm.History.Outfitting;
            string cursel = comboBoxYards.Text;

            string the = "Travel History Entry".T(EDTx.UserControlOutfitting_TravelHistoryEntry);

            comboBoxYards.Items.Clear();
            comboBoxYards.Items.Add(the);

            comboBoxYards.Items.AddRange(ItemData.GetAllModTypes());

            var list = (from x in ofl.GetFilteredList() select x.Ident()).ToList();
            comboBoxYards.Items.AddRange(list);

            if (cursel == "")
                cursel = GetSetting(dbYardSave, "");

            if (cursel == "" || !comboBoxYards.Items.Contains(cursel))
                cursel = the;

            comboBoxYards.Enabled = false;
            comboBoxYards.SelectedItem = cursel;
            comboBoxYards.Enabled = true;
        }

        public override void InitialDisplay()
        {
            RequestPanelOperation(this, new UserControlCommonBase.RequestTravelHistoryPos());     //request an update 
        }

        HistoryEntry last_he = null;

        public override void ReceiveHistoryEntry(HistoryEntry he)
        {
            if (comboBoxYards.Items.Count == 0)
                UpdateComboBox();

            last_he = he;
            Display();
        }

        private void Display()
        {
            DataGridViewColumn sortcol = dataGridViewOutfitting.SortedColumn != null ? dataGridViewOutfitting.SortedColumn : dataGridViewOutfitting.Columns[0];
            SortOrder sortorder = dataGridViewOutfitting.SortOrder;

            dataGridViewOutfitting.Rows.Clear();
            dataViewScrollerPanel.SuspendLayout();

            labelYard.Visible = false;

            Outfitting yard = null;

            if (comboBoxYards.SelectedIndex == 0 || comboBoxYards.Text.Length == 0)  // second is due to the order History gets called vs this on start
            {
                HistoryEntry lastshipyard = DiscoveryForm.History.GetLastHistoryEntry(x => x.EntryType == JournalTypeEnum.Outfitting, last_he);
                if (lastshipyard != null)
                    yard = (lastshipyard.journalEntry as EliteDangerousCore.JournalEvents.JournalOutfitting).YardInfo;      // this may pick up an empty yard..
            }
            else
            {
                yard = DiscoveryForm.History.Outfitting.GetFilteredList().Find(x => x.Ident().Equals(comboBoxYards.Text));
            }

            if (yard?.Items != null ) // yard may be null, and its entries may be null
            {
                DisplayYard(yard);
            }
            else
            {
                List<Tuple<Outfitting, List<Outfitting.OutfittingItem>>> itemlist = DiscoveryForm.History.Outfitting.GetItemTypeLocationsFromYardsWithoutRepeat(comboBoxYards.Text,nolocrepeats:true);
                if ( itemlist.Count > 0 )
                    DisplayItems(itemlist, comboBoxYards.Text);
            }

            dataViewScrollerPanel.ResumeLayout();

            dataGridViewOutfitting.Sort(sortcol, (sortorder == SortOrder.Descending) ? ListSortDirection.Descending : ListSortDirection.Ascending);
            dataGridViewOutfitting.Columns[sortcol.Index].HeaderCell.SortGlyphDirection = sortorder;
        }

        private void DisplayItems(List<Tuple<Outfitting, List<Outfitting.OutfittingItem>>> itemlist, string moduletype)
        {
            ISystem cursys = DiscoveryForm.History.CurrentSystem();

            foreach (var yard in itemlist)
            {
                double distance = DiscoveryForm.History.DistanceCurrentTo(yard.Item1.StarSystem);
                string dte = EDDConfig.Instance.ConvertTimeToSelectedFromUTC(yard.Item1.Datetimeutc).ToString();
                string yardname = yard.Item1.Location;

                foreach (var item in yard.Item2)
                {
                    string itemname = item.Name.StartsWith(item.ModType) ? item.Name.Mid(item.ModType.Length+1) : item.Name;
                    ItemData.ShipModule sm = ItemData.GetShipModuleProperties(item.FDName);
                    itemname = itemname.AppendPrePad(sm.InfoMassPower(true), ", ");

                    object[] rowobj = { dte, yardname, itemname, (distance > -1) ? (distance.ToString("N1") + "ly") : "Unknown".T(EDTx.Unknown), item.BuyPrice.ToString("N1") + "cr" };
                    dataGridViewOutfitting.Rows.Add(rowobj);
                }
            }

            labelYard.Text = moduletype;
            labelYard.Visible = true;
            Col1.HeaderText = "Date".T(EDTx.UserControlOutfitting_Date);
            Col1.Tag = "DT";
            Col2.HeaderText = "Yard".T(EDTx.UserControlOutfitting_Yard);
            Col2.Tag = null;
            Col3.HeaderText = "Item".T(EDTx.UserControlOutfitting_Item);
            Col3.Tag = null;
            Col4.HeaderText = "Distance".T(EDTx.UserControlOutfitting_Distance);
            Col4.Tag = "ly";
            ColPrice.HeaderText = "Price".T(EDTx.UserControlOutfitting_Price);
            ColPrice.Tag = "cr";
        }

        private void DisplayYard(Outfitting yard)
        {
            foreach (var i in yard.Items)
            {
                ItemData.ShipModule sm = ItemData.GetShipModuleProperties(i.FDName);
                //string namepart = i.Name.Left("Class", StringComparison.InvariantCultureIgnoreCase, true), classpart = i.Name.Mid("Class", StringComparison.InvariantCultureIgnoreCase, false);

                string info = sm.InfoMassPower(false);

                object[] rowobj = { i.ModType, i.Name, info, sm.Mass.ToString("0.#t"),i.BuyPrice.ToString("N1") + "cr" };
                dataGridViewOutfitting.Rows.Add(rowobj);
            }

            double distance = DiscoveryForm.History.DistanceCurrentTo(yard.StarSystem);

            labelYard.Text = yard.Ident() + (distance > -1 ? (" @ " + distance.ToString("N1") + "ly") : "");
            labelYard.Visible = true;

            Col1.HeaderText = "Type".T(EDTx.UserControlOutfitting_Type);
            Col1.Tag = null;
            Col2.HeaderText = "Item".T(EDTx.UserControlOutfitting_Item);
            Col2.Tag = null;
            Col3.HeaderText = "Info".T(EDTx.UserControlOutfitting_Info);
            Col3.Tag = null;
            Col4.HeaderText = "Mass".T(EDTx.UserControlOutfitting_Mass);
            Col4.Tag = "t";
            ColPrice.HeaderText = "Price".T(EDTx.UserControlOutfitting_Price);
            ColPrice.Tag = "cr";
        }

        #endregion


        private void comboBoxHistoryWindow_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxYards.Enabled)
            {
                PutSetting(dbYardSave, comboBoxYards.Text);
                Display();
            }
        }

        private void dataGridView_SortCompare(object sender, DataGridViewSortCompareEventArgs e)
        {
            if ( e.Column.Tag != null )
            {
                string t = (string)e.Column.Tag;

                if (t == "DT")
                    e.SortDataGridViewColumnDate();
                else
                    e.SortDataGridViewColumnNumeric(t);
            }
        }

    }
}
